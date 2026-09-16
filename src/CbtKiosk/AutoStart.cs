using System.Diagnostics;
using Microsoft.Win32;

namespace CbtKiosk;

/// <summary>
/// Registers / unregisters the application for automatic start when Windows starts (at user logon).
///
/// Two mechanisms are supported:
///
///   * "registry" - the standard Run key.
///       per user  : HKCU\Software\Microsoft\Windows\CurrentVersion\Run   (no admin needed)
///       all users : HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run   (needs administrator)
///
///   * "task" - a Task Scheduler task with an "at logon" trigger (schtasks.exe). More reliable in
///       managed environments and can run with the highest available privileges.
///
/// Only one mechanism is ever active, so the app cannot start twice.
/// </summary>
public static class AutoStart
{
    public const string MethodRegistry = "registry";
    public const string MethodTask = "task";

    private const string RunKeyUser = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyMachine = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CbtKiosk";
    private const string TaskName = "CbtKiosk";

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

    // ------------------------------------------------------------------ registry

    public static bool IsEnabledForUser => HasRunValue(Registry.CurrentUser, RunKeyUser);
    public static bool IsEnabledForAllUsers => HasRunValue(Registry.LocalMachine, RunKeyMachine);

    private static bool HasRunValue(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, false);
            return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName) as string);
        }
        catch { return false; }
    }

    /// <summary>The command stored in the Run key (HKCU first, then HKLM), or null when absent.</summary>
    public static string RegisteredRunCommand()
    {
        foreach (var (root, subKey) in new[] { (Registry.CurrentUser, RunKeyUser), (Registry.LocalMachine, RunKeyMachine) })
        {
            try
            {
                using var key = root.OpenSubKey(subKey, false);
                if (key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value))
                    return value;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static void SetRunValue(RegistryKey root, string subKey)
    {
        using var key = root.CreateSubKey(subKey, true)
            ?? throw new InvalidOperationException("Tidak dapat membuka kunci registry.");
        key.SetValue(ValueName, Command, RegistryValueKind.String);
    }

    private static void RemoveRunValue(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, true);
            if (key?.GetValue(ValueName) != null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* ignore */ }
    }

    // ------------------------------------------------------------------ scheduled task

    public static bool IsTaskEnabled()
    {
        var (code, _) = RunSchtasks("/Query", "/TN", TaskName);
        return code == 0;
    }

    private static (int Code, string Output) RunSchtasks(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return (-1, "schtasks.exe tidak dapat dijalankan.");

            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            if (!p.WaitForExit(20000)) return (-1, "schtasks.exe timeout.");
            return (p.ExitCode, output.Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    // ------------------------------------------------------------------ state / apply

    public static bool IsEnabled => IsEnabledForUser || IsEnabledForAllUsers || IsTaskEnabled();

    /// <summary>Human-readable description of the current auto-start state.</summary>
    public static string Describe()
    {
        var parts = new List<string>();
        if (IsEnabledForAllUsers) parts.Add("registry: semua pengguna");
        if (IsEnabledForUser) parts.Add("registry: pengguna ini");
        if (IsTaskEnabled()) parts.Add("scheduled task");
        return parts.Count == 0 ? "Tidak aktif." : "Aktif (" + string.Join(", ", parts) + ").";
    }

    /// <summary>Detailed diagnostic report (registered command, file existence, task state).</summary>
    public static string Verify()
    {
        var lines = new List<string>
        {
            "Path aplikasi sekarang:",
            "  " + CurrentExePath,
            "  file ada: " + (File.Exists(CurrentExePath) ? "YA" : "TIDAK"),
            "",
        };

        var run = RegisteredRunCommand();
        if (run == null)
        {
            lines.Add("Registry Run: TIDAK ada.");
        }
        else
        {
            var path = run.Trim().Trim('"');
            lines.Add("Registry Run: ADA");
            lines.Add("  perintah: " + run);
            lines.Add("  file di path itu ada: " + (File.Exists(path) ? "YA" : "TIDAK (path salah / file dipindah)"));
            if (!string.Equals(path, CurrentExePath, StringComparison.OrdinalIgnoreCase))
                lines.Add("  PERINGATAN: path berbeda dari aplikasi yang sedang berjalan!");
        }

        lines.Add("");
        var (code, output) = RunSchtasks("/Query", "/TN", TaskName);
        lines.Add(code == 0 ? "Scheduled task: ADA" : "Scheduled task: tidak ada.");
        if (code != 0 && !string.IsNullOrWhiteSpace(output))
            lines.Add("  (" + output.Split('\n')[0].Trim() + ")");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Enables or disables auto-start with the chosen mechanism, clearing the other one so the app
    /// starts exactly once. Returns false (with a reason) on failure, e.g. writing HKLM without
    /// administrator rights.
    /// </summary>
    public static bool Apply(bool enabled, bool allUsers, string method, out string error)
    {
        error = null;
        var useTask = string.Equals(method, MethodTask, StringComparison.OrdinalIgnoreCase);

        try
        {
            if (!enabled)
            {
                RemoveRunValue(Registry.CurrentUser, RunKeyUser);
                RemoveRunValue(Registry.LocalMachine, RunKeyMachine);
                var (code, output) = RunSchtasks("/Delete", "/TN", TaskName, "/F");
                if (code != 0 && IsTaskEnabled())
                {
                    error = output;
                    return false;
                }
                Logger.Info("Auto-start disabled.");
                return true;
            }

            if (useTask)
            {
                // Remove any registry entry so we don't start twice.
                RemoveRunValue(Registry.CurrentUser, RunKeyUser);
                RemoveRunValue(Registry.LocalMachine, RunKeyMachine);

                var args = new List<string>
                {
                    "/Create", "/TN", TaskName,
                    "/TR", Command,
                    "/SC", "ONLOGON", "/F",
                };
                if (allUsers)
                {
                    args.Add("/RL"); args.Add("HIGHEST");
                }

                var (code, output) = RunSchtasks(args.ToArray());
                if (code != 0)
                {
                    error = output;
                    Logger.Warn("Scheduled task creation failed: " + output);
                    return false;
                }

                Logger.Info($"Auto-start enabled (scheduled task) -> {Command}");
                return true;
            }

            // registry method
            var (dCode, dOutput) = RunSchtasks("/Delete", "/TN", TaskName, "/F");
            if (dCode != 0 && IsTaskEnabled())
            {
                error = "Tidak dapat menghapus scheduled task lama: " + dOutput;
                return false;
            }

            if (allUsers)
            {
                SetRunValue(Registry.LocalMachine, RunKeyMachine);
                RemoveRunValue(Registry.CurrentUser, RunKeyUser);
            }
            else
            {
                SetRunValue(Registry.CurrentUser, RunKeyUser);
                RemoveRunValue(Registry.LocalMachine, RunKeyMachine);
            }

            Logger.Info($"Auto-start enabled (registry, allUsers={allUsers}) -> {Command}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Logger.Warn("Auto-start could not be applied: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Self-healing check performed at startup: when auto-start is enabled in the settings but the
    /// registered path no longer matches the running executable (e.g. the .exe was moved), re-register
    /// it. Never throws.
    /// </summary>
    public static void EnsureMatches(AppSettings settings)
    {
        try
        {
            if (!settings.AutoStart) return;

            var run = RegisteredRunCommand();
            var useTask = string.Equals(settings.StartupMethod, MethodTask, StringComparison.OrdinalIgnoreCase);

            if (useTask)
            {
                if (!IsTaskEnabled())
                {
                    Logger.Info("Auto-start: scheduled task missing - re-creating.");
                    Apply(true, settings.AutoStartAllUsers, MethodTask, out _);
                }
                return;
            }

            if (run == null)
            {
                Logger.Info("Auto-start: registry entry missing - re-creating.");
                Apply(true, settings.AutoStartAllUsers, MethodRegistry, out _);
                return;
            }

            var registered = run.Trim().Trim('"');
            if (!string.Equals(registered, CurrentExePath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"Auto-start: registered path '{registered}' differs from running path '{CurrentExePath}' - fixing.");
                Apply(true, settings.AutoStartAllUsers, MethodRegistry, out _);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("AutoStart.EnsureMatches failed: " + ex.Message);
        }
    }
}
