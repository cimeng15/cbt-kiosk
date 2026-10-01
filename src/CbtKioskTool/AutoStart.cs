using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace CbtKioskTool
{
    /// <summary>
    /// Auto-start registration, byte-for-byte compatible with AutoStart.cs in the kiosk:
    ///   * registry Run key (HKCU / HKLM) named "CbtKiosk"
    ///   * scheduled task "CbtKiosk" with an ONLOGON trigger
    /// </summary>
    internal static class AutoStart
    {
        private const string RunKeyUser = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunKeyMachine = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CbtKiosk";
        private const string TaskName = "CbtKiosk";

        /// <summary>When set, registrations point at this executable (CbtKiosk.exe) instead of this tool.</summary>
        public static string TargetExePath { get; set; }

        public static string CurrentExePath => System.Windows.Forms.Application.ExecutablePath;

        public static string EffectiveExePath =>
            string.IsNullOrWhiteSpace(TargetExePath) ? CurrentExePath : TargetExePath;

        private static string Command => "\"" + EffectiveExePath + "\"";

        // ------------------------------------------------------------------ registry

        public static bool IsEnabledForUser => HasRunValue(Registry.CurrentUser, RunKeyUser);
        public static bool IsEnabledForAllUsers => HasRunValue(Registry.LocalMachine, RunKeyMachine);

        private static bool HasRunValue(RegistryKey root, string subKey)
        {
            try
            {
                using (var key = root.OpenSubKey(subKey, false))
                    return key != null && !string.IsNullOrWhiteSpace(key.GetValue(ValueName) as string);
            }
            catch { return false; }
        }

        public static string RegisteredRunCommand()
        {
            foreach (var pair in new[] { new KeyValuePair<RegistryKey, string>(Registry.CurrentUser, RunKeyUser),
                                         new KeyValuePair<RegistryKey, string>(Registry.LocalMachine, RunKeyMachine) })
            {
                try
                {
                    using (var key = pair.Key.OpenSubKey(pair.Value, false))
                    {
                        if (key == null) continue;
                        var value = key.GetValue(ValueName) as string;
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static void SetRunValue(RegistryKey root, string subKey)
        {
            using (var key = root.CreateSubKey(subKey))
            {
                if (key == null) throw new InvalidOperationException("Tidak dapat membuka kunci registry.");
                key.SetValue(ValueName, Command, RegistryValueKind.String);
            }
        }

        private static void RemoveRunValue(RegistryKey root, string subKey)
        {
            try
            {
                using (var key = root.OpenSubKey(subKey, true))
                    if (key != null && key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
            }
            catch { /* ignore */ }
        }

        // ------------------------------------------------------------ scheduled task

        public static bool IsTaskEnabled()
        {
            var result = RunSchtasks("/Query", "/TN", TaskName);
            return result.Item1 == 0;
        }

        private static Tuple<int, string> RunSchtasks(params string[] args)
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
                foreach (var a in args) psi.Arguments += (psi.Arguments.Length == 0 ? "" : " ") + a;

                using (var p = Process.Start(psi))
                {
                    if (p == null) return Tuple.Create(-1, "schtasks.exe tidak dapat dijalankan.");
                    var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(20000)) return Tuple.Create(-1, "schtasks.exe timeout.");
                    return Tuple.Create(p.ExitCode, output.Trim());
                }
            }
            catch (Exception ex)
            {
                return Tuple.Create(-1, ex.Message);
            }
        }

        // ------------------------------------------------------------------- state

        public static bool IsEnabled => IsEnabledForUser || IsEnabledForAllUsers || IsTaskEnabled();

        public static string Describe()
        {
            var parts = new List<string>();
            if (IsEnabledForAllUsers) parts.Add("registry: semua pengguna");
            if (IsEnabledForUser) parts.Add("registry: pengguna ini");
            if (IsTaskEnabled()) parts.Add("scheduled task");
            return parts.Count == 0 ? "Tidak aktif." : "Aktif (" + string.Join(", ", parts) + ").";
        }

        /// <summary>Diagnostic text used by the "Periksa startup" button.</summary>
        public static string Verify()
        {
            var lines = new List<string>
            {
                "Path aplikasi yang didaftarkan:",
                "  " + EffectiveExePath,
                "  file ada: " + (File.Exists(EffectiveExePath) ? "YA" : "TIDAK"),
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
            }

            lines.Add("");
            var task = RunSchtasks("/Query", "/TN", TaskName);
            lines.Add(task.Item1 == 0 ? "Scheduled task: ADA" : "Scheduled task: tidak ada.");
            if (task.Item1 != 0 && !string.IsNullOrWhiteSpace(task.Item2))
                lines.Add("  (" + task.Item2.Split('\n')[0].Trim() + ")");

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static bool Apply(bool enabled, bool allUsers, string method, out string error)
        {
            error = null;
            var useTask = string.Equals(method, Kiosk.MethodTask, StringComparison.OrdinalIgnoreCase);

            try
            {
                if (!enabled)
                {
                    RemoveRunValue(Registry.CurrentUser, RunKeyUser);
                    RemoveRunValue(Registry.LocalMachine, RunKeyMachine);
                    var del = RunSchtasks("/Delete", "/TN", TaskName, "/F");
                    if (del.Item1 != 0 && IsTaskEnabled())
                    {
                        error = del.Item2;
                        return false;
                    }
                    return true;
                }

                if (useTask)
                {
                    RemoveRunValue(Registry.CurrentUser, RunKeyUser);
                    RemoveRunValue(Registry.LocalMachine, RunKeyMachine);

                    var args = new List<string> { "/Create", "/TN", TaskName, "/TR", Command, "/SC", "ONLOGON", "/F" };
                    if (allUsers) { args.Add("/RL"); args.Add("HIGHEST"); }

                    var created = RunSchtasks(args.ToArray());
                    if (created.Item1 != 0)
                    {
                        error = created.Item2;
                        return false;
                    }
                    return true;
                }

                var delete = RunSchtasks("/Delete", "/TN", TaskName, "/F");
                if (delete.Item1 != 0 && IsTaskEnabled())
                {
                    error = "Tidak dapat menghapus scheduled task lama: " + delete.Item2;
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
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
