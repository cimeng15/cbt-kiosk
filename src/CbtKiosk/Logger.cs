using System.Text;

namespace CbtKiosk;

/// <summary>
/// Very small file logger. Writes to %ProgramData%\CbtKiosk\logs, falling back to
/// %LocalAppData%\CbtKiosk\logs when the shared folder is not writable (non-admin user).
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static string _dir;

    public static string LogDirectory
    {
        get
        {
            if (_dir != null) return _dir;
            try
            {
                var shared = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CbtKiosk");
                _dir = Path.Combine(shared, "logs");
                Directory.CreateDirectory(_dir);
            }
            catch
            {
                var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CbtKiosk");
                _dir = Path.Combine(user, "logs");
                Directory.CreateDirectory(_dir);
            }
            return _dir;
        }
    }

    public static string LogFilePath => Path.Combine(LogDirectory, $"cbtkiosk-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}{Environment.NewLine}";
            lock (Gate) { File.AppendAllText(LogFilePath, line, Encoding.UTF8); }
        }
        catch
        {
            // Never let logging break the exam.
        }
    }
}
