using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CbtKiosk;

/// <summary>
/// Configuration window. Reachable in two ways:
///   * as an administrator:  CbtKiosk.exe --settings   (recommended; writes the shared file)
///   * from the running kiosk: tray icon -> "Buka Pengaturan" (asks for the settings password).
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly bool _standalone;

    private TextBox _txtBaseUrl, _txtStartUrl, _txtKioskUrl, _txtFallbackUrl;
    private NumericUpDown _numTimeout, _numAttempts, _numInterval;
    private TextBox _txtOfflinePwd, _txtOfflinePwd2;
    private TextBox _txtSettingsPwd, _txtSettingsPwd2;
    private CheckBox _chkBlockNav, _chkAllowReload, _chkAllowZoom, _chkAllowBack, _chkClearSession;
    private Label _lblOfflineState, _lblSettingsPwdState, _lblSavePath, _lblTest;
    private Button _btnClearOffline, _btnClearSettingsPwd;

    public SettingsForm(AppSettings settings, bool standalone)
    {
        _settings = settings;
        _standalone = standalone;

        Text = "CBT Kiosk - Pengaturan";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(640, 648);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        var y = 14;

        Label Header(string text, int top)
        {
            var l = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 10.5F),
                ForeColor = Color.FromArgb(0, 90, 158),
                Location = new Point(16, top),
                AutoSize = true,
            };
            return l;
        }

        Label Caption(string text, int top, int left = 16, int width = 150)
            => new() { Text = text, Location = new Point(left, top + 3), Size = new Size(width, 20), TextAlign = ContentAlignment.MiddleLeft };

        // ---- URLs -------------------------------------------------------------
        Controls.Add(Header("Alamat ujian", y)); y += 26;

        Controls.Add(Caption("URL ujian (dibuka):", y));
        _txtStartUrl = new TextBox { Location = new Point(175, y), Width = 445 };
        Controls.Add(_txtStartUrl); y += 28;

        Controls.Add(Caption("Base URL (domain):", y));
        _txtBaseUrl = new TextBox { Location = new Point(175, y), Width = 445 };
        Controls.Add(_txtBaseUrl); y += 28;

        Controls.Add(Caption("Endpoint password:", y));
        _txtKioskUrl = new TextBox { Location = new Point(175, y), Width = 340 };
        Controls.Add(_txtKioskUrl);
        var btnTest = new Button { Text = "Tes koneksi", Location = new Point(522, y - 1), Size = new Size(98, 24) };
        btnTest.Click += BtnTest_Click;
        Controls.Add(btnTest); y += 28;

        Controls.Add(Caption("Fallback online:", y));
        _txtFallbackUrl = new TextBox { Location = new Point(175, y), Width = 445 };
        Controls.Add(_txtFallbackUrl); y += 22;
        Controls.Add(new Label
        {
            Text = "Opsional. Dipakai bila endpoint utama tidak terjangkau.",
            Location = new Point(175, y), Size = new Size(445, 18), ForeColor = Color.Gray,
        }); y += 30;

        _lblTest = new Label { Location = new Point(175, y), Size = new Size(445, 20), ForeColor = Color.DimGray };
        Controls.Add(_lblTest); y += 30;

        // ---- Request tuning ----------------------------------------------------
        Controls.Add(Header("Permintaan password ke server", y)); y += 26;

        Controls.Add(Caption("Timeout (ms):", y));
        _numTimeout = new NumericUpDown { Location = new Point(175, y), Width = 90, Minimum = 500, Maximum = 60000, Increment = 500 };
        Controls.Add(_numTimeout);

        Controls.Add(new Label { Text = "Percobaan:", Location = new Point(290, y + 3), AutoSize = true });
        _numAttempts = new NumericUpDown { Location = new Point(360, y), Width = 60, Minimum = 1, Maximum = 20 };
        Controls.Add(_numAttempts);

        Controls.Add(new Label { Text = "Jeda (ms):", Location = new Point(440, y + 3), AutoSize = true });
        _numInterval = new NumericUpDown { Location = new Point(510, y), Width = 80, Minimum = 0, Maximum = 30000, Increment = 100 };
        Controls.Add(_numInterval); y += 34;

        // ---- Offline fallback password ----------------------------------------
        Controls.Add(Header("Password cadangan offline", y)); y += 24;
        Controls.Add(new Label
        {
            Text = "Dipakai HANYA bila server tidak dapat dihubungi (internet terputus).\n" +
                   "Kosongkan bila tidak diperlukan.",
            Location = new Point(16, y), Size = new Size(600, 34), ForeColor = Color.DimGray,
        }); y += 38;

        _lblOfflineState = new Label { Location = new Point(175, y), Size = new Size(300, 20), ForeColor = Color.DarkGreen };
        Controls.Add(_lblOfflineState);
        _btnClearOffline = new Button { Text = "Hapus", Location = new Point(500, y - 3), Size = new Size(80, 24) };
        _btnClearOffline.Click += (s, e) =>
        {
            _settings.OfflineFallbackPasswordHash = "";
            _txtOfflinePwd.Text = _txtOfflinePwd2.Text = "";
            UpdateOfflineState();
        };
        Controls.Add(_btnClearOffline); y += 26;

        Controls.Add(Caption("Password baru:", y));
        _txtOfflinePwd = new TextBox { Location = new Point(175, y), Width = 445, UseSystemPasswordChar = true };
        Controls.Add(_txtOfflinePwd); y += 28;

        Controls.Add(Caption("Ulangi password:", y));
        _txtOfflinePwd2 = new TextBox { Location = new Point(175, y), Width = 445, UseSystemPasswordChar = true };
        Controls.Add(_txtOfflinePwd2); y += 34;

        // ---- Settings password -------------------------------------------------
        Controls.Add(Header("Password pengaturan", y)); y += 24;
        Controls.Add(new Label
        {
            Text = "Melindungi tombol 'Buka Pengaturan' dari siswa. Kosongkan bila tidak perlu.",
            Location = new Point(16, y), Size = new Size(600, 18), ForeColor = Color.DimGray,
        }); y += 24;

        _lblSettingsPwdState = new Label { Location = new Point(175, y), Size = new Size(300, 20), ForeColor = Color.DarkGreen };
        Controls.Add(_lblSettingsPwdState);
        _btnClearSettingsPwd = new Button { Text = "Hapus", Location = new Point(500, y - 3), Size = new Size(80, 24) };
        _btnClearSettingsPwd.Click += (s, e) =>
        {
            _settings.SettingsPasswordHash = "";
            _txtSettingsPwd.Text = _txtSettingsPwd2.Text = "";
            UpdateSettingsPwdState();
        };
        Controls.Add(_btnClearSettingsPwd); y += 26;

        Controls.Add(Caption("Password baru:", y));
        _txtSettingsPwd = new TextBox { Location = new Point(175, y), Width = 445, UseSystemPasswordChar = true };
        Controls.Add(_txtSettingsPwd); y += 28;

        Controls.Add(Caption("Ulangi password:", y));
        _txtSettingsPwd2 = new TextBox { Location = new Point(175, y), Width = 445, UseSystemPasswordChar = true };
        Controls.Add(_txtSettingsPwd2); y += 34;

        // ---- Lockdown options --------------------------------------------------
        Controls.Add(Header("Kunci penguncian (lockdown)", y)); y += 26;
        _chkBlockNav = new CheckBox { Text = "Blokir tombol Windows / Alt+Tab / Alt+F4 / Esc", Location = new Point(16, y), AutoSize = true };
        Controls.Add(_chkBlockNav); y += 24;
        _chkAllowReload = new CheckBox { Text = "Izinkan muat ulang (F5 / Ctrl+R)", Location = new Point(16, y), AutoSize = true };
        Controls.Add(_chkAllowReload); y += 24;
        _chkAllowZoom = new CheckBox { Text = "Izinkan zoom (Ctrl +/-)", Location = new Point(16, y), AutoSize = true };
        Controls.Add(_chkAllowZoom); y += 24;
        _chkAllowBack = new CheckBox { Text = "Izinkan tombol kembali (Backspace)", Location = new Point(16, y), AutoSize = true };
        Controls.Add(_chkAllowBack); y += 24;
        _chkClearSession = new CheckBox
        {
            Text = "Hapus sesi/cookies saat keluar (siswa harus login lagi)",
            Location = new Point(16, y),
            AutoSize = true,
        };
        Controls.Add(_chkClearSession); y += 30;

        // ---- Footer ------------------------------------------------------------
        _lblSavePath = new Label { Location = new Point(16, y), Size = new Size(600, 34), ForeColor = Color.DimGray };
        Controls.Add(_lblSavePath); y += 40;

        var btnOpenLog = new Button { Text = "Buka folder log", Location = new Point(16, y), Size = new Size(120, 30) };
        btnOpenLog.Click += (s, e) => OpenFolder(Logger.LogDirectory);
        Controls.Add(btnOpenLog);

        var btnSave = new Button { Text = "Simpan", Location = new Point(430, y), Size = new Size(100, 30) };
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);

        var btnCancel = new Button { Text = "Batal", Location = new Point(536, y), Size = new Size(84, 30) };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(btnCancel);
        CancelButton = btnCancel;
    }

    private void LoadValues()
    {
        _txtBaseUrl.Text = _settings.BaseUrl;
        _txtStartUrl.Text = _settings.StartUrl;
        _txtKioskUrl.Text = _settings.KioskUrl;
        _txtFallbackUrl.Text = _settings.FallbackUrl;
        _numTimeout.Value = Math.Clamp(_settings.KioskTimeoutMs, 500, 60000);
        _numAttempts.Value = Math.Clamp(_settings.KioskAttempts, 1, 20);
        _numInterval.Value = Math.Clamp(_settings.KioskAttemptIntervalMs, 0, 30000);
        _chkBlockNav.Checked = _settings.BlockNavigationKeys;
        _chkAllowReload.Checked = _settings.AllowReload;
        _chkAllowZoom.Checked = _settings.AllowZoom;
        _chkAllowBack.Checked = _settings.AllowBackNavigation;
        _chkClearSession.Checked = _settings.ClearSessionOnQuit;

        UpdateOfflineState();
        UpdateSettingsPwdState();

        var webview = GetWebView2Version();
        _lblSavePath.Text =
            $"Disimpan di: {_settings.SettingsPath}\n" +
            $"WebView2 Runtime: {webview}";
    }

    private void UpdateOfflineState()
    {
        var set = _settings.HasOfflineFallback;
        _lblOfflineState.Text = set ? "Status: SUDAH diatur (hash tersimpan)." : "Status: belum diatur.";
        _lblOfflineState.ForeColor = set ? Color.DarkGreen : Color.DimGray;
        _btnClearOffline.Enabled = set;
    }

    private void UpdateSettingsPwdState()
    {
        var set = !string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash);
        _lblSettingsPwdState.Text = set ? "Status: SUDAH diatur." : "Status: belum diatur (bebas dibuka).";
        _lblSettingsPwdState.ForeColor = set ? Color.DarkGreen : Color.DimGray;
        _btnClearSettingsPwd.Enabled = set;
    }

    private void BtnTest_Click(object sender, EventArgs e)
    {
        _lblTest.Text = "Menguji\u2026";
        _lblTest.ForeColor = Color.DimGray;
        Application.DoEvents();

        var probe = new AppSettings
        {
            KioskUrl = _txtKioskUrl.Text.Trim(),
            KioskTimeoutMs = (int)_numTimeout.Value,
            KioskAttempts = Math.Max(1, (int)_numAttempts.Value),
            KioskAttemptIntervalMs = (int)_numInterval.Value,
        };
        var client = new KioskClient(probe);
        var ok = client.TestConnection(probe.KioskUrl, out var message);
        _lblTest.Text = message;
        _lblTest.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
    }

    private void BtnSave_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtStartUrl.Text) || string.IsNullOrWhiteSpace(_txtBaseUrl.Text))
        {
            MessageBox.Show(this, "URL ujian dan Base URL tidak boleh kosong.", "Pengaturan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Offline fallback password: only change when something was typed.
        if (!string.IsNullOrEmpty(_txtOfflinePwd.Text) || !string.IsNullOrEmpty(_txtOfflinePwd2.Text))
        {
            if (_txtOfflinePwd.Text != _txtOfflinePwd2.Text)
            {
                MessageBox.Show(this, "Password cadangan offline tidak sama.", "Pengaturan",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _settings.OfflineFallbackPasswordHash = Hash.Sha256Hex(_txtOfflinePwd.Text);
        }

        // Settings password: only change when something was typed.
        if (!string.IsNullOrEmpty(_txtSettingsPwd.Text) || !string.IsNullOrEmpty(_txtSettingsPwd2.Text))
        {
            if (_txtSettingsPwd.Text != _txtSettingsPwd2.Text)
            {
                MessageBox.Show(this, "Password pengaturan tidak sama.", "Pengaturan",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _settings.SettingsPasswordHash = Hash.Sha256Hex(_txtSettingsPwd.Text);
        }

        _settings.BaseUrl = _txtBaseUrl.Text.Trim();
        _settings.StartUrl = _txtStartUrl.Text.Trim();
        _settings.KioskUrl = _txtKioskUrl.Text.Trim();
        _settings.FallbackUrl = _txtFallbackUrl.Text.Trim();
        _settings.KioskTimeoutMs = (int)_numTimeout.Value;
        _settings.KioskAttempts = (int)_numAttempts.Value;
        _settings.KioskAttemptIntervalMs = (int)_numInterval.Value;
        _settings.BlockNavigationKeys = _chkBlockNav.Checked;
        _settings.AllowReload = _chkAllowReload.Checked;
        _settings.AllowZoom = _chkAllowZoom.Checked;
        _settings.AllowBackNavigation = _chkAllowBack.Checked;
        _settings.ClearSessionOnQuit = _chkClearSession.Checked;

        try
        {
            var path = _settings.Save();
            MessageBox.Show(this,
                "Pengaturan tersimpan di:\n" + path +
                (_standalone ? "\n\nJalankan CbtKiosk.exe (tanpa argumen) untuk memulai mode ujian." : ""),
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Gagal menyimpan pengaturan.\n\n" + ex.Message +
                "\n\nJalankan aplikasi sebagai Administrator, atau gunakan CbtKiosk.exe --settings.",
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetWebView2Version()
    {
        try
        {
            var v = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrWhiteSpace(v) ? "TIDAK terpasang" : v;
        }
        catch
        {
            return "TIDAK terpasang";
        }
    }

    private static void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Tidak dapat membuka folder: " + ex.Message); }
    }
}
