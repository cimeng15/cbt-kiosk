using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CbtKioskTool
{
    /// <summary>
    /// The whole user interface of CbtKioskTool.exe: one compact window, no tabs, no console.
    /// It writes the very same settings.json the CBT Kiosk reads, so the kiosk itself stays
    /// untouched and the tool can be a few hundred kilobytes instead of tens of megabytes.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const int BottomBarHeight = 62;

        private AppSettings _settings;

        private Panel _content;
        private Panel _bottomBar;

        private TextBox _txtStartUrl, _txtBaseUrl, _txtKioskUrl, _txtFallbackUrl;
        private NumericUpDown _numTimeout, _numAttempts, _numInterval;
        private Label _lblTest;

        private TextBox _txtOfflinePwd, _txtOfflinePwd2;
        private Label _lblOfflineState;
        private CheckBox _chkDefaultEmergency;

        private TextBox _txtSettingsPwd, _txtSettingsPwd2;
        private Label _lblSettingsPwdState;

        private CheckBox _chkShowPasswords;
        private CheckBox _chkBlockNav, _chkAllowZoom, _chkAllowBack, _chkClearSession;

        private TextBox _txtKioskExe;
        private CheckBox _chkAutoStart, _chkAutoStartAllUsers;
        private ComboBox _cboStartupMethod;
        private Label _lblAutoStartState;

        private Label _lblStatus;

        public MainForm()
        {
            Text = Kiosk.Product + " " + Kiosk.Version + " - Pengaturan CBT Kiosk";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(740, 660);
            MinimumSize = new Size(680, 480);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            Icon = SystemIcons.Shield;
            MaximizeBox = true;
            MinimizeBox = true;

            BuildUi();
            Reload();
        }

        // ===================================================================== UI

        private void BuildUi()
        {
            _content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
            };
            Controls.Add(_content);

            _bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = BottomBarHeight,
                BackColor = Color.FromArgb(244, 246, 249),
            };
            Controls.Add(_bottomBar);

            BuildBottomBar();
            BuildContent();
        }

        private void BuildContent()
        {
            var y = 12;

            Label Header(string text)
            {
                var label = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI Semibold", 10F),
                    ForeColor = Color.FromArgb(0, 90, 158),
                    Location = new Point(14, y),
                    AutoSize = true,
                };
                _content.Controls.Add(label);
                y += 26;
                return label;
            }

            void Hint(string text, int height = 18)
            {
                _content.Controls.Add(new Label
                {
                    Text = text,
                    Location = new Point(14, y),
                    Size = new Size(690, height),
                    ForeColor = Color.Gray,
                });
                y += height + 4;
            }

            Label Caption(string text, int width = 160)
            {
                var label = new Label
                {
                    Text = text,
                    Location = new Point(14, y + 4),
                    Size = new Size(width, 20),
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                _content.Controls.Add(label);
                return label;
            }

            TextBox Field(string caption, int width)
            {
                Caption(caption);
                var box = new TextBox { Location = new Point(180, y), Width = width };
                _content.Controls.Add(box);
                y += 28;
                return box;
            }

            TextBox PasswordField(string caption)
            {
                Caption(caption);
                var box = new TextBox
                {
                    Location = new Point(180, y),
                    Width = 380,
                    UseSystemPasswordChar = true,
                };
                _content.Controls.Add(box);
                y += 28;
                return box;
            }

            CheckBox Check(string caption, int indent = 14)
            {
                var box = new CheckBox
                {
                    Text = caption,
                    Location = new Point(indent, y),
                    AutoSize = true,
                };
                _content.Controls.Add(box);
                y += 24;
                return box;
            }

            // ------------------------------------------------- connections
            Header("Alamat ujian dan password online");

            _txtStartUrl = Field("URL ujian (dibuka):", 480);
            _txtBaseUrl = Field("Base URL (domain):", 480);
            _txtKioskUrl = Field("Endpoint password:", 380);

            var btnTest = new Button { Text = "Tes koneksi", Location = new Point(568, y - 29), Size = new Size(120, 25) };
            btnTest.Click += BtnTest_Click;
            _content.Controls.Add(btnTest);

            _txtFallbackUrl = Field("Fallback online:", 480);
            Hint("Opsional. Dipakai bila endpoint utama tidak terjangkau.");

            _lblTest = new Label { Location = new Point(180, y), Size = new Size(510, 34), ForeColor = Color.DimGray };
            _content.Controls.Add(_lblTest);
            y += 42;

            Header("Permintaan password ke server");
            Caption("Timeout (ms):");
            _numTimeout = new NumericUpDown
            {
                Location = new Point(180, y), Width = 80, Minimum = 500, Maximum = 60000, Increment = 500,
            };
            _content.Controls.Add(_numTimeout);

            _content.Controls.Add(new Label { Text = "Percobaan:", Location = new Point(280, y + 4), AutoSize = true });
            _numAttempts = new NumericUpDown { Location = new Point(350, y), Width = 55, Minimum = 1, Maximum = 20 };
            _content.Controls.Add(_numAttempts);

            _content.Controls.Add(new Label { Text = "Jeda (ms):", Location = new Point(420, y + 4), AutoSize = true });
            _numInterval = new NumericUpDown { Location = new Point(490, y), Width = 80, Minimum = 0, Maximum = 30000, Increment = 100 };
            _content.Controls.Add(_numInterval);
            y += 34;

            // ------------------------------------------------- passwords
            Header("Password darurat (offline)");
            Hint("SELALU diterima untuk keluar - terutama bila internet/server mati ATAU password online\n" +
                 "sudah kedaluwarsa. Jaring pengaman agar pengawas tidak pernah terkunci di luar.", 32);

            _lblOfflineState = new Label { Location = new Point(180, y), Size = new Size(380, 34), ForeColor = Color.DimGray };
            _content.Controls.Add(_lblOfflineState);

            var btnClearOffline = new Button { Text = "Kosongkan", Location = new Point(568, y + 2), Size = new Size(90, 25) };
            btnClearOffline.Click += BtnClearOffline_Click;
            _content.Controls.Add(btnClearOffline);
            y += 40;

            _txtOfflinePwd = PasswordField("Password darurat baru:");
            _txtOfflinePwd2 = PasswordField("Ulangi password darurat:");

            _chkDefaultEmergency = Check("Pakai password darurat BAWAAN \"" + Kiosk.DefaultOfflinePassword + "\" bila kolom di atas kosong");
            Hint("Hilangkan centang ini bila kiosk hanya boleh dibuka dengan password dari panel CBT.");
            y += 6;

            Header("Password pengaturan (kiosk)");
            Hint("Melindungi menu Pengaturan di dalam mode ujian agar tidak dibuka siswa.");

            _lblSettingsPwdState = new Label { Location = new Point(180, y), Size = new Size(380, 20), ForeColor = Color.DimGray };
            _content.Controls.Add(_lblSettingsPwdState);

            var btnClearSettingsPwd = new Button { Text = "Kosongkan", Location = new Point(568, y - 2), Size = new Size(90, 25) };
            btnClearSettingsPwd.Click += BtnClearSettingsPwd_Click;
            _content.Controls.Add(btnClearSettingsPwd);
            y += 30;

            _txtSettingsPwd = PasswordField("Password pengaturan baru:");
            _txtSettingsPwd2 = PasswordField("Ulangi password pengaturan:");

            _chkShowPasswords = Check("Tampilkan password yang sedang diketik");
            _chkShowPasswords.CheckedChanged += (s, e) =>
            {
                var reveal = _chkShowPasswords.Checked;
                foreach (var box in new[] { _txtOfflinePwd, _txtOfflinePwd2, _txtSettingsPwd, _txtSettingsPwd2 })
                    box.UseSystemPasswordChar = !reveal;
            };
            y += 6;

            // ------------------------------------------------- lockdown
            Header("Penguncian saat ujian");
            Hint("Pintasan yang selalu aktif di kiosk: F5 = muat ulang, Ctrl+Alt+Q = keluar (minta password).");

            _chkBlockNav = Check("Blokir tombol Windows / Alt+Tab / Alt+F4 / Esc");
            _chkAllowZoom = Check("Izinkan zoom (Ctrl + / Ctrl -)");
            _chkAllowBack = Check("Izinkan tombol kembali (Backspace)");
            _chkClearSession = Check("Hapus sesi/cookies saat keluar (siswa harus login ulang)");
            y += 6;

            // ------------------------------------------------- startup
            Header("Startup Windows dan aplikasi kiosk");
            Hint("Kiosk terbuka sendiri saat Windows login, sehingga siswa tidak perlu membukanya.");

            Caption("CbtKiosk.exe:");
            _txtKioskExe = new TextBox { Location = new Point(180, y), Width = 400 };
            _content.Controls.Add(_txtKioskExe);

            var btnBrowse = new Button { Text = "Pilih...", Location = new Point(588, y - 1), Size = new Size(70, 25) };
            btnBrowse.Click += BtnBrowseKiosk_Click;
            _content.Controls.Add(btnBrowse);
            y += 30;

            _chkAutoStart = Check("Jalankan CBT Kiosk otomatis saat Windows menyala");
            _chkAutoStart.CheckedChanged += (s, e) => { _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked; };

            _chkAutoStartAllUsers = Check("Untuk SEMUA pengguna Windows (perlu Administrator)", 34);

            _content.Controls.Add(new Label { Text = "Metode:", Location = new Point(34, y + 4), AutoSize = true });
            _cboStartupMethod = new ComboBox
            {
                Location = new Point(100, y), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cboStartupMethod.Items.Add("Registry Run key (bawaan)");
            _cboStartupMethod.Items.Add("Scheduled Task (lebih andal)");
            _content.Controls.Add(_cboStartupMethod);
            y += 30;

            _lblAutoStartState = new Label { Location = new Point(34, y), Size = new Size(500, 20), ForeColor = Color.DimGray };
            _content.Controls.Add(_lblAutoStartState);

            var btnVerify = new Button { Text = "Periksa startup", Location = new Point(548, y - 3), Size = new Size(110, 26) };
            btnVerify.Click += BtnVerifyStartup_Click;
            _content.Controls.Add(btnVerify);
            y += 34;

            var btnRunKiosk = new Button { Text = "Jalankan CBT Kiosk", Location = new Point(14, y), Size = new Size(170, 32) };
            btnRunKiosk.Click += BtnRunKiosk_Click;
            _content.Controls.Add(btnRunKiosk);

            var btnOpenSettings = new Button { Text = "Buka folder pengaturan", Location = new Point(194, y), Size = new Size(180, 32) };
            btnOpenSettings.Click += (s, e) => OpenFolder(SettingsStore.LoadedDirectoryHint());
            _content.Controls.Add(btnOpenSettings);

            var btnOpenExeFolder = new Button { Text = "Buka folder kiosk", Location = new Point(384, y), Size = new Size(150, 32) };
            btnOpenExeFolder.Click += (s, e) => OpenFolder(Path.GetDirectoryName(AutoStart.EffectiveExePath));
            _content.Controls.Add(btnOpenExeFolder);
            y += 42;
        }

        private void BuildBottomBar()
        {
            _lblStatus = new Label
            {
                Location = new Point(14, 10),
                Size = new Size(380, 18),
                ForeColor = Color.DimGray,
                AutoEllipsis = true,
            };
            _bottomBar.Controls.Add(_lblStatus);

            var font = new Font("Segoe UI Semibold", 9F);

            var btnSave = new Button
            {
                Text = "Simpan",
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = font,
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            _bottomBar.Controls.Add(btnSave);

            var btnClose = new Button { Text = "Tutup", Size = new Size(90, 34) };
            btnClose.Click += (s, e) => Close();
            _bottomBar.Controls.Add(btnClose);

            var btnReload = new Button { Text = "Muat ulang", Size = new Size(100, 34) };
            btnReload.Click += (s, e) => Reload();
            _bottomBar.Controls.Add(btnReload);

            var btnDefaults = new Button { Text = "Bawaan", Size = new Size(90, 34) };
            btnDefaults.Click += BtnDefaults_Click;
            _bottomBar.Controls.Add(btnDefaults);

            void Layout()
            {
                var top = Math.Max(0, (_bottomBar.Height - btnSave.Height) / 2 - 6);
                btnClose.Location = new Point(_bottomBar.Width - 14 - btnClose.Width, top);
                btnSave.Location = new Point(btnClose.Left - 10 - btnSave.Width, top);
                btnReload.Location = new Point(btnSave.Left - 10 - btnReload.Width, top);
                btnDefaults.Location = new Point(btnReload.Left - 10 - btnDefaults.Width, top);
                _lblStatus.Width = Math.Max(150, btnDefaults.Left - 26);
            }

            _bottomBar.Resize += (s, e) => Layout();
            Layout();

            AcceptButton = btnSave;
            CancelButton = btnClose;
        }

        // ================================================================ values

        private void Reload()
        {
            string source;
            string warning;
            _settings = SettingsStore.Load(out source, out warning);
            LoadValues();
            ShowStatus("Dimuat dari: " + source + "   |   " +
                       (IsAdministrator() ? "Administrator: YA" : "Administrator: TIDAK"));
            if (warning != null) Warn(warning);
        }

        private void LoadValues()
        {
            AutoStart.TargetExePath = SettingsStore.LoadKioskExePath();

            _txtStartUrl.Text = _settings.StartUrl;
            _txtBaseUrl.Text = _settings.BaseUrl;
            _txtKioskUrl.Text = _settings.KioskUrl;
            _txtFallbackUrl.Text = _settings.FallbackUrl;
            _numTimeout.Value = Clamp(_settings.KioskTimeoutMs, 500, 60000);
            _numAttempts.Value = Clamp(_settings.KioskAttempts, 1, 20);
            _numInterval.Value = Clamp(_settings.KioskAttemptIntervalMs, 0, 30000);

            _txtOfflinePwd.Text = "";
            _txtOfflinePwd2.Text = "";
            _txtSettingsPwd.Text = "";
            _txtSettingsPwd2.Text = "";
            _chkDefaultEmergency.Checked = _settings.AllowDefaultEmergencyPassword;
            UpdatePasswordStates();

            _chkBlockNav.Checked = _settings.BlockNavigationKeys;
            _chkAllowZoom.Checked = _settings.AllowZoom;
            _chkAllowBack.Checked = _settings.AllowBackNavigation;
            _chkClearSession.Checked = _settings.ClearSessionOnQuit;

            _txtKioskExe.Text = AutoStart.TargetExePath ?? FindKioskExe();
            _chkAutoStart.Checked = AutoStart.IsEnabled || _settings.AutoStart;
            _chkAutoStartAllUsers.Checked = AutoStart.IsEnabledForAllUsers || _settings.AutoStartAllUsers;
            _chkAutoStartAllUsers.Enabled = _chkAutoStart.Checked;
            _cboStartupMethod.SelectedIndex =
                string.Equals(_settings.StartupMethod, Kiosk.MethodTask, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            UpdateAutoStartState();

            _lblTest.Text = "";
        }

        private void Collect()
        {
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
            _settings.StartupMethod = _cboStartupMethod.SelectedIndex == 1 ? Kiosk.MethodTask : Kiosk.MethodRegistry;
        }

        private void UpdatePasswordStates()
        {
            if (_settings.HasOfflineFallback)
            {
                _lblOfflineState.Text = "Status: password darurat SENDIRI sudah diatur.\nBiarkan kolom kosong untuk mempertahankannya.";
                _lblOfflineState.ForeColor = Color.DarkGreen;
            }
            else if (_settings.AllowDefaultEmergencyPassword)
            {
                _lblOfflineState.Text = "Status: memakai password BAWAAN \"" + Kiosk.DefaultOfflinePassword + "\".\nIsi kolom di bawah untuk menggantinya.";
                _lblOfflineState.ForeColor = Color.FromArgb(180, 95, 6);
            }
            else
            {
                _lblOfflineState.Text = "Status: TIDAK ada password darurat - hanya password online yang bisa membuka.";
                _lblOfflineState.ForeColor = Color.Firebrick;
            }

            var settingsPwd = !string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash);
            _lblSettingsPwdState.Text = settingsPwd
                ? "Status: sudah diatur. Biarkan kosong untuk mempertahankannya."
                : "Status: belum diatur (menu Pengaturan di kiosk bebas dibuka).";
            _lblSettingsPwdState.ForeColor = settingsPwd ? Color.DarkGreen : Color.Gray;
        }

        private void UpdateAutoStartState()
        {
            _lblAutoStartState.Text = "Status sekarang: " + AutoStart.Describe();
            _lblAutoStartState.ForeColor = AutoStart.IsEnabled ? Color.DarkGreen : Color.Gray;
        }

        // =============================================================== actions

        private AppSettings Probe()
        {
            return new AppSettings
            {
                KioskUrl = _txtKioskUrl.Text.Trim(),
                FallbackUrl = _txtFallbackUrl.Text.Trim(),
                KioskTimeoutMs = (int)_numTimeout.Value,
                KioskAttempts = Math.Max(1, (int)_numAttempts.Value),
                KioskAttemptIntervalMs = (int)_numInterval.Value,
            };
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            _lblTest.Text = "Menguji...";
            _lblTest.ForeColor = Color.DimGray;
            Application.DoEvents();

            string message;
            var ok = KioskApi.TestConnection(Probe(), out message);
            _lblTest.Text = message;
            _lblTest.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
        }

        private void BtnClearOffline_Click(object sender, EventArgs e)
        {
            if (!_settings.HasOfflineFallback &&
                string.IsNullOrEmpty(_txtOfflinePwd.Text) && string.IsNullOrEmpty(_txtOfflinePwd2.Text) &&
                !_settings.AllowDefaultEmergencyPassword)
            {
                Info("Belum ada password darurat yang diatur.");
                return;
            }

            _settings.OfflineFallbackPasswordHash = "";
            _txtOfflinePwd.Text = "";
            _txtOfflinePwd2.Text = "";
            UpdatePasswordStates();
            ShowStatus("Password darurat sendiri dihapus - kembali ke bawaan (belum tersimpan).");
        }

        private void BtnClearSettingsPwd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash) &&
                string.IsNullOrEmpty(_txtSettingsPwd.Text) && string.IsNullOrEmpty(_txtSettingsPwd2.Text))
            {
                Info("Belum ada password pengaturan yang diatur.");
                return;
            }

            _settings.SettingsPasswordHash = "";
            _txtSettingsPwd.Text = "";
            _txtSettingsPwd2.Text = "";
            UpdatePasswordStates();
            ShowStatus("Password pengaturan akan dihapus setelah klik Simpan.");
        }

        private void BtnBrowseKiosk_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Pilih berkas CbtKiosk.exe",
                Filter = "Aplikasi CBT Kiosk (CbtKiosk.exe)|CbtKiosk.exe|Aplikasi Windows (*.exe)|*.exe",
                CheckFileExists = true,
                FileName = _txtKioskExe.Text.Trim(),
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) _txtKioskExe.Text = dialog.FileName;
            }
        }

        private void BtnVerifyStartup_Click(object sender, EventArgs e)
        {
            AutoStart.TargetExePath = _txtKioskExe.Text.Trim();
            MessageBox.Show(this, AutoStart.Verify(), "Periksa Startup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnRunKiosk_Click(object sender, EventArgs e)
        {
            var path = _txtKioskExe.Text.Trim();
            if (!File.Exists(path))
            {
                Warn("Berkas CbtKiosk.exe tidak ditemukan di:\r\n\r\n" + path +
                     "\r\n\r\nKlik \"Pilih...\" untuk menunjuk lokasi yang benar.");
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
            }
            catch (Exception ex)
            {
                Warn("Tidak dapat menjalankan CBT Kiosk.\r\n\r\n" + ex.Message);
            }
        }

        private void BtnDefaults_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Isi semua kolom dengan nilai bawaan pabrik?\r\n" +
                                      "Perubahan baru tersimpan setelah klik Simpan.",
                    "Pengaturan bawaan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var kioskPath = _txtKioskExe.Text.Trim();
            _settings = new AppSettings();       // defaults, no passwords
            LoadValues();
            _txtKioskExe.Text = kioskPath;
            ShowStatus("Nilai bawaan dimuat. Klik Simpan untuk menerapkannya.");
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtStartUrl.Text) || string.IsNullOrWhiteSpace(_txtBaseUrl.Text))
            {
                Warn("URL ujian dan Base URL tidak boleh kosong.");
                return;
            }

            // Passwords: an empty pair keeps what is already stored.
            if (!ApplyPassword(_txtOfflinePwd, _txtOfflinePwd2, "Password darurat offline",
                    hash => _settings.OfflineFallbackPasswordHash = hash))
                return;

            if (!ApplyPassword(_txtSettingsPwd, _txtSettingsPwd2, "Password pengaturan",
                    hash => _settings.SettingsPasswordHash = hash))
                return;

            Collect();

            string path;
            try
            {
                path = SettingsStore.Save(_settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Gagal menyimpan pengaturan.\r\n\r\n" + ex.Message +
                    "\r\n\r\n" +
                    (IsAdministrator()
                        ? "Periksa izin tulis folder pengaturan."
                        : "Jalankan aplikasi ini sebagai Administrator (klik kanan -> Run as administrator) " +
                          "agar pengaturan bersama semua pengguna (%ProgramData%\\CbtKiosk) dapat ditulis."),
                    "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var kioskPath = _txtKioskExe.Text.Trim();
            if (!string.IsNullOrWhiteSpace(kioskPath))
            {
                AutoStart.TargetExePath = kioskPath;
                SettingsStore.SaveKioskExePath(kioskPath);
            }

            string autoStartError;
            var autoStartOk = AutoStart.Apply(_chkAutoStart.Checked, _chkAutoStartAllUsers.Checked,
                _settings.StartupMethod, out autoStartError);

            _txtOfflinePwd.Text = "";
            _txtOfflinePwd2.Text = "";
            _txtSettingsPwd.Text = "";
            _txtSettingsPwd2.Text = "";
            UpdatePasswordStates();
            UpdateAutoStartState();

            var message = "Pengaturan TERSIMPAN.\r\n\r\nBerkas:\r\n" + path;
            if (_settings.AutoStart)
                message += "\r\n\r\nAuto-start: " + (autoStartOk ? "aktif" : "GAGAL diterapkan");
            if (!autoStartOk)
                message += "\r\n\r\nPERINGATAN: pengaturan auto-start gagal diterapkan.\r\n" + autoStartError +
                           (IsAdministrator() ? "" : "\r\n\r\nJalankan sebagai Administrator untuk opsi \"semua pengguna\".");

            message += "\r\n\r\nJika CbtKiosk.exe sedang berjalan, tutup dan buka ulang agar pengaturan ini dipakai.";

            MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK,
                autoStartOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            ShowStatus("Tersimpan: " + path);
        }

        private bool ApplyPassword(TextBox first, TextBox second, string label, Action<string> assign)
        {
            var a = first.Text;
            var b = second.Text;

            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return true;   // keep the stored one

            if (a != b)
            {
                Warn(label + " tidak sama.");
                return false;
            }

            if (a.Length < 4)
            {
                Warn(label + " terlalu pendek (minimal 4 karakter).");
                return false;
            }

            assign(Hash.Sha256Hex(a));
            return true;
        }

        // =============================================================== helpers

        private static bool IsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static decimal Clamp(int value, int min, int max) =>
            Math.Min(Math.Max(value, min), max);

        private static string FindKioskExe()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "CbtKiosk.exe"),
                Path.Combine(baseDir, "dist", "CbtKiosk.exe"),
                Path.Combine(baseDir, "publish", "win-x64", "CbtKiosk.exe"),
                Path.Combine(baseDir, "..", "CbtKiosk.exe"),
                Path.Combine(SettingsStore.SharedDirectory, "CbtKiosk.exe"),
            };

            foreach (var candidate in candidates)
            {
                try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); }
                catch { /* ignore */ }
            }

            try { return Path.GetFullPath(candidates[0]); } catch { return candidates[0]; }
        }

        private void ShowStatus(string text)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = Color.DimGray;
        }

        private void Warn(string message) =>
            MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private void Info(string message) =>
            MessageBox.Show(this, message, "Pengaturan", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private static void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("Folder tidak ditemukan:\r\n\r\n" + path);
                    return;
                }
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tidak dapat membuka folder: " + ex.Message);
            }
        }
    }
}
