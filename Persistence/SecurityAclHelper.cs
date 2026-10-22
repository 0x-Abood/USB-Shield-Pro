// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Security ACL & Storage Directory Helper
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace UsbShieldPro.Persistence
{
    /// <summary>
    /// Ensures machine-level state directories are created and protected
    /// with restrictive NTFS Access Control Lists (ACLs) and proper ownership.
    /// Standard users cannot tamper with journal, auth, recovery, or trusted device files.
    /// Invariant: Sensitive files must be owned by BUILTIN\Administrators (or approved SYSTEM).
    /// </summary>
    public static class SecurityAclHelper
    {
        public static string BaseDirectoryOverride { get; set; }

        public static void ResetSeams()
        {
        }

        #region Token Privilege P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        public static bool EnablePrivilege(string privilegeName)
        {
            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                    return false;

                LUID luid;
                if (!LookupPrivilegeValue(null, privilegeName, out luid))
                    return false;

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Privileges.Luid = luid;
                tp.Privileges.Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    return false;

                return Marshal.GetLastWin32Error() == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hToken != IntPtr.Zero)
                    CloseHandle(hToken);
            }
        }

        public static bool IsRunningElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        public static string GetProgramDataDirectory()
        {
            if (!string.IsNullOrEmpty(BaseDirectoryOverride))
            {
                if (!Directory.Exists(BaseDirectoryOverride))
                {
                    try { Directory.CreateDirectory(BaseDirectoryOverride); } catch { }
                }
                return BaseDirectoryOverride;
            }

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string targetDir = Path.Combine(programData, "USBShieldPro");

            if (!Directory.Exists(targetDir))
            {
                try
                {
                    Directory.CreateDirectory(targetDir);
                    ApplyRestrictiveAcl(targetDir);
                }
                catch { }
            }

            return targetDir;
        }

        public static void EnsureProgramDataAcls()
        {
            string err;
            if (!ApplyProgramDataDirectorySecurity(GetProgramDataDirectory(), out err))
            {
                System.Diagnostics.Trace.WriteLine("Failed to ensure ProgramData ACLs: " + err);
            }
        }

        public static void ApplyRestrictiveAcl(string directoryPath)
        {
            string err;
            ApplyProgramDataDirectorySecurity(directoryPath, out err);
        }

        public static bool ApplyProgramDataDirectorySecurity(string directoryPath, out string error)
        {
            error = null;
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                DirectoryInfo dInfo = new DirectoryInfo(directoryPath);

                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier sidUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

                // 1. Decouple DACL from Owner so DACL protection is guaranteed to apply
                DirectorySecurity dSecurity = dInfo.GetAccessControl(AccessControlSections.Access);

                // Block inheritance
                dSecurity.SetAccessRuleProtection(true, false);

                // Purge existing rules (removes non-admin user-specific ACEs)
                foreach (FileSystemAccessRule r in dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    dSecurity.RemoveAccessRule(r);
                }

                // Grant SYSTEM Full Control (inheritable to children)
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidSystem,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // Grant BUILTIN\Administrators Full Control (inheritable to children)
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidAdmins,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // Grant Authenticated Users ONLY traversal on directory container itself (NO INHERIT to child files)
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidUsers,
                    FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // Apply DACL directly
                dInfo.SetAccessControl(dSecurity);

                // 2. Set Owner and Group to BUILTIN\Administrators
                try
                {
                    DirectorySecurity ownerSecurity = dInfo.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group);
                    ownerSecurity.SetOwner(sidAdmins);
                    ownerSecurity.SetGroup(sidAdmins);
                    dInfo.SetAccessControl(ownerSecurity);
                }
                catch (Exception ex)
                {
                    if (IsRunningElevated())
                    {
                        error = "Failed to set directory owner to BUILTIN\\Administrators: " + ex.Message;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool VerifyProgramDataDirectorySecurity(string directoryPath, out string failureReason)
        {
            failureReason = null;
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    failureReason = "Directory does not exist.";
                    return false;
                }

                DirectoryInfo dInfo = new DirectoryInfo(directoryPath);
                DirectorySecurity dSecurity = dInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);

                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier sidUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

                // 1. OWNER VERIFICATION (Strict Gate)
                SecurityIdentifier ownerSid = dSecurity.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                if (ownerSid == null)
                {
                    failureReason = "Unable to determine directory owner SID.";
                    return false;
                }

                // If owner is not Administrators or SYSTEM: verification must FAIL.
                if (ownerSid != sidAdmins && ownerSid != sidSystem)
                {
                    string ownerName = ownerSid.Value;
                    try { ownerName = dSecurity.GetOwner(typeof(NTAccount)).Value; } catch { }
                    failureReason = string.Format("Directory owner is not BUILTIN\\Administrators or SYSTEM (Current Owner: {0}, SID: {1}).",
                        ownerName, ownerSid.Value);
                    return false;
                }

                // For production ProgramData root specifically, Owner MUST be BUILTIN\Administrators
                string prodDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "USBShieldPro");
                if (string.Equals(Path.GetFullPath(directoryPath), Path.GetFullPath(prodDir), StringComparison.OrdinalIgnoreCase))
                {
                    if (ownerSid != sidAdmins)
                    {
                        string ownerName = ownerSid.Value;
                        try { ownerName = dSecurity.GetOwner(typeof(NTAccount)).Value; } catch { }
                        failureReason = string.Format("Production ProgramData root owner must be BUILTIN\\Administrators (Current Owner: {0}, SID: {1}).",
                            ownerName, ownerSid.Value);
                        return false;
                    }
                }

                // 2. INHERITANCE VERIFICATION
                if (!dSecurity.AreAccessRulesProtected)
                {
                    failureReason = "Inheritance is enabled (expected protected).";
                    return false;
                }

                // 3. DACL PERMISSIONS VERIFICATION
                bool hasSystem = false;
                bool hasAdmins = false;
                bool hasUsersRestricted = false;

                foreach (FileSystemAccessRule rule in dSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    SecurityIdentifier sid = rule.IdentityReference as SecurityIdentifier;
                    if (sid == null) continue;

                    if (sid == sidSystem)
                    {
                        if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl &&
                            (rule.InheritanceFlags & (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)) == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit))
                        {
                            hasSystem = true;
                        }
                    }
                    else if (sid == sidAdmins)
                    {
                        if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl &&
                            (rule.InheritanceFlags & (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)) == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit))
                        {
                            hasAdmins = true;
                        }
                    }
                    else if (sid == sidUsers)
                    {
                        FileSystemRights dangerous = FileSystemRights.Write | FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories |
                                                     FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
                                                     FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
                        if ((rule.FileSystemRights & dangerous) != 0)
                        {
                            failureReason = "Authenticated Users has write/delete permissions: " + rule.FileSystemRights;
                            return false;
                        }
                        if (rule.InheritanceFlags == InheritanceFlags.None)
                        {
                            hasUsersRestricted = true;
                        }
                    }
                    else
                    {
                        FileSystemRights dangerous = FileSystemRights.Write | FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories |
                                                     FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
                                                     FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
                        if ((rule.FileSystemRights & dangerous) != 0)
                        {
                            failureReason = "Unsafe principal found with write/delete permissions: " + rule.IdentityReference.Value;
                            return false;
                        }
                    }
                }

                if (!hasSystem) { failureReason = "Missing SYSTEM FullControl."; return false; }
                if (!hasAdmins) { failureReason = "Missing BUILTIN\\Administrators FullControl."; return false; }
                if (!hasUsersRestricted) { failureReason = "Missing restricted Authenticated Users traversal rule."; return false; }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = "Verification exception: " + ex.Message;
                return false;
            }
        }

        public static bool VerifySensitiveFileSecurity(string filePath, out string failureReason)
        {
            failureReason = null;
            try
            {
                if (!File.Exists(filePath))
                {
                    failureReason = "File does not exist: " + filePath;
                    return false;
                }

                FileInfo fInfo = new FileInfo(filePath);
                FileSecurity fSecurity = fInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                // 1. Check Owner
                SecurityIdentifier ownerSid = fSecurity.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                if (ownerSid == null)
                {
                    failureReason = "Unable to resolve file owner SID.";
                    return false;
                }

                if (ownerSid != sidAdmins && ownerSid != sidSystem)
                {
                    string ownerName = ownerSid.Value;
                    try { ownerName = fSecurity.GetOwner(typeof(NTAccount)).Value; } catch { }
                    failureReason = string.Format("File owner is not BUILTIN\\Administrators or SYSTEM (Current Owner: {0}, SID: {1}).",
                        ownerName, ownerSid.Value);
                    return false;
                }

                // 2. Check Inheritance
                if (!fSecurity.AreAccessRulesProtected)
                {
                    failureReason = "File inheritance is enabled (expected protected).";
                    return false;
                }

                // 3. Check DACL
                bool hasSystem = false;
                bool hasAdmins = false;

                foreach (FileSystemAccessRule rule in fSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    SecurityIdentifier sid = rule.IdentityReference as SecurityIdentifier;
                    if (sid == null) continue;

                    if (sid == sidSystem)
                    {
                        if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                            hasSystem = true;
                    }
                    else if (sid == sidAdmins)
                    {
                        if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                            hasAdmins = true;
                    }
                    else
                    {
                        FileSystemRights dangerous = FileSystemRights.Write | FileSystemRights.CreateFiles |
                                                     FileSystemRights.Delete | FileSystemRights.ChangePermissions |
                                                     FileSystemRights.TakeOwnership;
                        if ((rule.FileSystemRights & dangerous) != 0)
                        {
                            failureReason = "File grants write/delete/tamper access to unauthorized principal: " + rule.IdentityReference.Value;
                            return false;
                        }
                    }
                }

                if (!hasSystem) { failureReason = "Missing SYSTEM FullControl on file."; return false; }
                if (!hasAdmins) { failureReason = "Missing BUILTIN\\Administrators FullControl on file."; return false; }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = "File verification exception: " + ex.Message;
                return false;
            }
        }

        public static bool EnsureAllOwnershipAndAclsElevated(out string error)
        {
            error = null;
            try
            {
                if (!IsRunningElevated())
                {
                    error = "Process is not elevated (requires Administrator privileges).";
                    return false;
                }

                // Enable Windows security privileges in elevated token
                EnablePrivilege("SeTakeOwnershipPrivilege");
                EnablePrivilege("SeRestorePrivilege");
                EnablePrivilege("SeSecurityPrivilege");

                string dir = GetProgramDataDirectory();
                if (!ApplyProgramDataDirectorySecurity(dir, out error))
                {
                    return false;
                }

                // Sensitive child files to enforce ownership and DACL
                string[] sensitiveFiles = new string[]
                {
                    Path.Combine(dir, "trusted_devices.json"),
                    Path.Combine(dir, "trusted_devices.json.bak"),
                    Path.Combine(dir, "settings.json"),
                    Path.Combine(dir, "journal.json"),
                    Path.Combine(dir, "journal.bak"),
                    Path.Combine(dir, ".auth"),
                    Path.Combine(dir, ".recovery"),
                    Path.Combine(dir, "temp_access.json")
                };

                foreach (string file in sensitiveFiles)
                {
                    if (File.Exists(file))
                    {
                        ApplySensitiveFileSecurity(file);
                    }
                }

                // Recovery scripts (allow Authenticated Users ReadAndExecute)


                // Verification phase
                string verifyReason;
                if (!VerifyProgramDataDirectorySecurity(dir, out verifyReason))
                {
                    error = "Root directory security verification failed: " + verifyReason;
                    return false;
                }

                string primary = Path.Combine(dir, "trusted_devices.json");
                if (File.Exists(primary))
                {
                    if (!VerifySensitiveFileSecurity(primary, out verifyReason))
                    {
                        error = "Primary trusted devices repository security verification failed: " + verifyReason;
                        return false;
                    }
                }

                string backup = Path.Combine(dir, "trusted_devices.json.bak");
                if (File.Exists(backup))
                {
                    if (!VerifySensitiveFileSecurity(backup, out verifyReason))
                    {
                        error = "Backup trusted devices repository security verification failed: " + verifyReason;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "EnsureAllOwnershipAndAclsElevated exception: " + ex.Message;
                return false;
            }
        }

        public static void ApplySensitiveFileSecurity(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                FileInfo fInfo = new FileInfo(filePath);
                
                // 1. Decouple DACL from Owner so DACL protection is guaranteed to apply
                FileSecurity daclSecurity = fInfo.GetAccessControl(AccessControlSections.Access);
                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                daclSecurity.SetAccessRuleProtection(true, false);
                foreach (FileSystemAccessRule r in daclSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    daclSecurity.RemoveAccessRule(r);
                }

                daclSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidSystem,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                daclSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidAdmins,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                fInfo.SetAccessControl(daclSecurity);

                // 2. Set Owner to BUILTIN\Administrators
                try
                {
                    FileSecurity ownerSecurity = fInfo.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group);
                    ownerSecurity.SetOwner(sidAdmins);
                    ownerSecurity.SetGroup(sidAdmins);
                    fInfo.SetAccessControl(ownerSecurity);
                }
                catch { }
            }
            catch { }
        }

        public static void ApplyRecoveryScriptSecurity(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                FileInfo fInfo = new FileInfo(filePath);

                FileSecurity daclSecurity = fInfo.GetAccessControl(AccessControlSections.Access);
                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier sidUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

                daclSecurity.SetAccessRuleProtection(true, false);
                foreach (FileSystemAccessRule r in daclSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    daclSecurity.RemoveAccessRule(r);
                }

                daclSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidSystem,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                daclSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidAdmins,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                daclSecurity.AddAccessRule(new FileSystemAccessRule(
                    sidUsers,
                    FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                    AccessControlType.Allow));

                fInfo.SetAccessControl(daclSecurity);

                try
                {
                    FileSecurity ownerSecurity = fInfo.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group);
                    ownerSecurity.SetOwner(sidAdmins);
                    ownerSecurity.SetGroup(sidAdmins);
                    fInfo.SetAccessControl(ownerSecurity);
                }
                catch { }
            }
            catch { }
        }
    }
}
