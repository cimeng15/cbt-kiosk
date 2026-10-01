using System;
using System.IO;
using System.Windows.Forms;

namespace CbtKioskTool
{
    /// <summary>
    /// Entry point of setting-CBT.exe - the SMALL settings app for CBT Kiosk.
    ///
    /// It replaces CbtKioskSetup.exe (a ~63 MB self-contained .NET 8 app that embedded the whole
    /// WebView2 runtime even though it never showed a web page). This tool targets the .NET
    /// Framework 4.6.2 that ships with Windows 10/11, so the single file is a few hundred KB and
    /// still runs without installing anything extra.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
                MessageBox.Show("Terjadi kesalahan:\r\n\r\n" + e.Exception.Message, Kiosk.Product,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                MessageBox.Show("Terjadi kesalahan:\r\n\r\n" + e.ExceptionObject, Kiosk.Product,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "-?") || HasFlag(args, "/?"))
            {
                MessageBox.Show(HelpText(), Kiosk.Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Remember an explicit kiosk location given on the command line.
            var kioskArg = ValueAfter(args, "--kiosk");
            if (!string.IsNullOrWhiteSpace(kioskArg)) SettingsStore.SaveKioskExePath(kioskArg);

            if (HasFlag(args, "--reset")) ResetSettings();

            Application.Run(new MainForm());
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ValueAfter(string[] args, string flag)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static void ResetSettings()
        {
            var answer = MessageBox.Show(
                "Hapus berkas pengaturan CBT Kiosk dan kembali ke pengaturan bawaan?\r\n\r\n" +
                SettingsStore.SharedPath + "\r\n" + SettingsStore.UserPath,
                Kiosk.Product, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            foreach (var path in new[] { SettingsStore.SharedPath, SettingsStore.UserPath })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Tidak dapat menghapus " + path + ":\r\n" + ex.Message, Kiosk.Product,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static string HelpText()
        {
            return
                "Aplikasi pengaturan RINGAN untuk CBT Kiosk (mode ujian).\r\n" +
                "\r\n" +
                "Ukurannya hanya beberapa ratus KB dan memakai .NET Framework yang sudah ada\r\n" +
                "di Windows 10/11 - tidak perlu memasang apa pun.\r\n" +
                "\r\n" +
                "CARA PAKAI\r\n" +
                "  1. Klik kanan aplikasi ini -> Run as administrator (untuk menyimpan pengaturan\r\n" +
                "     bersama semua pengguna di %ProgramData%).\r\n" +
                "  2. Isi alamat ujian, password darurat offline, dan opsi penguncian.\r\n" +
                "  3. Klik Simpan, lalu salin CbtKiosk.exe ke komputer siswa.\r\n" +
                "\r\n" +
                "BERKAS PENGATURAN (sama persis dengan yang dibaca CbtKiosk.exe)\r\n" +
                "  Semua pengguna : " + SettingsStore.SharedPath + "\r\n" +
                "  Pengguna ini   : " + SettingsStore.UserPath + "\r\n" +
                "\r\n" +
                "ARGUMEN\r\n" +
                "  --kiosk PATH   tentukan lokasi CbtKiosk.exe\r\n" +
                "  --reset        hapus pengaturan (kembali ke bawaan)\r\n" +
                "  --help         tampilkan bantuan ini\r\n" +
                "\r\n" +
                "PASSWORD DARURAT OFFLINE\r\n" +
                "  Selalu diterima untuk keluar dari mode ujian - terutama saat internet/server\r\n" +
                "  mati ATAU password online kedaluwarsa. Bila belum diubah, yang berlaku adalah\r\n" +
                "  bawaan pabrik: \"" + Kiosk.DefaultOfflinePassword + "\"\r\n" +
                "\r\n" +
                "  Sebaiknya ganti bawaan itu sebelum ujian, karena tercantum di dokumentasi.";
        }
    }
}
