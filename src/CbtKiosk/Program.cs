using System.Threading;

namespace CbtKiosk;

internal static class Program
{
    // Global\ prefix so the guard also works across different Windows sessions.
    private const string MutexName = "Global\\CbtKiosk.SingleInstance.9F2A5C71";
    private static Mutex _mutex;

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Logger.Error("Unhandled UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error("Unhandled exception: " + e.ExceptionObject);

        var settings = AppSettings.Load();

        Logger.Info("========================================================");
        Logger.Info($"CbtKiosk start - pid {Environment.ProcessId}, args=[{string.Join(' ', args)}]");
        Logger.Info($"Settings file : {settings.SettingsPath}");
        Logger.Info($"Base URL      : {settings.BaseUrl}");
        Logger.Info($"Kiosk endpoint: {settings.KioskUrl}");
        Logger.Info($"Fallback URL  : {(string.IsNullOrWhiteSpace(settings.FallbackUrl) ? "(none)" : settings.FallbackUrl)}");
        Logger.Info($"Offline pwd   : {(string.IsNullOrWhiteSpace(settings.OfflineFallbackPasswordHash) ? "(not set)" : "(set)")}");
        Logger.Info($"Auto-start    : {AutoStart.Describe()}");

        var openSettings = args.Any(a =>
            a.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/settings", StringComparison.OrdinalIgnoreCase));

        if (openSettings)
        {
            // Administrative entry point: open the settings window instead of the kiosk.
            // Recommended: run "CbtKiosk.exe --settings" as Administrator so the shared
            // settings file in %ProgramData% can be written.
            Application.Run(new SettingsForm(settings, standalone: true));
            return;
        }

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Logger.Info("Another instance is already running - exiting this one.");
            return;
        }

        try
        {
            Application.Run(new MainForm(settings));
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal: " + ex);
        }
        finally
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
            _mutex.Dispose();

            // Final cleanup after the process has released the WebView2 files: make sure no
            // cookies / session data survive, so the next exam always starts at the login page.
            if (settings.ClearSessionOnQuit)
            {
                MainForm.ClearSessionArtifactsOnDisk();
                Logger.Info("Session cleanup finished.");
            }
        }
    }
}
