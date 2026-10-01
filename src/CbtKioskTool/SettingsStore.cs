using System;
using System.IO;
using System.Text;

namespace CbtKioskTool
{
    /// <summary>
    /// Reads and writes settings.json in exactly the same two locations the kiosk uses:
    /// the shared folder (all users, preferred) and the per-user folder (no admin needed).
    /// </summary>
    internal static class SettingsStore
    {
        public static string SharedDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CbtKiosk");

        public static string UserDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CbtKiosk");

        public static string SharedPath => Path.Combine(SharedDirectory, "settings.json");
        public static string UserPath => Path.Combine(UserDirectory, "settings.json");

        /// <summary>Shared settings win; otherwise per-user; otherwise built-in defaults.</summary>
        public static AppSettings Load(out string source, out string warning)
        {
            source = "";
            warning = null;

            foreach (var path in new[] { SharedPath, UserPath })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var parsed = Json.Parse(File.ReadAllText(path));
                    var settings = AppSettings.FromJson(Json.AsObject(parsed));
                    settings.LoadedFrom = path;
                    source = path;
                    return settings;
                }
                catch (Exception ex)
                {
                    warning = "Berkas " + path + " tidak dapat dibaca: " + ex.Message;
                }
            }

            var defaults = new AppSettings();
            defaults.LoadedFrom = "";
            source = "(belum ada berkas - memakai nilai bawaan)";
            return defaults;
        }

        /// <summary>Writes to the file it was loaded from, then to the shared, then to the user folder.</summary>
        public static string Save(AppSettings settings)
        {
            var errors = new StringBuilder();

            foreach (var path in Candidates(settings))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, Json.SerializeSettings(settings), new UTF8Encoding(false));
                    settings.LoadedFrom = path;
                    return path;
                }
                catch (Exception ex)
                {
                    errors.Append(path).Append(": ").Append(ex.Message).Append("\r\n");
                }
            }

            throw new IOException("Tidak dapat menyimpan pengaturan.\r\n" + errors);
        }

        private static System.Collections.Generic.List<string> Candidates(AppSettings settings)
        {
            var list = new System.Collections.Generic.List<string>();
            void Add(string p)
            {
                if (!string.IsNullOrWhiteSpace(p) && !list.Contains(p)) list.Add(p);
            }

            Add(settings.LoadedFrom);
            Add(SharedPath);
            Add(UserPath);
            return list;
        }

        // ------------------------------------------------------------ kiosk.exe path

        /// <summary>Folder the supervisor most likely wants to open (shared folder when it exists).</summary>
        public static string LoadedDirectoryHint()
        {
            try
            {
                if (Directory.Exists(SharedDirectory)) return SharedDirectory;
            }
            catch { /* ignore */ }
            return UserDirectory;
        }

        private static string PreferencePath => Path.Combine(SharedDirectory, "setup.json");

        public static string LoadKioskExePath()
        {
            foreach (var path in new[] { PreferencePath, Path.Combine(UserDirectory, "setup.json") })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var map = Json.AsObject(Json.Parse(File.ReadAllText(path)));
                    var value = map != null && map.ContainsKey("KioskExePath") ? Json.AsString(map["KioskExePath"]) : null;
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        public static void SaveKioskExePath(string kioskPath)
        {
            var json = "{\r\n  \"KioskExePath\": " + Quote(kioskPath) + "\r\n}\r\n";

            foreach (var path in new[] { PreferencePath, Path.Combine(UserDirectory, "setup.json") })
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, json, new UTF8Encoding(false));
                    return;
                }
                catch { /* try the next location */ }
            }
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in value ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\');
                sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
