// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Secure Password Authentication Engine (PBKDF2)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Security
{
    /// <summary>
    /// Implements PBKDF2-HMAC-SHA256 password hashing with 100,000 iterations,
    /// constant-time verification, and DPAPI-protected machine storage.
    /// INVARIANT: Safe replacement retains previous verifier until replacement is validated.
    /// INVARIANT: Fail-closed; IO or ACL error never treats authentication as optional.
    /// Invariant: Sensitive byte arrays are zeroed after use.
    /// </summary>
    public static class PasswordManager
    {
        private const int SaltSizeBytes = 32;
        private const int HashSizeBytes = 32;
        private const int DefaultIterations = 100000;
        private static readonly object _syncLock = new object();

        private static Func<string, string> _faultInjectionSaveSeam = null;

        public static Func<string, string> FaultInjectionSaveSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionSaveSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionSaveSeam = null; return; } _faultInjectionSaveSeam = value; }
        }

        public static void ResetSeams()
        {
            _faultInjectionSaveSeam = null;
        }

        public static string GetAuthFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".auth");
        }

        public static string GetAuthBackupFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".auth.bak");
        }

        public static string GetAuthTempFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".auth.tmp");
        }

        /// <summary>
        /// Checks whether password authentication is configured.
        /// INVARIANT: Fail-closed. If file existence check throws an unexpected error,
        /// returns true to prevent fail-open bypass of authentication guards.
        /// </summary>
        public static bool IsPasswordConfigured()
        {
            try
            {
                string path = GetAuthFilePath();
                string bak = GetAuthBackupFilePath();
                bool primaryExists = File.Exists(path) && new FileInfo(path).Length > 0;
                bool bakExists = File.Exists(bak) && new FileInfo(bak).Length > 0;
                return primaryExists || bakExists;
            }
            catch
            {
                // Fail-closed security invariant:
                // If file inspection fails due to ACL or I/O error, NEVER assume auth is optional!
                return true;
            }
        }

        public static bool IsPasswordEnabled()
        {
            return IsPasswordConfigured();
        }

        /// <summary>
        /// Safely sets or updates the master application password.
        /// Uses atomic replacement with backup preservation to eliminate the delete-before-move failure gap.
        /// </summary>
        public static bool SetPassword(string password, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(password))
            {
                error = "Password cannot be empty.";
                return false;
            }

            if (FaultInjectionSaveSeam != null)
            {
                string fiErr = FaultInjectionSaveSeam(password);
                if (!string.IsNullOrEmpty(fiErr))
                {
                    error = fiErr;
                    return false;
                }
            }

            lock (_syncLock)
            {
                byte[] salt = new byte[SaltSizeBytes];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(salt);
                }

                byte[] derivedHash = null;
                byte[] plainBytes = null;
                try
                {
                    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, DefaultIterations, HashAlgorithmName.SHA256))
                    {
                        derivedHash = pbkdf2.GetBytes(HashSizeBytes);
                    }

                    // Format: VERSION$ALGORITHM$ITERATIONS$SALT_B64$HASH_B64
                    string record = string.Format("V2$PBKDF2-SHA256${0}${1}${2}",
                        DefaultIterations,
                        Convert.ToBase64String(salt),
                        Convert.ToBase64String(derivedHash));

                    plainBytes = Encoding.UTF8.GetBytes(record);
                    byte[] encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

                    string path = GetAuthFilePath();
                    string bak = GetAuthBackupFilePath();
                    string temp = GetAuthTempFilePath();
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        SecurityAclHelper.EnsureProgramDataAcls();
                    }

                    // 1. Write encrypted verifier to temp file with sector flush
                    using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.Write(encrypted, 0, encrypted.Length);
                        fs.Flush(true);
                    }

                    // 2. Atomic replacement with backup preservation
                    if (File.Exists(path))
                    {
                        try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                        try { if (File.Exists(bak)) File.SetAttributes(bak, FileAttributes.Normal); } catch { }

                        try
                        {
                            File.Replace(temp, path, bak, true);
                        }
                        catch
                        {
                            // Preserve backup before delete/move fallback
                            try { File.Copy(path, bak, true); } catch { }
                            try { File.Delete(path); } catch { }
                            File.Move(temp, path);
                        }
                    }
                    else
                    {
                        File.Move(temp, path);
                    }

                    // 3. Mark hidden and enforce security ACLs
                    try { File.SetAttributes(path, FileAttributes.Hidden); } catch { }
                    if (File.Exists(bak))
                    {
                        try { File.SetAttributes(bak, FileAttributes.Hidden); } catch { }
                    }

                    if (string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(path);
                        if (File.Exists(bak))
                        {
                            SecurityAclHelper.ApplySensitiveFileSecurity(bak);
                        }
                    }

                    // 4. Verify file was written and is readable
                    if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    {
                        error = "Password verifier file could not be verified on disk.";
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    try { if (File.Exists(GetAuthTempFilePath())) File.Delete(GetAuthTempFilePath()); } catch { }
                    return false;
                }
                finally
                {
                    if (plainBytes != null) Array.Clear(plainBytes, 0, plainBytes.Length);
                    if (derivedHash != null) Array.Clear(derivedHash, 0, derivedHash.Length);
                    Array.Clear(salt, 0, salt.Length);
                }
            }
        }

        public static void SetPassword(string password)
        {
            string err;
            if (!SetPassword(password, out err))
            {
                throw new InvalidOperationException(err ?? "Failed to set password.");
            }
        }

        public static bool RemovePassword()
        {
            lock (_syncLock)
            {
                try
                {
                    string path = GetAuthFilePath();
                    string bak = GetAuthBackupFilePath();
                    if (File.Exists(path))
                    {
                        try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                        File.Delete(path);
                    }
                    if (File.Exists(bak))
                    {
                        try { File.SetAttributes(bak, FileAttributes.Normal); } catch { }
                        File.Delete(bak);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool VerifyPasswordAgainstFile(string path, string inputPassword)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(inputPassword))
                return false;

            byte[] salt = null;
            byte[] storedHash = null;
            byte[] computedHash = null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                if (encrypted == null || encrypted.Length == 0) return false;

                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
                string record = Encoding.UTF8.GetString(decrypted);
                Array.Clear(decrypted, 0, decrypted.Length);

                string[] parts = record.Split('$');
                if (parts.Length < 5) return false;

                int iterations = int.Parse(parts[2]);
                salt = Convert.FromBase64String(parts[3]);
                storedHash = Convert.FromBase64String(parts[4]);

                using (var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, iterations, HashAlgorithmName.SHA256))
                {
                    computedHash = pbkdf2.GetBytes(storedHash.Length);
                }

                return ConstantTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (salt != null) Array.Clear(salt, 0, salt.Length);
                if (storedHash != null) Array.Clear(storedHash, 0, storedHash.Length);
                if (computedHash != null) Array.Clear(computedHash, 0, computedHash.Length);
            }
        }

        public static bool VerifyPassword(string inputPassword)
        {
            if (string.IsNullOrEmpty(inputPassword)) return false;

            lock (_syncLock)
            {
                string path = GetAuthFilePath();
                string bak = GetAuthBackupFilePath();

                // 1. Try primary authentication verifier
                if (VerifyPasswordAgainstFile(path, inputPassword))
                {
                    return true;
                }

                // 2. If primary failed or was corrupted/missing, try backup verifier
                if (File.Exists(bak) && VerifyPasswordAgainstFile(bak, inputPassword))
                {
                    // Cleanly heal primary from verified backup
                    try
                    {
                        File.Copy(bak, path, true);
                        try { File.SetAttributes(path, FileAttributes.Hidden); } catch { }
                        if (string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                        {
                            SecurityAclHelper.ApplySensitiveFileSecurity(path);
                        }
                    }
                    catch { }
                    return true;
                }

                return false;
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
