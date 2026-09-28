using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;

namespace CbtKiosk.Setup;

/// <summary>
/// Entry point of the standalone settings application (CbtKioskSetup.exe).
///
/// Why it exists: the kiosk used to be configured by running
/// <c>CbtKiosk.exe --settings</c> from a command line. This app does the same job through a
/// friendly window, so an administrator can set up a whole lab without touching a console.
/// </summary>
internal static class Program
{
    internal const string AppTitle = "CBT Kiosk \u2014 Aplikasi Pengaturan";

    /// <summary>True when the current process has administrator rights (needed for %ProgramData% and HKLM).</summary>
    internal static bool IsAdministrator { get; private set; }

    /// <summary>Preferred file name of the kiosk executable this app configures.</summary>
    internal const string KioskExeName = "CbtKiosk.exe";

    private const string HelpText =
        "Aplikasi pengaturan CBT Kiosk.\n\n" +
        "Cara pakai:\n" +
        "  1. Periksa/ubah alamat ujian, password, dan penguncian.\n" +
        "  2. Klik \"Simpan\".\n" +
        "  3. Jalankan CbtKiosk.exe pada komputer siswa.\n\n" +
        "Untuk menyimpan pengaturan bersama semua pengguna\n" +
        "(%ProgramData%\\CbtKiosk) dan mengaktifkan auto-start untuk\n" +
        "semua pengguna, jalankan aplikasi ini sebagai Administrator.\n\n" +
        "Argumen yang didukung:\n" +
        "  --help        tampilkan bantuan ini\n" +
        "  --kiosk PATH  tentukan lokasi CbtKiosk.exe\n" +
        "  --reset       hapus pengaturan (kembali ke bawaan)";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Logger.Error("Unhandled UI exception: " + e.Exception);
            ShowFatal(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error("Unhandled exception: " + e.ExceptionObject);

        IsAdministrator = CheckAdministrator();

        if (args.Any(a => a is "--help" or "-h" or "-?" or "/?" or "/help"))
        {
            MessageBox.Show(HelpText, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Logger.Info("========================================================");
        Logger.Info($"CbtKioskSetup start - pid {Environment.ProcessId}, administrator={IsAdministrator}, args=[{string.Join(' ', args)}]");

        // Remember a kiosk path given on the command line.
        var kioskArg = ValueAfter(args, "--kiosk");
        if (!string.IsNullOrWhiteSpace(kioskArg)) SaveKioskExePreference(kioskArg);

        if (args.Any(a => a.Equals("--reset", StringComparison.OrdinalIgnoreCase)))
        {
            ResetSettings();
        }

        // Tell the shared auto-start code which executable to register, so enabling auto-start here
        // writes the kiosk path - not the path of this settings app.
        AutoStart.TargetExePath = ResolveKioskExePath();

        try
        {
            Application.Run(new SetupForm());
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal: " + ex);
            ShowFatal(ex);
        }
    }

    private static void ShowFatal(Exception ex) =>
        MessageBox.Show(
            "Terjadi kesalahan pada aplikasi pengaturan.\n\n" + ex.Message +
            "\n\nLog: " + Logger.LogDirectory,
            AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static bool CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string ValueAfter(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    // ---------------------------------------------------------------- kiosk exe discovery

    /// <summary>Where the chosen kiosk path is remembered between runs.</summary>
    private static string PreferencePath =>
        Path.Combine(AppSettings.SharedDirectory, "setup.json");

    private sealed class SetupPreference
    {
        public string KioskExePath { get; set; } = "";
    }

    private static string LoadKioskExePreference()
    {
        foreach (var path in new[] { PreferencePath, Path.Combine(AppSettings.UserDirectory, "setup.json") })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var pref = JsonSerializer.Deserialize<SetupPreference>(File.ReadAllText(path));
                if (!string.IsNullOrWhiteSpace(pref?.KioskExePath)) return pref.KioskExePath;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    internal static void SaveKioskExePreference(string kioskPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(new SetupPreference { KioskExePath = kioskPath },
                new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(AppSettings.SharedDirectory);
            File.WriteAllText(PreferencePath, json);
        }
        catch
        {
            // Fall back to the per-user folder when the shared one is not writable.
            try
            {
                Directory.CreateDirectory(AppSettings.UserDirectory);
                var json = JsonSerializer.Serialize(new SetupPreference { KioskExePath = kioskPath },
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppSettings.UserDirectory, "setup.json"), json);
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Finds CbtKiosk.exe: an explicit preference first, then the auto-start registration, then the
    /// usual places (next to this app, its parent, dist/, publish/, %ProgramData%\CbtKiosk).
    /// </summary>
    internal static string ResolveKioskExePath()
    {
        var candidates = new List<string>();

        void Add(string p)
        {
            if (!string.IsNullOrWhiteSpace(p)) candidates.Add(p);
        }

        var pref = LoadKioskExePreference();
        if (!string.IsNullOrWhiteSpace(pref)) Add(pref);

        try
        {
            var registered = AutoStart.RegisteredRunCommand();
            if (!string.IsNullOrWhiteSpace(registered)) Add(registered.Trim().Trim('"'));
        }
        catch { /* ignore */ }

        var baseDir = AppContext.BaseDirectory;
        Add(Path.Combine(baseDir, KioskExeName));
        Add(Path.Combine(baseDir, "dist", KioskExeName));
        Add(Path.Combine(baseDir, "publish", "win-x64", KioskExeName));
        Add(Path.Combine(baseDir, "..", KioskExeName));
        Add(Path.Combine(baseDir, "..", "dist", KioskExeName));
        Add(Path.Combine(baseDir, "..", "..", KioskExeName));
        Add(Path.Combine(AppSettings.SharedDirectory, KioskExeName));

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            catch { /* ignore */ }
        }

        // Nothing found: still return the most likely target so the UI can show and fix it.
        var fallback = Path.Combine(baseDir, KioskExeName);
        try { return Path.GetFullPath(fallback); } catch { return fallback; }
    }

    // ---------------------------------------------------------------- elevation

    /// <summary>Relaunches this app elevated (UAC prompt) and exits the current instance.</summary>
    internal static bool RestartElevated()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"--kiosk \"{ResolveKioskExePath()}\"",
                WorkingDirectory = AppContext.BaseDirectory,
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Elevation cancelled or failed: " + ex.Message);
            return false;
        }
    }

    private static void ResetSettings()
    {
        var answer = MessageBox.Show(
            "Hapus berkas pengaturan CBT Kiosk dan kembali ke pengaturan bawaan?\n\n" +
            AppSettings.SharedPath + "\n" + AppSettings.UserPath,
            AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;

        foreach (var path in new[] { AppSettings.SharedPath, AppSettings.UserPath })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Logger.Info("Settings file deleted: " + path);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not delete {path}: {ex.Message}");
            }
        }
    }
}
