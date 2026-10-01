using System.Text.Json;
using System.Text.Json.Serialization;

namespace CbtKiosk;

public sealed class AppSettings
{
    // ---- What the client opens -------------------------------------------------
    public string BaseUrl { get; set; } = "https://cbt.smkdata.sch.id";
    public string StartUrl { get; set; } = "https://cbt.smkdata.sch.id";

    // ---- Quit/unlock password verification ------------------------------------
    /// <summary>Primary endpoint returning the current quit password.</summary>
    public string KioskUrl { get; set; } = "https://cbt.smkdata.sch.id/api/kiosk/settings";

    /// <summary>Secondary ("online fallback") endpoint, queried when the primary is unreachable. Optional.</summary>
    public string FallbackUrl { get; set; } = "";

    public int KioskTimeoutMs { get; set; } = 5000;
    public int KioskAttempts { get; set; } = 3;
    public int KioskAttemptIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Emergency ("offline") exit password, stored as a lower-case base16 SHA-256 hash. This password
    /// is ALWAYS accepted - also when the server is reachable and reports a valid or an expired
    /// password - so a supervisor can never be locked out of a machine.
    ///
    /// Leave empty to keep the factory default (see <see cref="DefaultOfflinePassword"/>).
    /// </summary>
    public string OfflineFallbackPasswordHash { get; set; } = "";

    /// <summary>
    /// When true (the default) the built-in emergency password keeps working as long as no custom
    /// one is configured. Set false to disable it - then only the online password from the CBT
    /// panel can unlock the kiosk.
    /// </summary>
    public bool AllowDefaultEmergencyPassword { get; set; } = true;

    // ---- Lockdown behaviour ----------------------------------------------------
    public bool BlockNavigationKeys { get; set; } = true;
    public bool AllowZoom { get; set; } = false;
    public bool AllowBackNavigation { get; set; } = false;

    /// <summary>Optional: if set (SHA-256 hash), the settings window asks for it before saving.</summary>
    public string SettingsPasswordHash { get; set; } = "";

    /// <summary>
    /// When true, cookies and site data are wiped on quit (and leftovers on start) so that every
    /// exam session requires a fresh login. Default: true.
    /// </summary>
    public bool ClearSessionOnQuit { get; set; } = true;

    // ---- Auto-start ------------------------------------------------------------
    /// <summary>Start the application automatically when Windows starts (at user logon).</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>When auto-starting, register for all users (HKLM) instead of the current user (HKCU).</summary>
    public bool AutoStartAllUsers { get; set; } = false;

    /// <summary>Auto-start mechanism: "registry" (Run key) or "task" (Task Scheduler, on logon).</summary>
    public string StartupMethod { get; set; } = "registry";

    // ---- Non-persisted helpers -------------------------------------------------
    [JsonIgnore] public string SettingsPath { get; set; } = "";
    [JsonIgnore] public string LoadSource { get; set; } = "";

    /// <summary>
    /// Factory default emergency password. It is always active until an administrator stores a
    /// custom offline password through the settings app, so a lab can always be unlocked even on a
    /// brand-new installation (internet down / server dead / online password expired).
    ///
    /// Change it from the settings app; the value below is only the fallback of last resort.
    /// </summary>
    public const string DefaultOfflinePassword = "smkdata2026";

    /// <summary>Lower-case SHA-256 hash of <see cref="DefaultOfflinePassword"/>.</summary>
    public static readonly string DefaultOfflinePasswordHash = Hash.Sha256Hex(DefaultOfflinePassword);

    /// <summary>
    /// The hash actually used to verify the emergency password: the configured one when the
    /// administrator set it, otherwise the built-in default. Empty when the default has been
    /// explicitly disabled and no custom password is set -> no emergency password at all.
    /// </summary>
    [JsonIgnore]
    public string EmergencyPasswordHash =>
        !string.IsNullOrWhiteSpace(OfflineFallbackPasswordHash) ? OfflineFallbackPasswordHash
        : AllowDefaultEmergencyPassword ? DefaultOfflinePasswordHash
        : "";

    /// <summary>True when an emergency password is available (custom or built-in default).</summary>
    [JsonIgnore]
    public bool HasEmergencyPassword => !string.IsNullOrWhiteSpace(EmergencyPasswordHash);

    /// <summary>True while the built-in factory password is the one in effect.</summary>
    [JsonIgnore]
    public bool UsesDefaultEmergencyPassword =>
        string.IsNullOrWhiteSpace(OfflineFallbackPasswordHash) && AllowDefaultEmergencyPassword;

    [JsonIgnore]
    public bool HasOfflineFallback => !string.IsNullOrWhiteSpace(OfflineFallbackPasswordHash);

    [JsonIgnore]
    public bool HasFallbackUrl => !string.IsNullOrWhiteSpace(FallbackUrl);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Directory that holds the settings file: shared first, then per-user.</summary>
    public static string SharedDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CbtKiosk");

    public static string UserDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CbtKiosk");

    public static string SharedPath => Path.Combine(SharedDirectory, "settings.json");
    public static string UserPath => Path.Combine(UserDirectory, "settings.json");

    public static AppSettings Load()
    {
        var defaults = new AppSettings();

        // 1. Shared settings (all users) take precedence.
        if (TryRead(SharedPath, out var shared))
        {
            shared.SettingsPath = SharedPath;
            shared.LoadSource = "shared";
            Logger.Info($"Loaded settings from {SharedPath}");
            return shared;
        }

        // 2. Per-user settings.
        if (TryRead(UserPath, out var user))
        {
            user.SettingsPath = UserPath;
            user.LoadSource = "user";
            Logger.Info($"Loaded settings from {UserPath}");
            return user;
        }

        // 3. Built-in defaults.
        defaults.SettingsPath = SharedPath; // default save target
        defaults.LoadSource = "defaults";
        Logger.Info("No settings file found - using built-in defaults.");
        return defaults;
    }

    private static bool TryRead(string path, out AppSettings settings)
    {
        settings = null;
        try
        {
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded == null) return false;

            // Fill in anything the file left empty from the defaults.
            var d = new AppSettings();
            if (string.IsNullOrWhiteSpace(loaded.BaseUrl)) loaded.BaseUrl = d.BaseUrl;
            if (string.IsNullOrWhiteSpace(loaded.StartUrl)) loaded.StartUrl = loaded.BaseUrl;
            if (string.IsNullOrWhiteSpace(loaded.KioskUrl)) loaded.KioskUrl = d.KioskUrl;
            if (loaded.KioskTimeoutMs <= 0) loaded.KioskTimeoutMs = d.KioskTimeoutMs;
            if (loaded.KioskAttempts <= 0) loaded.KioskAttempts = d.KioskAttempts;
            if (loaded.KioskAttemptIntervalMs < 0) loaded.KioskAttemptIntervalMs = d.KioskAttemptIntervalMs;

            settings = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not read settings from {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Saves to the location the settings were loaded from, falling back sensibly.</summary>
    public string Save()
    {
        var candidates = new List<string> { SettingsPath, SharedPath, UserPath };
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var path in candidates)
        {
            if (string.IsNullOrWhiteSpace(path) || !tried.Add(path)) continue;

            if (TryWrite(path, this, out var error))
            {
                SettingsPath = path;
                LoadSource = string.Equals(path, SharedPath, StringComparison.OrdinalIgnoreCase) ? "shared"
                           : string.Equals(path, UserPath, StringComparison.OrdinalIgnoreCase) ? "user"
                           : LoadSource;
                return path;
            }

            errors.Add($"{path}: {error}");
        }

        throw new IOException("Tidak dapat menyimpan pengaturan.\n" + string.Join("\n", errors));
    }

    private static bool TryWrite(string path, AppSettings settings, out string error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
            Logger.Info($"Saved settings to {path}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
