using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace CbtKioskTool
{
    /// <summary>
    /// Shared constants of the CBT Kiosk, kept in sync with src/CbtKiosk.
    /// </summary>
    internal static class Kiosk
    {
        /// <summary>Must match AppSettings.DefaultOfflinePassword in the kiosk.</summary>
        public const string DefaultOfflinePassword = "smkdata2026";

        public const string DefaultBaseUrl = "https://cbt.smkdata.sch.id";
        public const string DefaultStartUrl = "https://cbt.smkdata.sch.id";
        public const string DefaultKioskUrl = "https://cbt.smkdata.sch.id/api/kiosk/settings";

        /// <summary>Must match AutoStart.MethodRegistry / MethodTask in the kiosk.</summary>
        public const string MethodRegistry = "registry";
        public const string MethodTask = "task";

        public const string Product = "setting-CBT";
        public const string Version = "1.7.2";
    }

    /// <summary>Mirror of the kiosk's settings.json model (same property names, camel/lower insensitive read).</summary>
    internal sealed class AppSettings
    {
        public string BaseUrl = Kiosk.DefaultBaseUrl;
        public string StartUrl = Kiosk.DefaultStartUrl;
        public string KioskUrl = Kiosk.DefaultKioskUrl;
        public string FallbackUrl = "";
        public int KioskTimeoutMs = 5000;
        public int KioskAttempts = 3;
        public int KioskAttemptIntervalMs = 1000;

        public string OfflineFallbackPasswordHash = "";
        public bool AllowDefaultEmergencyPassword = true;

        public bool BlockNavigationKeys = true;
        public bool AllowZoom = false;
        public bool AllowBackNavigation = false;

        public string SettingsPasswordHash = "";
        public bool ClearSessionOnQuit = true;

        public bool AutoStart = false;
        public bool AutoStartAllUsers = false;
        public string StartupMethod = Kiosk.MethodRegistry;

        // ---- runtime-only ---------------------------------------------------
        /// <summary>Where this instance was loaded from ("" when it was built from defaults).</summary>
        public string LoadedFrom = "";

        public bool HasOfflineFallback => !string.IsNullOrWhiteSpace(OfflineFallbackPasswordHash);

        public static AppSettings FromJson(Dictionary<string, object> json)
        {
            var s = new AppSettings();
            if (json == null) return s;

            object v;
            if (json.TryGetValue("BaseUrl", out v) && Json.AsString(v) != null) s.BaseUrl = Json.AsString(v);
            if (json.TryGetValue("StartUrl", out v) && Json.AsString(v) != null) s.StartUrl = Json.AsString(v);
            if (json.TryGetValue("KioskUrl", out v) && Json.AsString(v) != null) s.KioskUrl = Json.AsString(v);
            if (json.TryGetValue("FallbackUrl", out v) && Json.AsString(v) != null) s.FallbackUrl = Json.AsString(v);
            if (json.TryGetValue("KioskTimeoutMs", out v)) s.KioskTimeoutMs = Json.AsInt(v, s.KioskTimeoutMs);
            if (json.TryGetValue("KioskAttempts", out v)) s.KioskAttempts = Json.AsInt(v, s.KioskAttempts);
            if (json.TryGetValue("KioskAttemptIntervalMs", out v)) s.KioskAttemptIntervalMs = Json.AsInt(v, s.KioskAttemptIntervalMs);
            if (json.TryGetValue("OfflineFallbackPasswordHash", out v) && Json.AsString(v) != null) s.OfflineFallbackPasswordHash = Json.AsString(v);
            if (json.TryGetValue("AllowDefaultEmergencyPassword", out v)) s.AllowDefaultEmergencyPassword = Json.AsBool(v, true);
            if (json.TryGetValue("BlockNavigationKeys", out v)) s.BlockNavigationKeys = Json.AsBool(v, true);
            if (json.TryGetValue("AllowZoom", out v)) s.AllowZoom = Json.AsBool(v, false);
            if (json.TryGetValue("AllowBackNavigation", out v)) s.AllowBackNavigation = Json.AsBool(v, false);
            if (json.TryGetValue("SettingsPasswordHash", out v) && Json.AsString(v) != null) s.SettingsPasswordHash = Json.AsString(v);
            if (json.TryGetValue("ClearSessionOnQuit", out v)) s.ClearSessionOnQuit = Json.AsBool(v, true);
            if (json.TryGetValue("AutoStart", out v)) s.AutoStart = Json.AsBool(v, false);
            if (json.TryGetValue("AutoStartAllUsers", out v)) s.AutoStartAllUsers = Json.AsBool(v, false);
            if (json.TryGetValue("StartupMethod", out v) && Json.AsString(v) != null) s.StartupMethod = Json.AsString(v);

            // Sensible fills, exactly like the kiosk does when it loads the file.
            if (string.IsNullOrWhiteSpace(s.BaseUrl)) s.BaseUrl = Kiosk.DefaultBaseUrl;
            if (string.IsNullOrWhiteSpace(s.StartUrl)) s.StartUrl = s.BaseUrl;
            if (string.IsNullOrWhiteSpace(s.KioskUrl)) s.KioskUrl = Kiosk.DefaultKioskUrl;
            if (s.KioskTimeoutMs <= 0) s.KioskTimeoutMs = 5000;
            if (s.KioskAttempts <= 0) s.KioskAttempts = 3;
            if (s.KioskAttemptIntervalMs < 0) s.KioskAttemptIntervalMs = 1000;

            return s;
        }
    }

    /// <summary>Lower-case base16 SHA-256, identical to Hash.cs in the kiosk.</summary>
    internal static class Hash
    {
        public static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var ba = Encoding.UTF8.GetBytes(a);
            var bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;

            var diff = 0;
            for (var i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
            return diff == 0;
        }
    }
}
