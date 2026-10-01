using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CbtKiosk.Setup;

/// <summary>
/// The settings window of CbtKioskSetup.exe: a tabbed, self-explaining configuration app for the
/// CBT Kiosk, replacing the old "run CbtKiosk.exe --settings from a console" workflow.
/// </summary>
public sealed class SetupForm : Form
{
    private const int BottomBarHeight = 60;

    private readonly AppSettings _original;
    private AppSettings _settings;

    private Panel _banner;
    private TabControl _tabs;
    private Panel _bottomBar;

    // -- General tab
    private TextBox _txtStartUrl, _txtBaseUrl, _txtKioskUrl, _txtFallbackUrl;
    private NumericUpDown _numTimeout, _numAttempts, _numInterval;
    private Label _lblTest;

    // -- Security tab
    private TextBox _txtOfflinePwd, _txtOfflinePwd2;
    private TextBox _txtSettingsPwd, _txtSettingsPwd2;
    private CheckBox _chkShowPasswords, _chkDefaultEmergency;
    private Label _lblOfflineState, _lblSettingsPwdState;
    private Button _btnClearOffline, _btnClearSettingsPwd;
    private CheckBox _chkBlockNav, _chkAllowZoom, _chkAllowBack, _chkClearSession;

    // -- Startup tab
    private TextBox _txtKioskExe;
    private CheckBox _chkAutoStart, _chkAutoStartAllUsers;
    private ComboBox _cboStartupMethod;
    private Label _lblAutoStartState;
    private Button _btnBrowseKiosk, _btnVerifyStartup, _btnRunKiosk;

    // -- Bottom bar
    private Label _lblStatus, _lblSaved;
    private Button _btnSave, _btnCancel, _btnReload, _btnDefaults;

    public SetupForm()
    {
        _settings = AppSettings.Load();
        _original = _settings;

        Text = Program.AppTitle + (Program.IsAdministrator ? " (Administrator)" : "");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = true;
        ClientSize = new Size(860, 690);
        MinimumSize = new Size(700, 520);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(7F, 15F);
        Icon = SystemIcons.Shield;

        BuildUi();
        LoadValues();
        LayoutBottomBar();
    }

    // ===================================================================== UI

    private void BuildUi()
    {
        // Docking note: WinForms docks controls in reverse collection order, so the Fill control
        // must be added FIRST to receive the remaining space after the top banner and bottom bar.
        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 6),
            Font = new Font("Segoe UI", 9.5F),
        };
        Controls.Add(_tabs);

        _banner = BuildBanner();
        Controls.Add(_banner);

        _bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = BottomBarHeight,
            BackColor = Color.FromArgb(244, 246, 249),
        };
        _bottomBar.Resize += (_, _) => LayoutBottomBar();
        Controls.Add(_bottomBar);

        BuildGeneralTab();
        BuildSecurityTab();
        BuildStartupTab();
        BuildHelpTab();

        BuildBottomBar();
    }

    private Panel BuildBanner()
    {
        var banner = new Panel
        {
            Dock = DockStyle.Top,
            Height = Program.IsAdministrator ? 0 : 42,
            BackColor = Color.FromArgb(255, 249, 219),
        };

        if (Program.IsAdministrator) return banner;

        var label = new Label
        {
            Text = "Berjalan tanpa hak Administrator \u2014 pengaturan bersama (%ProgramData%) dan " +
                   "auto-start \"semua pengguna\" tidak dapat disimpan.",
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = Color.FromArgb(120, 80, 0),
        };
        banner.Controls.Add(label);

        var button = new Button
        {
            Text = "Jalankan sebagai Administrator",
            Size = new Size(210, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
        };
        button.Click += (_, _) =>
        {
            if (Program.RestartElevated()) Application.Exit();
            else MessageBox.Show(this, "Tidak dapat menjalankan sebagai Administrator.", "Pengaturan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        banner.Controls.Add(button);

        // Keep the button pinned to the right edge as the window is resized.
        void Place() => button.Location = new Point(Math.Max(220, banner.ClientSize.Width - button.Width - 14),
            (banner.ClientSize.Height - button.Height) / 2);
        banner.Resize += (_, _) => Place();
        Place();

        return banner;
    }

    private void BuildGeneralTab()
    {
        var page = NewPage("Umum & Koneksi");
        var l = new FormLayout(page);

        l.Header("Alamat aplikasi ujian");

        _txtStartUrl = l.Text("URL ujian (yang dibuka siswa):", _settings.StartUrl);
        _txtBaseUrl = l.Text("Base URL (domain diizinkan):", _settings.BaseUrl);
        _txtKioskUrl = l.Text("Endpoint password keluar:", _settings.KioskUrl);
        _txtFallbackUrl = l.Text("Fallback online (opsional):", _settings.FallbackUrl);
        l.Hint("Fallback dipakai bila endpoint utama tidak terjangkau.");

        var testRow = l.Row();
        var btnTest = new Button
        {
            Text = "Tes koneksi",
            Location = new Point(16, testRow),
            Size = new Size(120, 28),
        };
        btnTest.Click += BtnTest_Click;
        page.Controls.Add(btnTest);

        _lblTest = new Label
        {
            Location = new Point(146, testRow + 4),
            Size = new Size(660, 20),
            ForeColor = Color.DimGray,
        };
        page.Controls.Add(_lblTest);
        l.Advance(34);

        l.Header("Permintaan password ke server");
        var row = l.Row();

        page.Controls.Add(new Label { Text = "Timeout (ms):", Location = new Point(16, row + 4), AutoSize = true });
        _numTimeout = new NumericUpDown
        {
            Location = new Point(120, row), Width = 90,
            Minimum = 500, Maximum = 60000, Increment = 500,
        };
        page.Controls.Add(_numTimeout);

        page.Controls.Add(new Label { Text = "Percobaan:", Location = new Point(240, row + 4), AutoSize = true });
        _numAttempts = new NumericUpDown
        {
            Location = new Point(320, row), Width = 60, Minimum = 1, Maximum = 20,
        };
        page.Controls.Add(_numAttempts);

        page.Controls.Add(new Label { Text = "Jeda antar percobaan (ms):", Location = new Point(400, row + 4), AutoSize = true });
        _numInterval = new NumericUpDown
        {
            Location = new Point(580, row), Width = 90, Minimum = 0, Maximum = 30000, Increment = 100,
        };
        page.Controls.Add(_numInterval);
        l.Advance(34);

        l.Hint("Nilai bawaan sudah cocok untuk sebagian besar sekolah: 5000 ms, 3 percobaan, jeda 1000 ms.");
    }

    private void BuildSecurityTab()
    {
        var page = NewPage("Keamanan & Penguncian");
        var l = new FormLayout(page);

        l.Header("Password darurat (offline)");
        l.Hint("SELALU diterima untuk keluar - terutama bila internet/server mati ATAU password online\n" +
               "sudah kedaluwarsa. Ini jaring pengaman agar pengawas tidak pernah terkunci di luar.");

        _lblOfflineState = new Label { Location = new Point(16, l.Row()), Size = new Size(500, 34), ForeColor = Color.DimGray };
        page.Controls.Add(_lblOfflineState);
        _btnClearOffline = new Button { Text = "Hapus", Location = new Point(526, l.Row() - 3), Size = new Size(80, 26) };
        _btnClearOffline.Click += BtnClearOffline_Click;
        page.Controls.Add(_btnClearOffline);
        l.Advance(40);

        _txtOfflinePwd = l.Password("Password darurat baru:");
        _txtOfflinePwd2 = l.Password("Ulangi password darurat:");

        _chkDefaultEmergency = l.Check(
            $"Pakai password darurat BAWAAN \"{AppSettings.DefaultOfflinePassword}\" bila kolom di atas kosong", 30);
        l.Hint("Hilangkan centang ini bila kiosk hanya boleh dibuka dengan password dari panel CBT.");

        l.Header("Password pengaturan");
        l.Hint("Melindungi menu Pengaturan di dalam mode ujian agar tidak dibuka siswa.\n" +
               "Kosongkan bila tidak perlu.");

        _lblSettingsPwdState = new Label { Location = new Point(16, l.Row()), Size = new Size(430, 20), ForeColor = Color.DimGray };
        page.Controls.Add(_lblSettingsPwdState);
        _btnClearSettingsPwd = new Button { Text = "Hapus", Location = new Point(526, l.Row() - 3), Size = new Size(80, 26) };
        _btnClearSettingsPwd.Click += BtnClearSettingsPwd_Click;
        page.Controls.Add(_btnClearSettingsPwd);
        l.Advance(32);

        _txtSettingsPwd = l.Password("Password baru:");
        _txtSettingsPwd2 = l.Password("Ulangi password:");

        _chkShowPasswords = l.Check("Tampilkan password yang sedang diketik", 26);
        _chkShowPasswords.CheckedChanged += (_, _) =>
        {
            var reveal = _chkShowPasswords.Checked;
            foreach (var box in new[] { _txtOfflinePwd, _txtOfflinePwd2, _txtSettingsPwd, _txtSettingsPwd2 })
                box.UseSystemPasswordChar = !reveal;
        };

        l.Header("Penguncian (lockdown) saat ujian");
        l.Hint("Pintasan yang selalu aktif: F5 = muat ulang halaman, Ctrl+Alt+Q = keluar (minta password).");

        _chkBlockNav = l.Check("Blokir tombol Windows / Alt+Tab / Alt+F4 / Esc", 26);
        _chkAllowZoom = l.Check("Izinkan zoom (Ctrl + / Ctrl -)", 26);
        _chkAllowBack = l.Check("Izinkan tombol kembali (Backspace)", 26);
        _chkClearSession = l.Check("Hapus sesi/cookies saat keluar (siswa harus login ulang)", 26);
    }

    private void BuildStartupTab()
    {
        var page = NewPage("Startup & Aplikasi");
        var l = new FormLayout(page);

        l.Header("Aplikasi CBT Kiosk yang diatur");
        l.Hint("Semua pengaturan yang dibuat di sini berlaku untuk berkas CbtKiosk.exe ini.");

        var exeRow = l.Row();
        _txtKioskExe = new TextBox { Location = new Point(16, exeRow), Width = 620 };
        page.Controls.Add(_txtKioskExe);
        _btnBrowseKiosk = new Button { Text = "Pilih\u2026", Location = new Point(646, exeRow - 1), Size = new Size(80, 26) };
        _btnBrowseKiosk.Click += BtnBrowseKiosk_Click;
        page.Controls.Add(_btnBrowseKiosk);
        l.Advance(30);

        _btnRunKiosk = new Button { Text = "Jalankan CBT Kiosk", Location = new Point(16, l.Row()), Size = new Size(170, 30) };
        _btnRunKiosk.Click += BtnRunKiosk_Click;
        page.Controls.Add(_btnRunKiosk);

        var btnOpenFolder = new Button { Text = "Buka folder aplikasi", Location = new Point(196, l.Row()), Size = new Size(170, 30) };
        btnOpenFolder.Click += (_, _) => OpenFolder(Path.GetDirectoryName(_txtKioskExe.Text.Trim()));
        page.Controls.Add(btnOpenFolder);

        var btnOpenLog = new Button { Text = "Buka folder log", Location = new Point(376, l.Row()), Size = new Size(150, 30) };
        btnOpenLog.Click += (_, _) => OpenFolder(Logger.LogDirectory);
        page.Controls.Add(btnOpenLog);
        l.Advance(36);

        var webview = GetWebView2Version();
        page.Controls.Add(new Label
        {
            Text = "WebView2 Runtime di komputer ini: " + webview +
                   (webview == "TIDAK terpasang" ? "  (wajib dipasang di komputer siswa)" : ""),
            Location = new Point(16, l.Row()),
            Size = new Size(780, 20),
            ForeColor = webview == "TIDAK terpasang" ? Color.Firebrick : Color.Gray,
        });
        l.Advance(34);

        l.Header("Saat Windows menyala (auto-start)");
        l.Hint("Aplikasi CBT Kiosk terbuka sendiri saat Windows login, sehingga siswa tidak perlu membukanya.");

        _chkAutoStart = l.Check("Jalankan CBT Kiosk otomatis saat Windows menyala", 26);
        _chkAutoStart.CheckedChanged += (_, _) => _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked;

        _chkAutoStartAllUsers = l.Check("Untuk SEMUA pengguna Windows (perlu Administrator)", 26, indent: 24);

        var methodRow = l.Row();
        page.Controls.Add(new Label { Text = "Metode startup:", Location = new Point(40, methodRow + 4), AutoSize = true });
        _cboStartupMethod = new ComboBox
        {
            Location = new Point(180, methodRow),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _cboStartupMethod.Items.Add("Registry Run key (bawaan)");
        _cboStartupMethod.Items.Add("Scheduled Task (lebih andal)");
        page.Controls.Add(_cboStartupMethod);
        l.Advance(30);

        l.Hint("Bila auto-start tidak jalan (diblokir kebijakan sekolah), pilih \"Scheduled Task\".");

        _lblAutoStartState = new Label { Location = new Point(40, l.Row()), Size = new Size(500, 20), ForeColor = Color.DimGray };
        page.Controls.Add(_lblAutoStartState);
        _btnVerifyStartup = new Button { Text = "Periksa startup", Location = new Point(556, l.Row() - 3), Size = new Size(130, 26) };
        _btnVerifyStartup.Click += BtnVerifyStartup_Click;
        page.Controls.Add(_btnVerifyStartup);
        l.Advance(36);

        l.Header("Pemeliharaan");
        var maintRow = l.Row();
        var btnRestore = new Button { Text = "Kembalikan pengaturan bawaan", Location = new Point(16, maintRow), Size = new Size(230, 30) };
        btnRestore.Click += (_, _) => LoadDefaults();
        page.Controls.Add(btnRestore);

        var btnOpenSettingsFolder = new Button { Text = "Buka folder pengaturan", Location = new Point(256, maintRow), Size = new Size(190, 30) };
        btnOpenSettingsFolder.Click += (_, _) => OpenFolder(Path.GetDirectoryName(_settings.SettingsPath));
        page.Controls.Add(btnOpenSettingsFolder);
    }

    private void BuildHelpTab()
    {
        var page = NewPage("Bantuan");
        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9.5F),
            Text = HelpText(),
        };
        page.Controls.Add(text);
    }

    private string HelpText() =>
        "APA INI?\r\n" +
        "  Aplikasi untuk mengatur CBT Kiosk tanpa perlu menjalankan perintah\r\n" +
        "  \"CbtKiosk.exe --settings\" dari console.\r\n" +
        "\r\n" +
        "LANGKAH CEPAT\r\n" +
        "  1. Tab Umum: periksa URL ujian dan endpoint password, lalu klik Tes koneksi.\r\n" +
        "  2. Tab Keamanan: periksa password darurat offline dan password pengaturan.\r\n" +
        "  3. Tab Startup: tentukan lokasi CbtKiosk.exe dan centang auto-start bila ingin otomatis.\r\n" +
        "  4. Klik Simpan.\r\n" +
        "  5. Salin CbtKiosk.exe ke semua komputer siswa.\r\n" +
        "\r\n" +
        "DI MANA PENGATURAN DISIMPAN?\r\n" +
        "  Semua pengguna (disarankan, perlu Administrator):\r\n" +
        "    " + AppSettings.SharedPath + "\r\n" +
        "  Hanya pengguna ini (tanpa Administrator):\r\n" +
        "    " + AppSettings.UserPath + "\r\n" +
        "\r\n" +
        "PASSWORD\r\n" +
        "  - Password keluar diambil dari server CBT (panel pengawas) dan SELALU diutamakan.\r\n" +
        "  - Password darurat offline SELALU diterima juga - dipakai bila server mati,\r\n" +
        "    bila password online KEDALUWARSA, atau sebagai jaring pengaman terakhir.\r\n" +
        "  - Bila password darurat belum pernah diubah, yang berlaku adalah bawaan pabrik:\r\n" +
        "    \"" + AppSettings.DefaultOfflinePassword + "\" (dapat dimatikan di tab Keamanan).\r\n" +
        "  - Semua password disimpan sebagai hash SHA-256, bukan teks biasa:\r\n" +
        "    kolom password boleh dibiarkan kosong untuk mempertahankan yang lama.\r\n" +
        "\r\n" +
        "PINTASAN DI DALAM MODE UJIAN\r\n" +
        "  F5                = muat ulang halaman ujian\r\n" +
        "  Ctrl + Alt + Q    = keluar dari ujian (minta password)\r\n" +
        "\r\n" +
        "LOG\r\n" +
        "  " + Logger.LogDirectory + "\r\n" +
        "\r\n" +
        "BANTUAN ARGUMEN\r\n" +
        "  CbtKioskSetup.exe --kiosk \"C:\\CbtKiosk\\CbtKiosk.exe\"   tentukan lokasi CbtKiosk.exe\r\n" +
        "  CbtKioskSetup.exe --reset                              hapus pengaturan\r\n";

    // ===================================================================== bottom bar

    private void BuildBottomBar()
    {
        _lblStatus = new Label
        {
            Location = new Point(16, 8),
            Size = new Size(420, 20),
            ForeColor = Color.DimGray,
            AutoEllipsis = true,
        };
        _bottomBar.Controls.Add(_lblStatus);

        _lblSaved = new Label
        {
            Location = new Point(16, 30),
            Size = new Size(420, 20),
            ForeColor = Color.DarkGreen,
            AutoEllipsis = true,
        };
        _bottomBar.Controls.Add(_lblSaved);

        _btnDefaults = new Button { Text = "Bawaan", Size = new Size(90, 36) };
        _btnDefaults.Click += (_, _) => LoadDefaults();
        _bottomBar.Controls.Add(_btnDefaults);

        _btnReload = new Button { Text = "Muat ulang", Size = new Size(100, 36) };
        _btnReload.Click += (_, _) =>
        {
            _settings = AppSettings.Load();
            LoadValues();
            SetSaved("Pengaturan dimuat ulang dari berkas.");
        };
        _bottomBar.Controls.Add(_btnReload);

        _btnCancel = new Button { Text = "Tutup", Size = new Size(90, 36) };
        _btnCancel.Click += (_, _) => Close();
        _bottomBar.Controls.Add(_btnCancel);

        _btnSave = new Button
        {
            Text = "Simpan",
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10F),
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;
        _bottomBar.Controls.Add(_btnSave);

        CancelButton = _btnCancel;
        AcceptButton = _btnSave;
    }

    private void LayoutBottomBar()
    {
        if (_bottomBar == null || _btnSave == null) return;

        var y = Math.Max(0, (_bottomBar.Height - _btnSave.Height) / 2);
        _btnCancel.Location = new Point(_bottomBar.Width - 14 - _btnCancel.Width, y);
        _btnSave.Location = new Point(_btnCancel.Left - 10 - _btnSave.Width, y);
        _btnReload.Location = new Point(_btnSave.Left - 10 - _btnReload.Width, y);
        _btnDefaults.Location = new Point(_btnReload.Left - 10 - _btnDefaults.Width, y);

        _lblStatus.Width = Math.Max(180, _btnDefaults.Left - 26);
        _lblSaved.Width = _lblStatus.Width;
    }

    // ===================================================================== values

    private void LoadValues()
    {
        _txtStartUrl.Text = _settings.StartUrl;
        _txtBaseUrl.Text = _settings.BaseUrl;
        _txtKioskUrl.Text = _settings.KioskUrl;
        _txtFallbackUrl.Text = _settings.FallbackUrl;
        _numTimeout.Value = Math.Clamp(_settings.KioskTimeoutMs, 500, 60000);
        _numAttempts.Value = Math.Clamp(_settings.KioskAttempts, 1, 20);
        _numInterval.Value = Math.Clamp(_settings.KioskAttemptIntervalMs, 0, 30000);

        _txtOfflinePwd.Text = _txtOfflinePwd2.Text = "";
        _txtSettingsPwd.Text = _txtSettingsPwd2.Text = "";
        UpdatePasswordStates();

        _chkBlockNav.Checked = _settings.BlockNavigationKeys;
        _chkAllowZoom.Checked = _settings.AllowZoom;
        _chkAllowBack.Checked = _settings.AllowBackNavigation;
        _chkClearSession.Checked = _settings.ClearSessionOnQuit;
        _chkDefaultEmergency.Checked = _settings.AllowDefaultEmergencyPassword;

        _txtKioskExe.Text = AutoStart.TargetExePath ?? Program.ResolveKioskExePath();

        _chkAutoStart.Checked = AutoStart.IsEnabled || _settings.AutoStart;
        _chkAutoStartAllUsers.Checked = AutoStart.IsEnabledForAllUsers || _settings.AutoStartAllUsers;
        _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked;
        _cboStartupMethod.SelectedIndex =
            string.Equals(_settings.StartupMethod, AutoStart.MethodTask, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        UpdateAutoStartState();

        _lblTest.Text = "";
        UpdateStatus();
    }

    private void LoadDefaults()
    {
        var answer = MessageBox.Show(this,
            "Isi semua kolom dengan nilai bawaan pabrik? Perubahan baru tersimpan setelah klik Simpan.",
            "Pengaturan bawaan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        // Keep the passwords that are already configured - defaults have none.
        _settings = new AppSettings
        {
            OfflineFallbackPasswordHash = _settings.OfflineFallbackPasswordHash,
            SettingsPasswordHash = _settings.SettingsPasswordHash,
            SettingsPath = _original.SettingsPath,
        };
        AutoStart.TargetExePath = _txtKioskExe.Text.Trim();
        LoadValues();
        SetSaved("Nilai bawaan dimuat. Klik Simpan untuk menerapkannya.");
    }

    private void UpdateStatus()
    {
        _lblStatus.Text =
            $"Administrator: {(Program.IsAdministrator ? "YA" : "TIDAK")}   |   " +
            $"Berkas: {_settings.SettingsPath}";
    }

    private void SetSaved(string message)
    {
        _lblSaved.Text = message;
        _lblSaved.ForeColor = Color.DarkGreen;
    }

    private void UpdatePasswordStates()
    {
        if (_settings.HasOfflineFallback)
        {
            _lblOfflineState.Text = "Status: SUDAH diatur (password sendiri). Biarkan kosong untuk mempertahankannya.";
            _lblOfflineState.ForeColor = Color.DarkGreen;
        }
        else if (_settings.AllowDefaultEmergencyPassword)
        {
            _lblOfflineState.Text =
                $"Status: memakai password BAWAAN \"{AppSettings.DefaultOfflinePassword}\".\n" +
                "Isi kolom di bawah untuk menggantinya dengan password sendiri.";
            _lblOfflineState.ForeColor = Color.FromArgb(180, 95, 6);
        }
        else
        {
            _lblOfflineState.Text = "Status: TIDAK ada password darurat - keluar hanya dengan password online.";
            _lblOfflineState.ForeColor = Color.Firebrick;
        }

        var settingsPwd = !string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash);
        _lblSettingsPwdState.Text = settingsPwd
            ? "Status: SUDAH diatur. Biarkan kosong untuk mempertahankannya."
            : "Status: belum diatur (bebas dibuka).";
        _lblSettingsPwdState.ForeColor = settingsPwd ? Color.DarkGreen : Color.Gray;
    }

    private void UpdateAutoStartState()
    {
        _lblAutoStartState.Text = "Status sekarang: " + AutoStart.Describe();
        _lblAutoStartState.ForeColor = AutoStart.IsEnabled ? Color.DarkGreen : Color.Gray;
    }

    // ===================================================================== actions

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

        try
        {
            var ok = new KioskClient(probe).TestConnection(probe.KioskUrl, out var message);
            _lblTest.Text = message;
            _lblTest.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
        }
        catch (Exception ex)
        {
            _lblTest.Text = "GAGAL - " + ex.Message;
            _lblTest.ForeColor = Color.Firebrick;
        }
    }

    private void BtnClearOffline_Click(object sender, EventArgs e)
    {
        if (!_settings.HasOfflineFallback &&
            string.IsNullOrEmpty(_txtOfflinePwd.Text) && string.IsNullOrEmpty(_txtOfflinePwd2.Text) &&
            !_settings.AllowDefaultEmergencyPassword)
        {
            MessageBox.Show(this, "Belum ada password darurat yang diatur.",
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settings.OfflineFallbackPasswordHash = "";
        _txtOfflinePwd.Text = _txtOfflinePwd2.Text = "";
        UpdatePasswordStates();
        SetSaved("Password darurat offline akan dihapus setelah klik Simpan.");
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
        UpdatePasswordStates();
        SetSaved("Password pengaturan akan dihapus setelah klik Simpan.");
    }

    private void BtnBrowseKiosk_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Pilih berkas CbtKiosk.exe",
            Filter = "Aplikasi CBT Kiosk (CbtKiosk.exe)|CbtKiosk.exe|Aplikasi Windows (*.exe)|*.exe",
            CheckFileExists = true,
            FileName = _txtKioskExe.Text.Trim(),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _txtKioskExe.Text = dialog.FileName;
    }

    private void BtnRunKiosk_Click(object sender, EventArgs e)
    {
        var path = _txtKioskExe.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this,
                "Berkas CbtKiosk.exe tidak ditemukan di:\n\n" + path +
                "\n\nKlik \"Pilih\u2026\" untuk menunjuk lokasi yang benar.",
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = true,
            });
            Logger.Info("Launched the kiosk from the settings app: " + path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Tidak dapat menjalankan CBT Kiosk.\n\n" + ex.Message,
                "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnVerifyStartup_Click(object sender, EventArgs e)
    {
        AutoStart.TargetExePath = _txtKioskExe.Text.Trim();
        var report = AutoStart.Verify();
        Logger.Info("Startup check:\n" + report);
        MessageBox.Show(this, report, "Periksa Startup", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSave_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtStartUrl.Text) || string.IsNullOrWhiteSpace(_txtBaseUrl.Text))
        {
            Warn("URL ujian dan Base URL tidak boleh kosong.");
            _tabs.SelectedIndex = 0;
            return;
        }

        // ---- Passwords: an empty pair keeps whatever is already stored.
        if (!ApplyPassword(_txtOfflinePwd, _txtOfflinePwd2, "Password darurat offline",
                hash => _settings.OfflineFallbackPasswordHash = hash))
            return;

        if (!ApplyPassword(_txtSettingsPwd, _txtSettingsPwd2, "Password pengaturan",
                hash => _settings.SettingsPasswordHash = hash))
            return;

        // ---- Plain fields
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
        _settings.AllowDefaultEmergencyPassword = _chkDefaultEmergency.Checked;
        _settings.AutoStart = _chkAutoStart.Checked;
        _settings.AutoStartAllUsers = _chkAutoStartAllUsers.Checked;
        _settings.StartupMethod = _cboStartupMethod.SelectedIndex == 1 ? AutoStart.MethodTask : AutoStart.MethodRegistry;

        // ---- Save the JSON (shared file first, then per-user).
        string path;
        try
        {
            path = _settings.Save();
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
            return;
        }

        // ---- Auto-start points at the kiosk executable it manages.
        var kioskPath = _txtKioskExe.Text.Trim();
        if (!string.IsNullOrWhiteSpace(kioskPath))
        {
            AutoStart.TargetExePath = kioskPath;
            Program.SaveKioskExePreference(kioskPath);
        }

        var autoStartOk = AutoStart.Apply(_chkAutoStart.Checked, _chkAutoStartAllUsers.Checked,
            _settings.StartupMethod, out var autoStartError);

        _txtOfflinePwd.Text = _txtOfflinePwd2.Text = "";
        _txtSettingsPwd.Text = _txtSettingsPwd2.Text = "";
        UpdatePasswordStates();
        UpdateAutoStartState();
        UpdateStatus();
        SetSaved($"\u2714 Pengaturan tersimpan: {path}");

        var message = "Pengaturan TERSIMPAN.\n\nBerkas:\n" + path;
        if (_settings.AutoStart)
            message += "\n\nAuto-start: " + (autoStartOk ? "aktif" : "GAGAL diterapkan");
        if (!autoStartOk)
            message += "\n\nPERINGATAN: pengaturan auto-start gagal diterapkan.\n" + autoStartError +
                (Program.IsAdministrator
                    ? ""
                    : "\n\nJalankan aplikasi ini sebagai Administrator, atau pilih metode Scheduled Task.");

        MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK,
            autoStartOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private bool ApplyPassword(TextBox first, TextBox second, string label, Action<string> assign)
    {
        var a = first.Text;
        var b = second.Text;

        // Both empty -> keep what is already stored.
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;

        if (a != b)
        {
            Warn($"{label} tidak sama.");
            return false;
        }

        assign(Hash.Sha256Hex(a));
        return true;
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void ShowSaveError(Exception ex) =>
        MessageBox.Show(this,
            "Gagal menyimpan pengaturan.\n\n" + ex.Message +
            "\n\n" +
            (Program.IsAdministrator
                ? "Periksa izin tulis pada folder pengaturan."
                : "Jalankan aplikasi ini sebagai Administrator agar pengaturan bersama semua " +
                  "pengguna (%ProgramData%\\CbtKiosk) dapat ditulis."),
            "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Error);

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
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Folder tidak ditemukan:\n\n" + path);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tidak dapat membuka folder: " + ex.Message);
        }
    }

    // ===================================================================== helpers

    private TabPage NewPage(string title)
    {
        var page = new TabPage(title)
        {
            BackColor = Color.White,
            AutoScroll = true,
        };
        _tabs.TabPages.Add(page);
        return page;
    }

    /// <summary>Tiny top-down layout helper so the tab pages stay readable in code.</summary>
    private sealed class FormLayout
    {
        private readonly TabPage _page;
        private int _y = 16;

        public FormLayout(TabPage page) { _page = page; }

        private const int CaptionX = 16;
        private const int FieldX = 210;
        private const int FieldWidth = 610;

        public int Row() => _y;

        public void Advance(int dy) => _y += dy;

        public void Header(string text)
        {
            _page.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 10.5F),
                ForeColor = Color.FromArgb(0, 90, 158),
                Location = new Point(CaptionX, _y),
                AutoSize = true,
            });
            _y += 30;
        }

        public void Hint(string text)
        {
            _page.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(CaptionX, _y),
                Size = new Size(FieldX + FieldWidth - CaptionX, 36),
                ForeColor = Color.Gray,
            });
            _y += 40;
        }

        public TextBox Text(string caption, string value)
        {
            _page.Controls.Add(new Label
            {
                Text = caption,
                Location = new Point(CaptionX, _y + 4),
                Size = new Size(FieldX - CaptionX - 8, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            });

            var box = new TextBox
            {
                Text = value ?? "",
                Location = new Point(FieldX, _y),
                Width = FieldWidth,
            };
            _page.Controls.Add(box);
            _y += 32;
            return box;
        }

        public TextBox Password(string caption)
        {
            var box = Text(caption, "");
            box.UseSystemPasswordChar = true;
            return box;
        }

        public CheckBox Check(string caption, int dy, int indent = 0)
        {
            var box = new CheckBox
            {
                Text = caption,
                Location = new Point(CaptionX + indent, _y),
                AutoSize = true,
            };
            _page.Controls.Add(box);
            _y += dy;
            return box;
        }
    }
}
