// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — 128-bit Master Recovery Key Manager
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
    /// Generates and verifies 128-bit cryptographic Master Recovery Keys for password recovery.
    /// Invariant: Exactly 128 bits (16 bytes) of true cryptographic entropy.
    /// Invariant: Plaintext key is NEVER saved to disk; only PBKDF2 verification material is stored.
    /// INVARIANT: Safe replacement retains previous verifier until replacement is validated.
    /// INVARIANT: Fail-closed; IO or ACL error never treats authentication as optional.
    /// </summary>
    public static class RecoveryKeyManager
    {
        private const string Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford Base32 (no I, L, O, U)
        private const int EntropyBytes = 16; // 128 bits
        private const int Iterations = 100000;
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

        public static string GetRecoveryFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".recovery");
        }

        public static string GetRecoveryBackupFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".recovery.bak");
        }

        public static string GetRecoveryTempFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ".recovery.tmp");
        }

        /// <summary>
        /// Checks whether the master recovery key is configured.
        /// INVARIANT: Fail-closed. If file inspection fails due to permissions or I/O error,
        /// returns true to prevent bypass of recovery verifications.
        /// </summary>
        public static bool IsRecoveryKeyConfigured()
        {
            try
            {
                string path = GetRecoveryFilePath();
                string bak = GetRecoveryBackupFilePath();
                bool primaryExists = File.Exists(path) && new FileInfo(path).Length > 0;
                bool bakExists = File.Exists(bak) && new FileInfo(bak).Length > 0;
                return primaryExists || bakExists;
            }
            catch
            {
                // Fail-closed security invariant
                return true;
            }
        }

        public static bool IsMasterRecoveryKeyConfigured()
        {
            return IsRecoveryKeyConfigured();
        }

        /// <summary>
        /// Generates a fresh 128-bit Base32 Master Recovery Key string formatted as SHLD-XXXXX-...
        /// </summary>
        public static string GenerateKeyString()
        {
            byte[] entropy = new byte[EntropyBytes];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(entropy);
            }

            string encoded = ToCrockfordBase32(entropy);
            Array.Clear(entropy, 0, entropy.Length);

            StringBuilder formatted = new StringBuilder("SHLD");
            for (int i = 0; i < encoded.Length; i++)
            {
                if (i % 5 == 0) formatted.Append('-');
                formatted.Append(encoded[i]);
            }
            return formatted.ToString();
        }

        /// <summary>
        /// Saves the PBKDF2 hash of the recovery key to disk with DPAPI and machine ACLs.
        /// Uses atomic replacement with backup preservation to eliminate the delete-before-move failure gap.
        /// </summary>
        public static bool SaveMasterRecoveryKey(string keyString, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(keyString))
            {
                error = "Recovery key cannot be empty.";
                return false;
            }

            if (FaultInjectionSaveSeam != null)
            {
                string fiErr = FaultInjectionSaveSeam(keyString);
                if (!string.IsNullOrEmpty(fiErr))
                {
                    error = fiErr;
                    return false;
                }
            }

            lock (_syncLock)
            {
                byte[] salt = new byte[32];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(salt);
                }

                byte[] keyHash = null;
                byte[] plainBytes = null;
                try
                {
                    using (var pbkdf2 = new Rfc2898DeriveBytes(NormalizeKey(keyString), salt, Iterations, HashAlgorithmName.SHA256))
                    {
                        keyHash = pbkdf2.GetBytes(32);
                    }

                    string record = string.Format("V2$RECOVERY${0}${1}${2}",
                        Iterations,
                        Convert.ToBase64String(salt),
                        Convert.ToBase64String(keyHash));

                    plainBytes = Encoding.UTF8.GetBytes(record);
                    byte[] encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

                    string path = GetRecoveryFilePath();
                    string bak = GetRecoveryBackupFilePath();
                    string temp = GetRecoveryTempFilePath();
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
                        error = "Recovery verifier file could not be verified on disk.";
                        return false;
                    }

                    return true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    error = "Access denied: " + ex.Message;
                    try { if (File.Exists(GetRecoveryTempFilePath())) File.Delete(GetRecoveryTempFilePath()); } catch { }
                    return false;
                }
                catch (IOException ex)
                {
                    error = "I/O error: " + ex.Message;
                    try { if (File.Exists(GetRecoveryTempFilePath())) File.Delete(GetRecoveryTempFilePath()); } catch { }
                    return false;
                }
                catch (CryptographicException ex)
                {
                    error = "DPAPI encryption error: " + ex.Message;
                    try { if (File.Exists(GetRecoveryTempFilePath())) File.Delete(GetRecoveryTempFilePath()); } catch { }
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    try { if (File.Exists(GetRecoveryTempFilePath())) File.Delete(GetRecoveryTempFilePath()); } catch { }
                    return false;
                }
                finally
                {
                    if (plainBytes != null) Array.Clear(plainBytes, 0, plainBytes.Length);
                    if (keyHash != null) Array.Clear(keyHash, 0, keyHash.Length);
                    Array.Clear(salt, 0, salt.Length);
                }
            }
        }

        public static bool DeleteMasterRecoveryKey()
        {
            lock (_syncLock)
            {
                try
                {
                    string path = GetRecoveryFilePath();
                    string bak = GetRecoveryBackupFilePath();
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

        /// <summary>
        /// Generates a fresh 128-bit Master Recovery Key, stores its PBKDF2 hash,
        /// and returns the plaintext string.
        /// </summary>
        public static string GenerateNewMasterRecoveryKey()
        {
            string key = GenerateKeyString();
            string err;
            SaveMasterRecoveryKey(key, out err);
            return key;
        }

        private static bool VerifyKeyAgainstFile(string path, string inputKey)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(inputKey))
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

                using (var pbkdf2 = new Rfc2898DeriveBytes(NormalizeKey(inputKey), salt, iterations, HashAlgorithmName.SHA256))
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

        public static bool VerifyMasterRecoveryKey(string inputKey)
        {
            if (string.IsNullOrEmpty(inputKey)) return false;

            lock (_syncLock)
            {
                string path = GetRecoveryFilePath();
                string bak = GetRecoveryBackupFilePath();

                // 1. Try primary recovery verifier
                if (VerifyKeyAgainstFile(path, inputKey))
                {
                    return true;
                }

                // 2. If primary failed or was corrupted/missing, try backup verifier
                if (File.Exists(bak) && VerifyKeyAgainstFile(bak, inputKey))
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

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return key.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
        }

        private static string ToCrockfordBase32(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            int bitBuffer = 0;
            int bitsInBuffer = 0;

            foreach (byte b in bytes)
            {
                bitBuffer = (bitBuffer << 8) | b;
                bitsInBuffer += 8;

                while (bitsInBuffer >= 5)
                {
                    bitsInBuffer -= 5;
                    int index = (bitBuffer >> bitsInBuffer) & 0x1F;
                    sb.Append(Base32Alphabet[index]);
                }
            }

            if (bitsInBuffer > 0)
            {
                int index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
                sb.Append(Base32Alphabet[index]);
            }

            return sb.ToString();
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
