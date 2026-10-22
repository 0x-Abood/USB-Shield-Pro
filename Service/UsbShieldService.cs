// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Background Security Agent & Named Pipe Service
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Service
{
    /// <summary>
    /// Background Windows Service providing 24/7 device monitoring, automated temp-unlock
    /// expiration across reboots, and secure local IPC.
    /// Invariant: Shared engine logic with Portable Mode — no duplicated business logic.
    /// Invariant: Strongly-typed IPC operations only — no arbitrary command execution.
    /// </summary>
    public class UsbShieldService : ServiceBase
    {
        public const string ServiceNameId = "UsbShieldService";
        public const string PipeName = "UsbShieldSecurityPipe";

        private Thread _serverThread;
        private Thread _sentinelThread;
        private volatile bool _running = false;
        private volatile NamedPipeServerStream _activePipe = null;
        private ProtectionPolicy _currentPolicy;
        private readonly AutoResetEvent _wakeEvent = new AutoResetEvent(false);
        private readonly object _policyLock = new object();

        private static DateTime _lastSentinelSweepUtc = DateTime.MinValue;
        private static int _sentinelSweepCount = 0;
        private static int _currentPolicyVersion = 0;

        public static DateTime LastSentinelSweepUtc
        {
            get { return _lastSentinelSweepUtc; }
            set { _lastSentinelSweepUtc = value; }
        }

        public static int SentinelSweepCount
        {
            get { return _sentinelSweepCount; }
            set { _sentinelSweepCount = value; }
        }

        public static int CurrentPolicyVersion
        {
            get { return _currentPolicyVersion; }
            set { _currentPolicyVersion = value; }
        }

        public Thread SentinelThreadInstance
        {
            get { return _sentinelThread; }
            set { _sentinelThread = value; }
        }

        public UsbShieldService()
        {
            ServiceName = ServiceNameId;
            CanStop = true;
            CanShutdown = true;
        }

        public void WakeSentinelSweep()
        {
            try { _wakeEvent.Set(); } catch { }
        }

        protected override void OnStart(string[] args)
        {
            _running = true;

            // Enforce machine-level directory ownership and ACLs
            string secError;
            SecurityAclHelper.EnsureAllOwnershipAndAclsElevated(out secError);

            // Reconcile startup state (in case PC was rebooted while temporary access was active)
            bool wasExpired;
            TemporaryAccessManager.ReconcileStartupState(out wasExpired);

            // Load policy from settings
            SettingsRepository.ResetCache();
            AppSettingsData appSettings = SettingsRepository.Load();
            lock (_policyLock)
            {
                _currentPolicy = (appSettings != null && appSettings.ActivePolicy != null) ? appSettings.ActivePolicy : SettingsRepository.LoadPolicy();
                _currentPolicyVersion = (_currentPolicy != null) ? _currentPolicy.PolicyVersion : 1;
            }
            _lastSentinelSweepUtc = DateTime.UtcNow;

            // AutoLockOnStartup early silent enforcement (Vector 10A)
            if (appSettings != null && appSettings.AutoLockOnStartup && !TemporaryAccessManager.IsActive)
            {
                bool policyModified = false;
                if (_currentPolicy == null)
                {
                    _currentPolicy = new ProtectionPolicy();
                }
                if (!_currentPolicy.IsAnyProtectionActive())
                {
                    _currentPolicy.BlockRemovableDisks = true;
                    policyModified = true;
                }
                List<string> applyErrors;
                ProtectionEngine.ApplyPolicy(_currentPolicy, null, out applyErrors);
                if (policyModified)
                {
                    SettingsRepository.SavePolicy(_currentPolicy);
                }
                try
                {
                    EventLog.WriteEntry("AutoLockOnStartup enforced early by UsbShieldService.", System.Diagnostics.EventLogEntryType.Information);
                }
                catch { }
            }

            // Wire topology confirmation wake action to wake sentinel worker immediately
            StorageNotificationManager.TopologyWakeAction = delegate { WakeSentinelSweep(); };

            // Start Sentinel loop
            _sentinelThread = new Thread(SentinelWorker);
            _sentinelThread.IsBackground = true;
            _sentinelThread.Start();

            // Start Named Pipe IPC Server
            _serverThread = new Thread(IpcServerWorker);
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        protected override void OnStop()
        {
            _running = false;
            WakeSentinelSweep();

            // Vector 10C: Deterministic Named Pipe shutdown
            try
            {
                var pipe = _activePipe;
                if (pipe != null)
                {
                    pipe.Close();
                }
            }
            catch { }

            try
            {
                using (NamedPipeClientStream dummy = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    dummy.Connect(200);
                }
            }
            catch { }

            try
            {
                if (_serverThread != null && _serverThread.IsAlive)
                {
                    _serverThread.Join(500);
                }
            }
            catch { }
        }

        private void SentinelWorker()
        {
            while (_running)
            {
                try
                {
                    _lastSentinelSweepUtc = DateTime.UtcNow;
                    Interlocked.Increment(ref _sentinelSweepCount);

                    // Dynamically reload active policy from persistent store
                    SettingsRepository.ResetCache();
                    lock (_policyLock)
                    {
                        _currentPolicy = SettingsRepository.LoadPolicy();
                        if (_currentPolicy != null && _currentPolicy.PolicyVersion > _currentPolicyVersion)
                        {
                            _currentPolicyVersion = _currentPolicy.PolicyVersion;
                        }
                    }

                    // Check temporary access expiration and re-lock
                    string expErr;
                    TemporaryAccessManager.CheckAndExpireIfDue(out expErr);
                    bool isTempActive = (TemporaryAccessManager.PeekRemainingSeconds() > 0);
                    if (!isTempActive)
                    {
                        TemporaryAccessState curState = TemporaryAccessManager.LoadState();
                        if (curState != null && curState.Status == TemporaryAccessStatus.Relocked && curState.OriginalDesiredPolicy != null)
                        {
                            lock (_policyLock)
                            {
                                if (_currentPolicy == null || !_currentPolicy.IsAnyProtectionActive())
                                {
                                    _currentPolicy = curState.OriginalDesiredPolicy;
                                }
                            }
                        }
                    }

                    // Evaluate and enforce connected devices
                    ActiveSentinel.EvaluateAndEnforceConnectedDevices(_currentPolicy, isTempActive, "ServiceSweep");
                }
                catch { }

                _wakeEvent.WaitOne(1000); // 1-second sentinel interval or instant wake on event/IPC
            }
        }

        private void IpcServerWorker()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            while (_running)
            {
                try
                {
                    PipeSecurity pipeSecurity = new PipeSecurity();
                    // Restrict to SYSTEM and Administrators
                    SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                    SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

                    pipeSecurity.AddAccessRule(new PipeAccessRule(sidSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
                    pipeSecurity.AddAccessRule(new PipeAccessRule(sidAdmins, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                    using (NamedPipeServerStream pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        4096,
                        4096,
                        pipeSecurity))
                    {
                        _activePipe = pipe;
                        try
                        {
                            pipe.WaitForConnection();
                        }
                        catch
                        {
                            if (!_running) break;
                            throw;
                        }
                        finally
                        {
                            _activePipe = null;
                        }

                        if (!_running) break;

                        using (StreamReader reader = new StreamReader(pipe, Encoding.UTF8))
                        using (StreamWriter writer = new StreamWriter(pipe, Encoding.UTF8))
                        {
                            string requestJson = reader.ReadLine();
                            if (!string.IsNullOrEmpty(requestJson))
                            {
                                string responseJson = ProcessIpcRequest(requestJson, serializer);
                                writer.WriteLine(responseJson);
                                writer.Flush();
                            }
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
        }

        internal string ProcessIpcRequest(string requestJson, JavaScriptSerializer serializer)
        {
            try
            {
                if (string.IsNullOrEmpty(requestJson))
                {
                    return serializer.Serialize(new IpcResponse { Success = false, ErrorMessage = "Invalid empty request." });
                }

                IpcRequest request;
                try
                {
                    request = serializer.Deserialize<IpcRequest>(requestJson);
                }
                catch (Exception ex)
                {
                    return serializer.Serialize(new IpcResponse { Success = false, ErrorMessage = "Malformed request JSON: " + ex.Message });
                }

                IpcResponse response = new IpcResponse { Success = true };

                if (request == null || string.IsNullOrEmpty(request.Command))
                {
                    response.Success = false;
                    response.ErrorMessage = "Invalid empty request command.";
                    return serializer.Serialize(response);
                }

                // Strictly defined operations ONLY
                string cmd = request.Command.Trim().ToUpperInvariant();
                switch (cmd)
                {
                    case "GETHEALTH":
                    case "GET_HEALTH":
                        bool isAlive = _sentinelThread != null && _sentinelThread.IsAlive;
                        double secondsSince = (_lastSentinelSweepUtc == DateTime.MinValue) ? -1 : (DateTime.UtcNow - _lastSentinelSweepUtc).TotalSeconds;
                        bool healthy = _running && isAlive && (secondsSince >= 0 && secondsSince <= 10.0);
                        ServiceHealthReport health = new ServiceHealthReport
                        {
                            IsHealthy = healthy,
                            IsSentinelAlive = isAlive,
                            LastSentinelSweepUtc = _lastSentinelSweepUtc,
                            SecondsSinceLastSweep = secondsSince,
                            SentinelSweepCount = _sentinelSweepCount,
                            CurrentPolicyVersion = _currentPolicyVersion,
                            StatusMessage = healthy ? "Service and ActiveSentinel operating normally." : "ActiveSentinel thread unresponsive or delayed."
                        };
                        response.Data = serializer.Serialize(health);
                        break;

                    case "GETSTATUS":
                    case "GET_STATUS":
                        ProtectionStatusReport report;
                        lock (_policyLock)
                        {
                            report = StateVerifier.Verify(_currentPolicy);
                        }
                        response.Data = serializer.Serialize(report);
                        break;

                    case "GETDEVICES":
                        List<DeviceIdentity> devices = DeviceDiscovery.DiscoverAllDevices();
                        response.Data = serializer.Serialize(devices);
                        break;

                    case "APPLYPOLICY":
                        if (string.IsNullOrEmpty(request.Payload))
                        {
                            response.Success = false;
                            response.ErrorMessage = "Payload cannot be null or empty for APPLYPOLICY.";
                            break;
                        }

                        ProtectionPolicy newPolicy;
                        try
                        {
                            newPolicy = serializer.Deserialize<ProtectionPolicy>(request.Payload);
                        }
                        catch (Exception ex)
                        {
                            response.Success = false;
                            response.ErrorMessage = "Malformed policy JSON: " + ex.Message;
                            break;
                        }

                        if (newPolicy == null)
                        {
                            response.Success = false;
                            response.ErrorMessage = "Deserialized policy is null.";
                            break;
                        }

                        // Semantic validation: Incompatibility guard
                        if (newPolicy.HardBlockUsbStor && newPolicy.WhitelistMode)
                        {
                            response.Success = false;
                            response.ErrorMessage = "Hard driver block (USBSTOR Start=4) is incompatible with Whitelist Mode.";
                            break;
                        }

                        // Version Agreement Guard: Reject invalid versions (< 1)
                        if (newPolicy.PolicyVersion < 1)
                        {
                            response.Success = false;
                            response.ErrorMessage = "Invalid PolicyVersion (must be >= 1).";
                            break;
                        }


                        // Version Agreement Guard: Reject stale policy generations (< current)
                        if (newPolicy.PolicyVersion < _currentPolicyVersion)
                        {
                            response.Success = false;
                            response.ErrorMessage = string.Format("Rejected stale policy generation (version {0} < current {1}).",
                                newPolicy.PolicyVersion, _currentPolicyVersion);
                            break;
                        }

                        // Duplicate Version Handling: Idempotent duplicate vs conflicting duplicate
                        if (newPolicy.PolicyVersion == _currentPolicyVersion && _currentPolicy != null)
                        {
                            if (ProtectionPolicy.AreEquivalent(newPolicy, _currentPolicy))
                            {
                                // Idempotent duplicate: Identical payload already accepted and active
                                response.Success = true;
                                response.Data = "IdempotentDuplicate";
                                break;
                            }
                            else
                            {
                                // Conflicting duplicate: Same generation number with conflicting rules
                                response.Success = false;
                                response.ErrorMessage = string.Format("Rejected conflicting policy with duplicate generation version {0}.",
                                    newPolicy.PolicyVersion);
                                break;
                            }
                        }

                        lock (_policyLock)
                        {
                            // Re-evaluate under lock for atomic serialization
                            if (newPolicy.PolicyVersion < _currentPolicyVersion)
                            {
                                response.Success = false;
                                response.ErrorMessage = string.Format("Rejected stale policy generation (version {0} < current {1}).",
                                    newPolicy.PolicyVersion, _currentPolicyVersion);
                                break;
                            }

                            if (newPolicy.PolicyVersion == _currentPolicyVersion && _currentPolicy != null)
                            {
                                if (ProtectionPolicy.AreEquivalent(newPolicy, _currentPolicy))
                                {
                                    response.Success = true;
                                    response.Data = "IdempotentDuplicate";
                                    break;
                                }
                                else
                                {
                                    response.Success = false;
                                    response.ErrorMessage = string.Format("Rejected conflicting policy with duplicate generation version {0}.",
                                        newPolicy.PolicyVersion);
                                    break;
                                }
                            }

                            List<string> errors;
                            bool ok = ProtectionEngine.ApplyPolicy(newPolicy, _currentPolicy, out errors);
                            response.Success = ok;
                            if (ok)
                            {
                                _currentPolicy = newPolicy;
                                _currentPolicyVersion = newPolicy.PolicyVersion;
                                SettingsRepository.SavePolicy(newPolicy);
                                WakeSentinelSweep();
                            }
                            else
                            {
                                response.ErrorMessage = (errors != null && errors.Count > 0)
                                    ? string.Join("; ", errors.ToArray())
                                    : "ProtectionEngine failed to apply policy.";
                            }
                        }
                        break;

                    case "WAKESENTINEL":
                    case "EVALUATENOW":
                        WakeSentinelSweep();
                        response.Success = true;
                        break;

                    case "SETTEMPORARYACCESS":
                        int minutes = 15;
                        if (!string.IsNullOrEmpty(request.Payload))
                        {
                            int.TryParse(request.Payload, out minutes);
                        }
                        if (minutes <= 0) minutes = 15;
                        string tempErr;
                        lock (_policyLock)
                        {
                            response.Success = TemporaryAccessManager.StartTemporaryAccess(minutes, _currentPolicy, out tempErr);
                            if (response.Success)
                            {
                                _currentPolicy = new ProtectionPolicy();
                                WakeSentinelSweep();
                            }
                        }
                        response.ErrorMessage = tempErr;
                        break;

                    case "CANCELTEMPORARYACCESS":
                        string cancelErr;
                        lock (_policyLock)
                        {
                            response.Success = TemporaryAccessManager.CancelTemporaryAccess(out cancelErr);
                            if (response.Success)
                            {
                                TemporaryAccessState curState = TemporaryAccessManager.LoadState();
                                if (curState != null && curState.OriginalDesiredPolicy != null)
                                {
                                    _currentPolicy = curState.OriginalDesiredPolicy;
                                }
                                WakeSentinelSweep();
                            }
                        }
                        response.ErrorMessage = cancelErr;
                        break;

                    case "RESTOREPRESHIELDSTATE":
                        lock (_policyLock)
                        {
                            List<string> restoreErrors;
                            response.Success = ProtectionEngine.RestoreBaseline(out restoreErrors);
                            response.ErrorMessage = (restoreErrors != null && restoreErrors.Count > 0)
                                ? string.Join("; ", restoreErrors.ToArray())
                                : null;
                            if (response.Success)
                            {
                                _currentPolicy = new ProtectionPolicy();
                                SettingsRepository.SavePolicy(_currentPolicy);
                                WakeSentinelSweep();
                            }
                        }
                        break;

                    default:
                        response.Success = false;
                        response.ErrorMessage = "Unrecognized or prohibited command.";
                        break;
                }

                return serializer.Serialize(response);
            }
            catch (Exception ex)
            {
                return serializer.Serialize(new IpcResponse { Success = false, ErrorMessage = ex.Message });
            }
        }
    }

    public class IpcRequest
    {
        public string Command { get; set; }
        public string Payload { get; set; }
    }

    public class IpcResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Data { get; set; }
    }

    public class ServiceHealthReport
    {
        public bool IsHealthy { get; set; }
        public bool IsSentinelAlive { get; set; }
        public DateTime LastSentinelSweepUtc { get; set; }
        public double SecondsSinceLastSweep { get; set; }
        public int SentinelSweepCount { get; set; }
        public int CurrentPolicyVersion { get; set; }
        public string StatusMessage { get; set; }

        public ServiceHealthReport()
        {
            IsHealthy = false;
            LastSentinelSweepUtc = DateTime.MinValue;
            SecondsSinceLastSweep = -1;
            StatusMessage = "Uninitialized";
        }
    }
}
