// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.1 — Safe Passive GitHub Release Update Notifier
// (c) 2026 0xAbood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Encapsulates the outcome of a GitHub release update check.
    /// </summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseNotes { get; set; }
        public string HtmlUrl { get; set; }
        public string ErrorMessage { get; set; }

        public UpdateCheckResult()
        {
            Success = false;
            UpdateAvailable = false;
            CurrentVersion = UpdateChecker.CurrentVersion;
            LatestVersion = "";
            ReleaseName = "";
            ReleaseNotes = "";
            HtmlUrl = UpdateChecker.DefaultReleasesUrl;
            ErrorMessage = null;
        }
    }

    /// <summary>
    /// Safe, passive, metadata-only update checker for USB Shield Pro.
    /// 
    /// SECURITY INVARIANTS:
    /// 1. ZERO NETWORK ACTIVITY IN BACKGROUND SERVICE:
    ///    The UsbShieldService daemon runs in Session 0 and is strictly prohibited from
    ///    making network connections. Update checks are strictly localized to GUI mode.
    /// 2. READ-ONLY METADATA CHECK:
    ///    Only reads release JSON metadata (tag_name, html_url) from GitHub API via TLS 1.2.
    ///    NEVER downloads, caches, or executes any executable payload or updater binaries.
    /// 3. URL WHITELIST ENFORCEMENT:
    ///    All external URLs must strictly belong to https://github.com/0x-Abood/USB-Shield-Pro/
    ///    before being opened in the default browser.
    /// 4. AIR-GAPPED & RATE-LIMIT DEFENSIVE:
    ///    Automatic checks are throttle-guarded to at most once per 12 hours and can be
    ///    completely disabled via AppSettingsData.CheckUpdatesAutomatically.
    /// </summary>
    public static class UpdateChecker
    {
        public const string CurrentVersion = "2.1.0";
        public const string DefaultReleasesUrl = "https://github.com/0x-Abood/USB-Shield-Pro/releases";
        public const string ExpectedRepoBaseUrl = "https://github.com/0x-Abood/USB-Shield-Pro";
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/0x-Abood/USB-Shield-Pro/releases/latest";
        public const string UserAgent = "USB-Shield-Pro/2.1.0 (+https://github.com/0x-Abood/USB-Shield-Pro)";
        public const int RequestTimeoutMs = 8000;

        /// <summary>
        /// Test Seam: Injects mock GitHub JSON response for 100% offline unit testing.
        /// When null, live HTTPS request is performed.
        /// </summary>
        public static Func<string> MockJsonResponseProvider = null;

        public static void ResetSeam()
        {
            MockJsonResponseProvider = null;
        }

        /// <summary>
        /// Validates that a target URL strictly matches the official GitHub repository domain and path.
        /// Prevents open-redirect or malicious domain injection from manipulated metadata.
        /// </summary>
        public static bool IsValidUpdateUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;

            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;

            string path = uri.AbsolutePath ?? "";
            if (!path.StartsWith("/0x-Abood/USB-Shield-Pro", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        /// <summary>
        /// Safely opens the specified release URL in the user's default browser after whitelist validation.
        /// </summary>
        public static void OpenReleaseUrl(string url)
        {
            string safeUrl = IsValidUpdateUrl(url) ? url : DefaultReleasesUrl;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = safeUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        /// <summary>
        /// Compares two version strings numerically using semantic components (Major, Minor, Build, Revision).
        /// Handles leading 'v'/'V', prerelease tags (e.g. '-rc1'), and uneven component counts.
        /// Returns:
        ///   > 0 if v1 > v2
        ///   = 0 if v1 == v2
        ///   &lt; 0 if v1 &lt; v2
        /// </summary>
        public static int CompareVersions(string v1, string v2)
        {
            int[] c1 = ParseVersionComponents(v1);
            int[] c2 = ParseVersionComponents(v2);

            for (int i = 0; i < 4; i++)
            {
                int p1 = (i < c1.Length) ? c1[i] : 0;
                int p2 = (i < c2.Length) ? c2[i] : 0;
                if (p1 != p2)
                {
                    return p1.CompareTo(p2);
                }
            }

            return 0;
        }

        /// <summary>
        /// Returns true if latestVersion is strictly greater than currentVersion.
        /// </summary>
        public static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            return CompareVersions(latestVersion, currentVersion) > 0;
        }

        /// <summary>
        /// Parses a version string into an array of 4 integer components [Major, Minor, Build, Revision].
        /// Robust against null, empty, non-numeric, or malformed strings.
        /// </summary>
        public static int[] ParseVersionComponents(string versionString)
        {
            int[] result = new int[4];
            if (string.IsNullOrWhiteSpace(versionString)) return result;

            string clean = versionString.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(1).Trim();
            }

            // Strip prerelease tags or commit suffixes: "2.2.0-rc1" -> "2.2.0", "2.1.0+build1" -> "2.1.0"
            int dashIdx = clean.IndexOfAny(new char[] { '-', '+' });
            if (dashIdx >= 0)
            {
                clean = clean.Substring(0, dashIdx).Trim();
            }

            string[] parts = clean.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(4, parts.Length); i++)
            {
                int val;
                if (int.TryParse(parts[i].Trim(), out val))
                {
                    result[i] = Math.Max(0, val);
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to parse GitHub Release API JSON response extracting tag_name and html_url safely.
        /// </summary>
        public static bool TryParseReleaseJson(string json, out string tagName, out string htmlUrl, out string name, out string body)
        {
            tagName = null;
            htmlUrl = null;
            name = null;
            body = null;

            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(json);
                if (dict == null) return false;

                if (dict.ContainsKey("tag_name") && dict["tag_name"] != null)
                {
                    tagName = dict["tag_name"].ToString().Trim();
                }

                if (dict.ContainsKey("html_url") && dict["html_url"] != null)
                {
                    htmlUrl = dict["html_url"].ToString().Trim();
                }

                if (dict.ContainsKey("name") && dict["name"] != null)
                {
                    name = dict["name"].ToString().Trim();
                }

                if (dict.ContainsKey("body") && dict["body"] != null)
                {
                    body = dict["body"].ToString().Trim();
                }

                return !string.IsNullOrEmpty(tagName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Synchronously checks GitHub for latest release metadata.
        /// </summary>
        public static UpdateCheckResult CheckForUpdatesSync()
        {
            UpdateCheckResult res = new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion
            };

            try
            {
                string json;
                if (MockJsonResponseProvider != null)
                {
                    json = MockJsonResponseProvider();
                }
                else
                {
                    // Enforce TLS 1.2
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(LatestReleaseApiUrl);
                    req.Method = "GET";
                    req.UserAgent = UserAgent;
                    req.Accept = "application/vnd.github.v3+json";
                    req.Timeout = RequestTimeoutMs;
                    req.ReadWriteTimeout = RequestTimeoutMs;
                    req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream stream = resp.GetResponseStream())
                    {
                        if (stream == null)
                        {
                            res.ErrorMessage = "Empty response stream from update server.";
                            return res;
                        }

                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }

                string tagName, htmlUrl, name, body;
                if (!TryParseReleaseJson(json, out tagName, out htmlUrl, out name, out body))
                {
                    res.ErrorMessage = "Failed to parse release metadata from GitHub.";
                    return res;
                }

                res.Success = true;
                res.LatestVersion = tagName;
                res.ReleaseName = name;
                res.ReleaseNotes = body;
                res.HtmlUrl = IsValidUpdateUrl(htmlUrl) ? htmlUrl : DefaultReleasesUrl;
                res.UpdateAvailable = IsNewerVersion(tagName, CurrentVersion);

                return res;
            }
            catch (WebException wex)
            {
                res.Success = false;
                if (wex.Response is HttpWebResponse)
                {
                    HttpWebResponse httpResp = (HttpWebResponse)wex.Response;
                    if (httpResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        res.ErrorMessage = "No releases published yet on repository.";
                    }
                    else if ((int)httpResp.StatusCode == 403)
                    {
                        res.ErrorMessage = "GitHub API rate limit reached. Please try again later.";
                    }
                    else
                    {
                        res.ErrorMessage = string.Format("HTTP error {0}: {1}", (int)httpResp.StatusCode, httpResp.StatusDescription);
                    }
                }
                else
                {
                    res.ErrorMessage = wex.Message;
                }
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorMessage = ex.Message;
                return res;
            }
        }

        /// <summary>
        /// Asynchronously checks for updates on a background thread pool worker and invokes callback on completion.
        /// </summary>
        public static void CheckForUpdatesAsync(Action<UpdateCheckResult> callback)
        {
            if (callback == null) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateCheckResult result = CheckForUpdatesSync();
                try
                {
                    callback(result);
                }
                catch { }
            });
        }
    }
}
