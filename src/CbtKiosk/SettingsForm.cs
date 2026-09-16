using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CbtKiosk;

/// <summary>
/// Configuration window. Reachable in two ways:
///   * as an administrator:  CbtKiosk.exe --settings   (recommended; writes the shared file)
///   * from the running kiosk: tray icon -> "Buka Pengaturan" (asks for the settings password).
///
/// Layout note: the settings fields live in a scrollable panel and the Save / Cancel buttons sit
/// in a bar pinned to the bottom of the window. This guarantees the Save button is ALWAYS visible
/// and every field is reachable, regardless of screen resolution or DPI scaling (previously the
/// buttons were placed below the visible area and got cut off).
/// </summary>
public sealed class SettingsForm : Form
{
    private const int BottomBarHeight = 58;

    private readonly AppSettings _settings;
    private readonly bool _standalone;

    private Panel _content;
    private Panel _bottomBar;

    private TextBox _txtBaseUrl, _txtStartUrl, _txtKioskUrl, _txtFallbackUrl;
    private NumericUpDown _numTimeout, _numAttempts, _numInterval;
    private TextBox _txtOfflinePwd, _txtOfflinePwd2;
    private TextBox _txtSettingsPwd, _txtSettingsPwd2;
    private CheckBox _chkBlockNav, _chkAllowZoom, _chkAllowBack, _chkClearSession;
    private CheckBox _chkAutoStart, _chkAutoStartAllUsers;
    private Label _lblOfflineState, _lblSettingsPwdState, _lblSavePath, _lblTest, _lblAutoStartState;
    private Button _btnClearOffline, _btnClearSettingsPwd;
    private Button _btnSave, _btnCancel, _btnOpenLog;

    public SettingsForm(AppSettings settings, bool standalone)
    {
        _settings = settings;
        _standalone = standalone;

        Text = "CBT Kiosk - Pengaturan";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(760, 680);
        MinimumSize = new Size(560, 420);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(7F, 15F);

        BuildUi();
        LoadValues();
        LayoutBottomBar();
    }

    private void BuildUi()
    {
        // ---- Shell: scrollable content + pinned bottom bar --------------------
        _content = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, ClientSize.Height - BottomBarHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AutoScroll = true,
            BackColor = Color.White,
        };

        _bottomBar = new Panel
        {
            Location = new Point(0, ClientSize.Height - BottomBarHeight),
            Size = new Size(ClientSize.Width, BottomBarHeight),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(244, 246, 249),
        };
        _bottomBar.Resize += (s, e) => LayoutBottomBar();

        Controls.Add(_content);
        Controls.Add(_bottomBar);

        void Add(Control c) => _content.Controls.Add(c);

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
        Add(Header("Alamat ujian", y)); y += 26;

        Add(Caption("URL ujian (dibuka):", y));
        _txtStartUrl = new TextBox { Location = new Point(170, y), Width = 430 };
        Add(_txtStartUrl); y += 28;

        Add(Caption("Base URL (domain):", y));
        _txtBaseUrl = new TextBox { Location = new Point(170, y), Width = 430 };
        Add(_txtBaseUrl); y += 28;

        Add(Caption("Endpoint password:", y));
        _txtKioskUrl = new TextBox { Location = new Point(170, y), Width = 330 };
        Add(_txtKioskUrl);
        var btnTest = new Button { Text = "Tes koneksi", Location = new Point(508, y - 1), Size = new Size(100, 24) };
        btnTest.Click += BtnTest_Click;
        Add(btnTest); y += 28;

        Add(Caption("Fallback online:", y));
        _txtFallbackUrl = new TextBox { Location = new Point(170, y), Width = 430 };
        Add(_txtFallbackUrl); y += 22;
        Add(new Label
        {
            Text = "Opsional. Dipakai bila endpoint utama tidak terjangkau.",
            Location = new Point(170, y), Size = new Size(430, 18), ForeColor = Color.Gray,
        }); y += 30;

        _lblTest = new Label { Location = new Point(170, y), Size = new Size(430, 20), ForeColor = Color.DimGray };
        Add(_lblTest); y += 30;

        // ---- Request tuning ----------------------------------------------------
        Add(Header("Permintaan password ke server", y)); y += 26;

        Add(Caption("Timeout (ms):", y));
        _numTimeout = new NumericUpDown { Location = new Point(170, y), Width = 90, Minimum = 500, Maximum = 60000, Increment = 500 };
        Add(_numTimeout);

        Add(new Label { Text = "Percobaan:", Location = new Point(285, y + 3), AutoSize = true });
        _numAttempts = new NumericUpDown { Location = new Point(350, y), Width = 60, Minimum = 1, Maximum = 20 };
        Add(_numAttempts);

        Add(new Label { Text = "Jeda (ms):", Location = new Point(430, y + 3), AutoSize = true });
        _numInterval = new NumericUpDown { Location = new Point(500, y), Width = 80, Minimum = 0, Maximum = 30000, Increment = 100 };
        Add(_numInterval); y += 34;

        // ---- Offline fallback password ----------------------------------------
        Add(Header("Password cadangan offline", y)); y += 24;
        Add(new Label
        {
            Text = "Dipakai HANYA bila server tidak dapat dihubungi (internet terputus).\n" +
                   "Kosongkan bila tidak diperlukan.",
            Location = new Point(16, y), Size = new Size(600, 34), ForeColor = Color.DimGray,
        }); y += 38;

        _lblOfflineState = new Label { Location = new Point(170, y), Size = new Size(330, 20), ForeColor = Color.DarkGreen };
        Add(_lblOfflineState);
        _btnClearOffline = new Button { Text = "Hapus", Location = new Point(520, y - 3), Size = new Size(80, 24) };
        _btnClearOffline.Click += BtnClearOffline_Click;
        Add(_btnClearOffline); y += 26;

        Add(Caption("Password baru:", y));
        _txtOfflinePwd = new TextBox { Location = new Point(170, y), Width = 430, UseSystemPasswordChar = true };
        Add(_txtOfflinePwd); y += 28;

        Add(Caption("Ulangi password:", y));
        _txtOfflinePwd2 = new TextBox { Location = new Point(170, y), Width = 430, UseSystemPasswordChar = true };
        Add(_txtOfflinePwd2); y += 34;

        // ---- Settings password -------------------------------------------------
        Add(Header("Password pengaturan", y)); y += 24;
        Add(new Label
        {
            Text = "Melindungi tombol 'Buka Pengaturan' dari siswa. Kosongkan bila tidak perlu.",
            Location = new Point(16, y), Size = new Size(600, 18), ForeColor = Color.DimGray,
        }); y += 24;

        _lblSettingsPwdState = new Label { Location = new Point(170, y), Size = new Size(330, 20), ForeColor = Color.DarkGreen };
        Add(_lblSettingsPwdState);
        _btnClearSettingsPwd = new Button { Text = "Hapus", Location = new Point(520, y - 3), Size = new Size(80, 24) };
        _btnClearSettingsPwd.Click += BtnClearSettingsPwd_Click;
        Add(_btnClearSettingsPwd); y += 26;

        Add(Caption("Password baru:", y));
        _txtSettingsPwd = new TextBox { Location = new Point(170, y), Width = 430, UseSystemPasswordChar = true };
        Add(_txtSettingsPwd); y += 28;

        Add(Caption("Ulangi password:", y));
        _txtSettingsPwd2 = new TextBox { Location = new Point(170, y), Width = 430, UseSystemPasswordChar = true };
        Add(_txtSettingsPwd2); y += 34;

        // ---- Lockdown options --------------------------------------------------
        Add(Header("Kunci penguncian (lockdown)", y)); y += 24;
        Add(new Label
        {
            Text = "Pintasan: F5 = muat ulang, Ctrl+Alt+Q = keluar (keduanya selalu aktif).",
            Location = new Point(16, y), Size = new Size(600, 18), ForeColor = Color.DimGray,
        }); y += 26;
        _chkBlockNav = new CheckBox { Text = "Blokir tombol Windows / Alt+Tab / Alt+F4 / Esc", Location = new Point(16, y), AutoSize = true };
        Add(_chkBlockNav); y += 24;
        _chkAllowZoom = new CheckBox { Text = "Izinkan zoom (Ctrl +/-)", Location = new Point(16, y), AutoSize = true };
        Add(_chkAllowZoom); y += 24;
        _chkAllowBack = new CheckBox { Text = "Izinkan tombol kembali (Backspace)", Location = new Point(16, y), AutoSize = true };
        Add(_chkAllowBack); y += 24;
        _chkClearSession = new CheckBox
        {
            Text = "Hapus sesi/cookies saat keluar (siswa harus login lagi)",
            Location = new Point(16, y),
            AutoSize = true,
        };
        Add(_chkClearSession); y += 30;

        // ---- Startup -----------------------------------------------------------
        Add(Header("Saat Windows menyala (startup)", y)); y += 24;
        Add(new Label
        {
            Text = "Jalankan aplikasi ini otomatis saat Windows login, sehingga siswa tidak perlu membukanya.",
            Location = new Point(16, y), Size = new Size(600, 18), ForeColor = Color.DimGray,
        }); y += 24;

        _chkAutoStart = new CheckBox
        {
            Text = "Jalankan otomatis saat Windows menyala",
            Location = new Point(16, y),
            AutoSize = true,
        };
        _chkAutoStart.CheckedChanged += (s, e) =>
        {
            _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked;
        };
        Add(_chkAutoStart); y += 24;

        _chkAutoStartAllUsers = new CheckBox
        {
            Text = "Untuk SEMUA pengguna Windows (perlu dijalankan sebagai Administrator)",
            Location = new Point(34, y),
            AutoSize = true,
        };
        Add(_chkAutoStartAllUsers); y += 24;

        _lblAutoStartState = new Label { Location = new Point(34, y), Size = new Size(600, 20), ForeColor = Color.DimGray };
        Add(_lblAutoStartState); y += 30;

        // ---- Footer info -------------------------------------------------------
        _lblSavePath = new Label { Location = new Point(16, y), Size = new Size(660, 36), ForeColor = Color.DimGray };
        Add(_lblSavePath); y += 42;

        // Keep a little breathing room at the bottom of the scrollable area.
        _content.AutoScrollMinSize = new Size(0, y + 8);

        // ---- Bottom bar (always visible) --------------------------------------
        _btnOpenLog = new Button { Text = "Buka folder log", Size = new Size(150, 34) };
        _btnOpenLog.Click += (s, e) => OpenFolder(Logger.LogDirectory);
        _bottomBar.Controls.Add(_btnOpenLog);

        _btnSave = new Button
        {
            Text = "Simpan",
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;
        _bottomBar.Controls.Add(_btnSave);

        _btnCancel = new Button { Text = "Batal", Size = new Size(90, 34) };
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        _bottomBar.Controls.Add(_btnCancel);

        CancelButton = _btnCancel;
    }

    /// <summary>Pins the bottom-bar buttons: log folder on the left, Save / Cancel on the right.</summary>
    private void LayoutBottomBar()
    {
        if (_bottomBar == null || _btnSave == null) return;

        var y = Math.Max(0, (_bottomBar.Height - _btnSave.Height) / 2);
        _btnCancel.Location = new Point(_bottomBar.Width - 14 - _btnCancel.Width, y);
        _btnSave.Location = new Point(_btnCancel.Left - 10 - _btnSave.Width, y);
        _btnOpenLog.Location = new Point(14, y);
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
        _chkAllowZoom.Checked = _settings.AllowZoom;
        _chkAllowBack.Checked = _settings.AllowBackNavigation;
        _chkClearSession.Checked = _settings.ClearSessionOnQuit;

        UpdateOfflineState();
        UpdateSettingsPwdState();

        // Reflect the real registry state (the source of truth for auto-start).
        _chkAutoStart.Checked = AutoStart.IsEnabled || _settings.AutoStart;
        _chkAutoStartAllUsers.Checked = AutoStart.IsEnabledForAllUsers || _settings.AutoStartAllUsers;
        _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked;
        UpdateAutoStartState();

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
    }

    private void UpdateSettingsPwdState()
    {
        var set = !string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash);
        _lblSettingsPwdState.Text = set ? "Status: SUDAH diatur." : "Status: belum diatur (bebas dibuka).";
        _lblSettingsPwdState.ForeColor = set ? Color.DarkGreen : Color.DimGray;
    }

    private void UpdateAutoStartState()
    {
        _lblAutoStartState.Text = "Status registry: " + AutoStart.Describe();
        _lblAutoStartState.ForeColor = AutoStart.IsEnabled ? Color.DarkGreen : Color.DimGray;
    }

    private void BtnClearOffline_Click(object sender, EventArgs e)
    {
        if (!_settings.HasOfflineFallback &&
            string.IsNullOrEmpty(_txtOfflinePwd.Text) && string.IsNullOrEmpty(_txtOfflinePwd2.Text))
        {
            MessageBox.Show(this, "Belum ada password cadangan offline yang diatur.",
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settings.OfflineFallbackPasswordHash = "";
        _txtOfflinePwd.Text = _txtOfflinePwd2.Text = "";
        UpdateOfflineState();
    }

    private void BtnClearSettingsPwd_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash) &&
            string.IsNullOrEmpty(_txtSettingsPwd.Text) && string.IsNullOrEmpty(_txtSettingsPwd2.Text))
        {
            MessageBox.Show(this, "Belum ada password pengaturan yang diatur.",
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settings.SettingsPasswordHash = "";
        _txtSettingsPwd.Text = _txtSettingsPwd2.Text = "";
        UpdateSettingsPwdState();
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
        _settings.AllowZoom = _chkAllowZoom.Checked;
        _settings.AllowBackNavigation = _chkAllowBack.Checked;
        _settings.ClearSessionOnQuit = _chkClearSession.Checked;
        _settings.AutoStart = _chkAutoStart.Checked;
        _settings.AutoStartAllUsers = _chkAutoStartAllUsers.Checked;

        try
        {
            var path = _settings.Save();

            // Apply the auto-start setting to the registry (the real mechanism Windows reads).
            var autoStartOk = AutoStart.Apply(_chkAutoStart.Checked, _chkAutoStartAllUsers.Checked, out var autoStartError);

            UpdateOfflineState();
            UpdateSettingsPwdState();
            UpdateAutoStartState();
            _lblSavePath.Text = $"Disimpan di: {path}\nWebView2 Runtime: {GetWebView2Version()}";

            var message = "Pengaturan tersimpan di:\n" + path +
                (_standalone ? "\n\nJalankan CbtKiosk.exe (tanpa argumen) untuk memulai mode ujian." : "");

            if (!autoStartOk)
            {
                message += "\n\nPERINGATAN: pengaturan auto-start gagal diterapkan.\n" + autoStartError +
                    (_chkAutoStartAllUsers.Checked
                        ? "\n\nJalankan aplikasi sebagai Administrator untuk opsi 'semua pengguna'."
                        : "");
            }

            MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK,
                autoStartOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
