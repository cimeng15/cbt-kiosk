using Microsoft.Win32;

namespace CbtKiosk;

/// <summary>
/// Registers / unregisters the application for automatic start when Windows starts (at user logon),
/// using the standard "Run" registry keys.
///
///   * Per user  : HKCU\Software\Microsoft\Windows\CurrentVersion\Run   (no admin rights needed)
///   * All users : HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run   (needs administrator rights)
///
/// Only one of the two scopes is ever active, so the app cannot start twice.
/// </summary>
public static class AutoStart
{
    private const string RunKeyUser = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyMachine = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CbtKiosk";

    /// <summary>Full path of the running executable (the .exe itself, also for single-file publish).</summary>
    public static string CurrentExePath
    {
        get
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                path = Application.ExecutablePath;
            }
            return path;
        }
    }

    private static string Command => $"\"{CurrentExePath}\"";

    public static bool IsEnabledForUser => HasValue(Registry.CurrentUser, RunKeyUser);
    public static bool IsEnabledForAllUsers => HasValue(Registry.LocalMachine, RunKeyMachine);
    public static bool IsEnabled => IsEnabledForUser || IsEnabledForAllUsers;

    /// <summary>Human-readable description of the current auto-start state.</summary>
    public static string Describe()
    {
        if (IsEnabledForAllUsers) return "Aktif - untuk SEMUA pengguna Windows.";
        if (IsEnabledForUser) return "Aktif - untuk pengguna Windows ini saja.";
        return "Tidak aktif.";
    }

    private static bool HasValue(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables auto-start. When enabling, the other scope is cleared so the app
    /// starts exactly once. Returns false (with a reason in <paramref name="error"/>) on failure,
    /// e.g. when writing HKLM without administrator rights.
    /// </summary>
    public static bool Apply(bool enabled, bool allUsers, out string error)
    {
        error = null;
        try
        {
            if (enabled)
            {
                var root = allUsers ? Registry.LocalMachine : Registry.CurrentUser;
                var subKey = allUsers ? RunKeyMachine : RunKeyUser;

                using (var key = root.CreateSubKey(subKey, true))
                {
                    if (key == null)
                    {
                        error = "Tidak dapat membuka kunci registry.";
                        return false;
                    }
                    key.SetValue(ValueName, Command, RegistryValueKind.String);
                }

                // Clear the other scope so we never start twice.
                RemoveFrom(allUsers ? Registry.CurrentUser : Registry.LocalMachine,
                           allUsers ? RunKeyUser : RunKeyMachine);

                Logger.Info($"Auto-start enabled ({subKey}) -> {Command}");
            }
            else
            {
                RemoveFrom(Registry.CurrentUser, RunKeyUser);
                RemoveFrom(Registry.LocalMachine, RunKeyMachine);
                Logger.Info("Auto-start disabled.");
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Logger.Warn("Auto-start could not be applied: " + ex.Message);
            return false;
        }
    }

    private static void RemoveFrom(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, true);
            if (key?.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore: the key may not exist or may be read-only.
        }
    }
}
