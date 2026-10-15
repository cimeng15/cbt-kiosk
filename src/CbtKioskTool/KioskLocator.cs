using System;
using System.Collections.Generic;
using System.IO;

namespace CbtKioskTool
{
    /// <summary>
    /// Detects the location of CbtKiosk.exe automatically, so the supervisor does not have to
    /// point at the file by hand. Replaces the fixed five-candidate guess in MainForm, which
    /// missed common install folders (C:\CbtKiosk, Desktop, Downloads, other drives) and could
    /// therefore show an empty or stale "CbtKiosk.exe:" box even when the file existed.
    /// Search order (first hit wins):
    ///   1. previously chosen path stored in setup.json (SettingsStore.LoadKioskExePath)
    ///   2. registered auto-start command (registry Run / scheduled task)
    ///   3. folders around this tool itself (same folder, dist, publish, parent, sibling dist)
    ///   4. well-known folders on every fixed drive (C:\CbtKiosk, Desktop, Downloads, ...)
    /// </summary>
    internal static class KioskLocator
    {
        private const string ExeName = "CbtKiosk.exe";

        /// <summary>Runs the whole search. Never throws; falls back to a sensible default path.</summary>
        public static string Find()
        {
            try
            {
                var found = FindOrNull();
                if (found != null) return found;
            }
            catch { /* ignore */ }

            // Nothing found: default to "next to this tool", which is where the README puts it.
            try { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExeName)); }
            catch { return ExeName; }
        }

        public static string FindOrNull()
        {
            // 1) what the supervisor chose before
            try
            {
                var remembered = SettingsStore.LoadKioskExePath();
                if (IsValid(remembered)) return remembered;
            }
            catch { /* ignore */ }

            // 2) where auto-start points
            try
            {
                var run = AutoStart.RegisteredRunCommand();
                var path = run != null ? run.Trim().Trim('"') : null;
                if (IsValid(path) && string.Equals(Path.GetFileName(path), ExeName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            catch { /* ignore */ }

            // 3) around this tool
            foreach (var candidate in AroundTool())
            {
                if (IsValid(candidate)) return candidate;
            }

            // 4) well-known folders on every fixed drive
            foreach (var candidate in WellKnownFolders())
            {
                if (IsValid(candidate)) return candidate;
            }

            return null;
        }

        private static bool IsValid(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
                       string.Equals(Path.GetFileName(path), ExeName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static IEnumerable<string> AroundTool()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(baseDir, ExeName);
            yield return Path.Combine(baseDir, "dist", ExeName);
            yield return Path.Combine(baseDir, "publish", "win-x64", ExeName);
            yield return Path.Combine(baseDir, "..", ExeName);
            yield return Path.Combine(baseDir, "..", "dist", ExeName);
        }

        private static IEnumerable<string> WellKnownFolders()
        {
            var roots = new List<string>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady) roots.Add(drive.RootDirectory.FullName);
                }
            }
            catch { /* ignore */ }
            if (roots.Count == 0) roots.Add(@"C:\");

            foreach (var root in roots)
            {
                yield return Path.Combine(root, "CbtKiosk", ExeName);

                foreach (var profile in UserProfiles())
                {
                    yield return Path.Combine(root, "Users", profile, "Desktop", ExeName);
                    yield return Path.Combine(root, "Users", profile, "Downloads", ExeName);
                    yield return Path.Combine(root, "Users", profile, ExeName);
                }
            }

            // Other ProgramData / AppData copies, just in case.
            yield return Path.Combine(SettingsStore.SharedDirectory, ExeName);
            yield return Path.Combine(SettingsStore.UserDirectory, ExeName);
        }

        private static IEnumerable<string> UserProfiles()
        {
            var names = new List<string>();
            try
            {
                var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Users");
                if (Directory.Exists(usersRoot))
                {
                    foreach (var dir in Directory.GetDirectories(usersRoot))
                    {
                        var name = Path.GetFileName(dir);
                        // Skip system profiles that never hold the file; keeps the loop short.
                        if (string.Equals(name, "Public", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Default User", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "All Users", StringComparison.OrdinalIgnoreCase))
                            continue;
                        names.Add(name);
                    }
                }
            }
            catch { /* ignore */ }
            return names;
        }
    }
}
