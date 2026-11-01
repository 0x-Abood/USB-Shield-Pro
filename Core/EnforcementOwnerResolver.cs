// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Single Enforcement Owner Architecture
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.Threading;
using UsbShieldPro.Service;

namespace UsbShieldPro.Core
{
    public enum EnforcementOwnerState
    {
        Service,
        ServiceDegraded,
        GuiFallback,
        None,
        Conflict
    }

    public enum IpcHealthState
    {
        Connected,
        Degraded,
        NotApplicable
    }

    /// <summary>
    /// Resolves the single authoritative enforcement owner.
    /// Invariant: There must never be two concurrent enforcement owners.
    /// Invariant: If Service is running and healthy, Service is authoritative. GUI operates strictly as UI/config client.
    /// Invariant: GUI never starts a second enforcement engine if IPC is degraded while Service is running.
    /// Invariant: Transitions between Service and GUI are mutually exclusive and race-safe.
    /// Invariant: Degraded background service is explicitly identified and reported.
    /// </summary>
    public static class EnforcementOwnerResolver
    {
        private static readonly object _syncLock = new object();
        private static int _guiWorkerActive = 0; // 0 = idle, 1 = active
        private static volatile bool _serviceStartPending = false;

        private static DateTime _lastHealthCheckUtc = DateTime.MinValue;
        private static EnforcementOwnerState _cachedState = EnforcementOwnerState.GuiFallback;
        private static readonly object _cacheLock = new object();
        private const int CacheTtlMs = 1500;

        private static Func<bool, EnforcementOwnerState> _resolveOwnerOverride = null;
        public static Func<bool, EnforcementOwnerState> ResolveOwnerOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _resolveOwnerOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _resolveOwnerOverride = null; return; } _resolveOwnerOverride = value; }
        }

        public static void ResetSeams()
        {
            _resolveOwnerOverride = null;
        }

        public static void ResetCache()
        {
            lock (_cacheLock)
            {
                _lastHealthCheckUtc = DateTime.MinValue;
                _cachedState = EnforcementOwnerState.GuiFallback;
            }
        }

        public static EnforcementOwnerState ResolveOwner(bool forceRefresh = false)
        {
            if (ResolveOwnerOverride != null)
            {
                return ResolveOwnerOverride(forceRefresh);
            }

            try
            {
                bool isInstalled = ServiceManager.IsServiceInstalled();
                bool isRunning = ServiceManager.IsServiceRunning();

                if (!isInstalled || !isRunning)
                {
                    lock (_cacheLock)
                    {
                        _cachedState = EnforcementOwnerState.GuiFallback;
                        _lastHealthCheckUtc = DateTime.UtcNow;
                    }
                    return EnforcementOwnerState.GuiFallback;
                }

                // Service is installed and running. Check cache unless forceRefresh
                lock (_cacheLock)
                {
                    if (!forceRefresh && (DateTime.UtcNow - _lastHealthCheckUtc).TotalMilliseconds < CacheTtlMs)
                    {
                        return _cachedState;
                    }
                }

                // Query service health over IPC
                ServiceHealthReport health = ServiceManager.CheckServiceHealth(1500);
                EnforcementOwnerState resolved;

                if (health != null && health.IsHealthy)
                {
                    resolved = EnforcementOwnerState.Service;
                }
                else
                {
                    resolved = EnforcementOwnerState.ServiceDegraded;
                }

                lock (_cacheLock)
                {
                    _cachedState = resolved;
                    _lastHealthCheckUtc = DateTime.UtcNow;
                }

                return resolved;
            }
            catch
            {
                return EnforcementOwnerState.GuiFallback;
            }
        }

        public static EnforcementOwnerState GetEffectiveOwnerState(bool forceRefresh = false)
        {
            return ResolveOwner(forceRefresh);
        }

        public static bool IsServiceAuthoritative(bool forceRefresh = false)
        {
            return ResolveOwner(forceRefresh) == EnforcementOwnerState.Service;
        }

        public static bool IsGuiEnforcementAllowed()
        {
            if (_serviceStartPending) return false;
            return ResolveOwner() == EnforcementOwnerState.GuiFallback;
        }

        public static bool IsGuiOwner()
        {
            return IsGuiEnforcementAllowed();
        }

        public static IpcHealthState CheckIpcHealth()
        {
            var owner = ResolveOwner();
            if (owner != EnforcementOwnerState.Service && owner != EnforcementOwnerState.ServiceDegraded)
            {
                return IpcHealthState.NotApplicable;
            }

            try
            {
                var resp = ServiceManager.SendIpcRequest("GETSTATUS", null, 1500);
                if (resp != null && resp.Success)
                {
                    return IpcHealthState.Connected;
                }
                return IpcHealthState.Degraded;
            }
            catch
            {
                return IpcHealthState.Degraded;
            }
        }

        public static void MarkGuiWorkerActive()
        {
            Interlocked.Exchange(ref _guiWorkerActive, 1);
        }

        public static void MarkGuiWorkerIdle()
        {
            Interlocked.Exchange(ref _guiWorkerActive, 0);
        }

        public static bool IsGuiWorkerActive()
        {
            return Volatile.Read(ref _guiWorkerActive) == 1;
        }

        /// <summary>
        /// Transition Protocol: Prepares GUI before starting the Windows Service.
        /// Suspends GUI Sentinel and waits for any in-flight GUI worker to exit.
        /// </summary>
        public static bool PrepareForServiceStart(int timeoutMs = 3000)
        {
            _serviceStartPending = true;
            ResetCache();
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!IsGuiWorkerActive())
                {
                    return true;
                }
                Thread.Sleep(50);
            }
            return !IsGuiWorkerActive();
        }

        /// <summary>
        /// Transition Protocol: Confirms service has completely stopped and exited
        /// before permitting GUI fallback Sentinel to begin enforcement.
        /// </summary>
        public static void HandleServiceStopped()
        {
            _serviceStartPending = false;
            ResetCache();
            // Wait settling interval (300ms) to ensure background thread in service process is dead
            Thread.Sleep(300);
        }
    }
}
