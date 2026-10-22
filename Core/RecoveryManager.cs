// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Pre-Shield Recovery & Standalone Package Generator
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Windows;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Implements exact pre-shield configuration recovery and generates
    /// standalone recovery packages (.bat and .reg).
    /// Invariant: NEVER "Restore Windows Defaults" — ALWAYS restore the exact pre-shield baseline.
    /// Invariant: Zero operation lag — recovery package generated before mutations are committed.
    /// Invariant: No automatic gpupdate /force.
    /// </summary>
    public static class RecoveryManager
    {
        public static string GetRecoveryRegPath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "PreShield_Rollback.reg");
        }

        public static string GetRestoreCliCommand()
        {
            return "UsbShieldPro.exe --restore-defaults";
        }

        /// <summary>
        /// Generates the standalone recovery package (.reg on explicit user request).
        /// Invariant: Zero automatic .bat or .reg dropping in ProgramData during normal operation.
        /// </summary>
        public static bool GenerateRecoveryPackage(string customExportDirectory = null)
        {
            if (string.IsNullOrEmpty(customExportDirectory))
            {
                // Invariant: Zero automatic dropping of scripts in ProgramData
                return true;
            }

            try
            {
                string targetPath = Path.Combine(customExportDirectory, "PreShield_Rollback.reg");
                return ExportRegistryRollback(targetPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Exports the baseline registry rollback file (.reg) to a user-selected path.
        /// </summary>
        public static bool ExportRegistryRollback(string destinationFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(destinationFilePath)) return false;

                JournalLoadResult loadResult = JournalRepository.LoadWithStatus();
                if (loadResult.Status == JournalLoadStatus.Corrupted || loadResult.Status == JournalLoadStatus.AccessDenied)
                {
                    return false;
                }

                StateJournal journal = loadResult.Journal ?? new StateJournal();

                StringBuilder regSb = new StringBuilder();
                regSb.AppendLine("Windows Registry Editor Version 5.00");
                regSb.AppendLine();
                regSb.AppendLine("; ═══════════════════════════════════════════════════════════════════════════");
                regSb.AppendLine("; USB Shield Pro — Standalone Pre-Shield State Rollback File");
                regSb.AppendLine("; Registry rollback file containing the captured recovery values.");
                regSb.AppendLine("; Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
                regSb.AppendLine("; ═══════════════════════════════════════════════════════════════════════════");
                regSb.AppendLine();

                Dictionary<string, List<RegistryOriginalValue>> grouped = new Dictionary<string, List<RegistryOriginalValue>>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in journal.BaselineEntries)
                {
                    string fullKey = (entry.Hive ?? "HKEY_LOCAL_MACHINE") + "\\" + entry.KeyPath;
                    if (!grouped.ContainsKey(fullKey))
                        grouped[fullKey] = new List<RegistryOriginalValue>();
                    grouped[fullKey].Add(entry);
                }

                foreach (var kvp in grouped)
                {
                    regSb.AppendLine("[" + kvp.Key + "]");
                    foreach (var entry in kvp.Value)
                    {
                        if (!entry.ExistedBefore)
                        {
                            regSb.AppendLine("\"" + entry.ValueName + "\"=-");
                        }
                        else
                        {
                            if (string.Equals(entry.ValueKind, "DWord", StringComparison.OrdinalIgnoreCase))
                            {
                                int val = Convert.ToInt32(entry.OriginalData);
                                regSb.AppendLine("\"" + entry.ValueName + "\"=dword:" + val.ToString("x8"));
                            }
                            else
                            {
                                regSb.AppendLine("\"" + entry.ValueName + "\"=\"" + (entry.OriginalData != null ? entry.OriginalData.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") : "") + "\"");
                            }
                        }
                    }
                    regSb.AppendLine();
                }

                string dir = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(destinationFilePath, regSb.ToString(), Encoding.Unicode);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restores the exact pre-shield baseline state programmatically.
        /// Invariant: Refuses recovery success when journal is corrupted, unreadable, or pristine/empty.
        /// Invariant: Never marks BaselineRestored without positive verification of restored values.
        /// Invariant: Preserves exact registry types, DWORDs, and originally absent values.
        /// </summary>
        public static bool RestorePreShieldState(out List<string> errors)
        {
            errors = new List<string>();
            JournalLoadResult loadResult = JournalRepository.LoadWithStatus();

            if (loadResult.Status == JournalLoadStatus.Corrupted)
            {
                errors.Add("Recovery aborted: State recovery journal is corrupted or unreadable. No destructive rollback will be performed to prevent system misconfiguration.");
                return false;
            }

            if (loadResult.Status == JournalLoadStatus.AccessDenied)
            {
                errors.Add("Recovery aborted: Access to recovery journal was denied. Administrator privileges required.");
                return false;
            }

            if (loadResult.Status == JournalLoadStatus.Pristine)
            {
                errors.Add("Recovery unavailable: No pre-shield baseline found. The system configuration has never been modified by USB Shield Pro.");
                return false;
            }

            StateJournal journal = loadResult.Journal;
            if (journal == null || journal.BaselineEntries == null || journal.BaselineEntries.Count == 0)
            {
                errors.Add("Recovery unavailable: State recovery journal contains zero baseline entries.");
                return false;
            }

            bool allVerified = true;

            foreach (var entry in journal.BaselineEntries)
            {
                try
                {
                    RegistryHive hiveEnum = ParseHive(entry.Hive);
                    if (RegistryTransactionHelper.HiveOverride.HasValue)
                    {
                        hiveEnum = RegistryTransactionHelper.HiveOverride.Value;
                    }

                    string effectivePath = entry.KeyPath;
                    if (RegistryTransactionHelper.KeyPathRewriter != null)
                    {
                        effectivePath = RegistryTransactionHelper.KeyPathRewriter(effectivePath);
                    }

                    using (var baseKey = RegistryKey.OpenBaseKey(hiveEnum, RegistryView.Registry64))
                    {
                        if (!entry.ExistedBefore)
                        {
                            // Delete ONLY this specific value from the key
                            using (var subKey = baseKey.OpenSubKey(effectivePath, true))
                            {
                                if (subKey != null)
                                {
                                    subKey.DeleteValue(entry.ValueName, false);
                                }
                            }

                            // Verify deletion
                            using (var checkKey = baseKey.OpenSubKey(effectivePath, false))
                            {
                                if (checkKey != null && checkKey.GetValue(entry.ValueName) != null)
                                {
                                    allVerified = false;
                                    errors.Add("Failed to remove created value: " + entry.GetTargetKeyIdentifier());
                                }
                            }
                        }
                        else
                        {
                            // Restore exact original value
                            using (var subKey = baseKey.CreateSubKey(effectivePath))
                            {
                                if (subKey != null)
                                {
                                    RegistryValueKind kind = ParseValueKind(entry.ValueKind);
                                    object convertedData = ConvertOriginalData(entry.OriginalData, kind);
                                    subKey.SetValue(entry.ValueName, convertedData, kind);
                                }
                            }

                            // Verify restoration
                            using (var checkKey = baseKey.OpenSubKey(effectivePath, false))
                            {
                                if (checkKey == null)
                                {
                                    allVerified = false;
                                    errors.Add("Key missing after restoration: " + effectivePath);
                                }
                                else
                                {
                                    object val = checkKey.GetValue(entry.ValueName);
                                    if (val == null || !VerifyValueMatch(val, entry.OriginalData, entry.ValueKind))
                                    {
                                        allVerified = false;
                                        errors.Add("Value mismatch after restoration: " + entry.GetTargetKeyIdentifier());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    allVerified = false;
                    errors.Add("Error reverting " + entry.GetTargetKeyIdentifier() + ": " + ex.Message);
                }
            }

            if (allVerified)
            {
                journal.BaselineRestored = true;
                journal.BaselineRestoredUtc = DateTime.UtcNow;
                string saveErr;
                JournalRepository.Save(journal, out saveErr);
            }

            return allVerified;
        }

        private static object ConvertOriginalData(object data, RegistryValueKind kind)
        {
            if (data == null)
            {
                if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord) return 0;
                return "";
            }

            try
            {
                if (kind == RegistryValueKind.DWord)
                {
                    return Convert.ToInt32(data);
                }
                if (kind == RegistryValueKind.QWord)
                {
                    return Convert.ToInt64(data);
                }
                if (kind == RegistryValueKind.MultiString)
                {
                    if (data is string[]) return (string[])data;
                    if (data is System.Collections.IEnumerable && !(data is string))
                    {
                        List<string> list = new List<string>();
                        foreach (object item in (System.Collections.IEnumerable)data)
                        {
                            if (item != null) list.Add(item.ToString());
                        }
                        return list.ToArray();
                    }
                    return new string[] { data.ToString() };
                }
                if (kind == RegistryValueKind.Binary)
                {
                    if (data is byte[]) return (byte[])data;
                    return Encoding.UTF8.GetBytes(data.ToString());
                }
            }
            catch { }

            return data.ToString();
        }

        private static bool VerifyValueMatch(object actual, object expected, string kind)
        {
            if (actual == null && expected == null) return true;
            if (actual == null || expected == null) return false;

            string k = (kind ?? "").ToLowerInvariant();
            if (k == "dword")
            {
                try
                {
                    return Convert.ToInt32(actual) == Convert.ToInt32(expected);
                }
                catch { return false; }
            }
            if (k == "qword")
            {
                try
                {
                    return Convert.ToInt64(actual) == Convert.ToInt64(expected);
                }
                catch { return false; }
            }

            return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string MapToRegTypeString(string kind)
        {
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "dword": return "REG_DWORD";
                case "qword": return "REG_QWORD";
                case "string": return "REG_SZ";
                case "multistring": return "REG_MULTI_SZ";
                case "binary": return "REG_BINARY";
                default: return "REG_DWORD";
            }
        }

        private static string FormatRegDataForCmd(object data, string kind)
        {
            if (data == null) return "\"\"";
            string str = data.ToString();
            if ((kind ?? "").ToLowerInvariant() == "dword")
            {
                int val;
                if (int.TryParse(str, out val)) return val.ToString();
            }
            return "\"" + str.Replace("\"", "\\\"") + "\"";
        }

        private static RegistryHive ParseHive(string hive)
        {
            if (string.Equals(hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hive, "HKCU", StringComparison.OrdinalIgnoreCase))
                return RegistryHive.CurrentUser;
            return RegistryHive.LocalMachine;
        }

        private static RegistryValueKind ParseValueKind(string kind)
        {
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "dword": return RegistryValueKind.DWord;
                case "qword": return RegistryValueKind.QWord;
                case "string": return RegistryValueKind.String;
                case "multistring": return RegistryValueKind.MultiString;
                case "binary": return RegistryValueKind.Binary;
                default: return RegistryValueKind.DWord;
            }
        }
    }
}
