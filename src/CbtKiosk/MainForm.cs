using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CbtKiosk;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly KioskClient _client;
    private WebView2 _web;
    private NotifyIcon _tray;
    private bool _quitting;

    // Low-level keyboard hook.
    private Native.LowLevelKeyboardProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;

    // Hotkey state tracked by the hook.
    private bool _altDown;
    private bool _winDown;
    private bool _ctrlDown;
    private bool _shiftDown;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        _client = new KioskClient(settings);

        Text = "CBT Kiosk - Mode Ujian";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.Black;
        KeyPreview = true;

        BuildUi();

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        KeyDown += MainForm_KeyDown;
        Deactivate += (s, e) => { if (!_quitting) BeginInvoke(new Action(RegainFocus)); };
    }

    private void BuildUi()
    {
        _web = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.White,
        };
        Controls.Add(_web);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "CBT Kiosk - Mode Ujian",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _tray.DoubleClick += (s, e) => BringToFront();
        _tray.ContextMenuStrip.Items.Add("Keluar dari ujian\u2026", null, (s, e) => RequestQuit());
        _tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _tray.ContextMenuStrip.Items.Add("Buka Pengaturan (perlu password)\u2026", null, (s, e) => OpenSettingsFromKiosk());
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        Native.ShowWindow(Handle, 3 /* SW_MAXIMIZE */);
        InstallKeyboardHook();
        BlockShellKeys();

        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 init failed: " + ex);
            MessageBox.Show(
                "Tidak dapat memulai komponen browser (WebView2).\n\n" +
                "Pastikan 'Microsoft Edge WebView2 Runtime' sudah terpasang di komputer ini.\n\n" +
                "Detail: " + ex.Message,
                "CBT Kiosk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DoQuit();
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CbtKiosk", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _web.EnsureCoreWebView2Async(env);

        var core = _web.CoreWebView2;
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.AreDevToolsEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = _settings.AllowZoom;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false; // let us handle Ctrl+P/W/N/T/etc.
        s.IsSwipeNavigationEnabled = false;
        s.IsBuiltInErrorPageEnabled = true;

        core.NewWindowRequested += OnNewWindowRequested;
        core.WindowCloseRequested += OnWindowCloseRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.PermissionRequested += OnPermissionRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.ProcessFailed += OnProcessFailed;
        core.Settings.AreHostObjectsAllowed = false;

        var start = string.IsNullOrWhiteSpace(_settings.StartUrl) ? _settings.BaseUrl : _settings.StartUrl;
        Logger.Info("Navigating to " + start);
        core.Navigate(start);
    }

    // ---------------------------------------------------------------- navigation

    private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsAllowedUri(e.Uri))
        {
            Logger.Warn("Blocked navigation to " + e.Uri);
            e.Cancel = true;
        }
    }

    private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            Logger.Warn($"Navigation completed with error {e.WebErrorStatus} for {_web.Source}");
        }
    }

    private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (IsAllowedUri(e.Uri))
        {
            Logger.Info("New window request -> same window: " + e.Uri);
            _web.CoreWebView2.Navigate(e.Uri);
        }
        else
        {
            Logger.Warn("Blocked popup to " + e.Uri);
        }
    }

    private void OnWindowCloseRequested(object sender, object e)
    {
        Logger.Warn("Page requested window close - ignored (use the quit password).");
    }

    private void OnDownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
    {
        Logger.Warn("Download blocked: " + e.DownloadOperation.Uri);
        e.Cancel = true;
    }

    private void OnPermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        Logger.Warn($"Permission '{e.PermissionKind}' requested by {e.Uri} - denied.");
        e.State = CoreWebView2PermissionState.Deny;
        e.Handled = true;
    }

    private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Logger.Error("WebView2 process failed: " + e.ProcessFailedKind + " - reloading.");
        try { _web.CoreWebView2.Reload(); } catch { /* ignore */ }
    }

    /// <summary>Navigation is restricted to the CBT host (and any explicit fallback host).</summary>
    private bool IsAllowedUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return false;
        if (uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return true;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var target)) return false;
        if (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp) return false;

        foreach (var allowed in AllowedHosts())
        {
            if (string.Equals(target.Host, allowed, StringComparison.OrdinalIgnoreCase)) return true;
            if (target.Host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private IEnumerable<string> AllowedHosts()
    {
        foreach (var url in new[] { _settings.BaseUrl, _settings.StartUrl, _settings.KioskUrl, _settings.FallbackUrl })
        {
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var u) && !string.IsNullOrWhiteSpace(u.Host))
                yield return u.Host;
        }
    }

    // ---------------------------------------------------------------- quit / unlock

    private void RequestQuit()
    {
        using var prompt = new PasswordPrompt("Keluar dari Ujian",
            "Masukkan password untuk keluar dari mode ujian.\nPassword dikelola oleh pengawas melalui panel CBT.");
        if (prompt.ShowDialog(this) != DialogResult.OK) return;

        var entered = prompt.EnteredPassword;
        var result = Task.Run(() => _client.Validate(entered)).GetAwaiter().GetResult();
        Logger.Info("Quit attempt result: " + result);

        switch (result)
        {
            case QuitPasswordResult.Correct:
                DoQuit();
                break;
            case QuitPasswordResult.Wrong:
                ShowError("Password salah.");
                break;
            case QuitPasswordResult.Expired:
                ShowError("Password sudah kedaluwarsa. Minta pengawas memperbarui password di panel CBT.");
                break;
            case QuitPasswordResult.Unavailable:
                ShowError("Server CBT menjawab tetapi password tidak tersedia saat ini.");
                break;
            case QuitPasswordResult.Unreachable:
                ShowError("Tidak dapat menghubungi server CBT dan tidak ada password cadangan offline yang dikonfigurasi.\n\n" +
                          "Hubungi pengawas.");
                break;
        }
    }

    private void OpenSettingsFromKiosk()
    {
        if (!string.IsNullOrWhiteSpace(_settings.SettingsPasswordHash))
        {
            using var prompt = new PasswordPrompt("Buka Pengaturan", "Masukkan password pengaturan.");
            if (prompt.ShowDialog(this) != DialogResult.OK) return;
            if (!Hash.FixedTimeEquals(Hash.Sha256Hex(prompt.EnteredPassword),
                    _settings.SettingsPasswordHash.ToLowerInvariant()))
            {
                ShowError("Password pengaturan salah.");
                return;
            }
        }

        using var form = new SettingsForm(_settings, standalone: false);
        form.ShowDialog(this);
        // Apply the (possibly) changed navigation-related settings.
        if (_web?.CoreWebView2 != null)
        {
            _web.CoreWebView2.Settings.IsZoomControlEnabled = _settings.AllowZoom;
        }
    }

    private void ShowError(string message)
    {
        var previous = TopMost;
        TopMost = true;
        MessageBox.Show(this, message, "CBT Kiosk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        TopMost = previous;
    }

    /// <summary>Pulls the window back to the front (used when the user tries to switch away).</summary>
    private void RegainFocus()
    {
        try
        {
            Native.ShowWindow(Handle, 3 /* SW_MAXIMIZE */);
            Native.SetForegroundWindow(Handle);
            BringToFront();
            Activate();
        }
        catch { /* ignore */ }
    }

    private void DoQuit()
    {
        _quitting = true;
        Logger.Info("Quitting the kiosk.");
        RemoveKeyboardHook();
        UnblockShellKeys();
        _tray.Visible = false;
        _tray.Dispose();
        Close();
    }

    // ---------------------------------------------------------------- shell key suppression

    private void InstallKeyboardHook()
    {
        _hookProc = HookCallback;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookHandle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc,
            Native.GetModuleHandle(module.ModuleName), 0);
        if (_hookHandle == IntPtr.Zero)
            Logger.Warn("Failed to install the low-level keyboard hook (Win32 error " + Marshal.GetLastWin32Error() + ").");
        else
            Logger.Info("Keyboard hook installed.");
    }

    private void RemoveKeyboardHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            var vk = (Keys)data.vkCode;
            var isDown = wParam == (IntPtr)Native.WM_KEYDOWN || wParam == (IntPtr)Native.WM_SYSKEYDOWN;
            var isUp = wParam == (IntPtr)Native.WM_KEYUP || wParam == (IntPtr)Native.WM_SYSKEYUP;

            if (vk == Keys.LWin || vk == Keys.RWin) { _winDown = isDown; }
            if (vk == Keys.LMenu || vk == Keys.RMenu) { _altDown = isDown; }
            if (vk == Keys.LControlKey || vk == Keys.RControlKey) { _ctrlDown = isDown; }
            if (vk == Keys.LShiftKey || vk == Keys.RShiftKey) { _shiftDown = isDown; }

            if (_settings.BlockNavigationKeys)
            {
                if (isDown && ShouldBlock(vk)) return (IntPtr)1; // swallow the key

                // Also swallow the key-up of the Windows key: the Start menu is opened on key-up,
                // so blocking only the key-down would still show it.
                if (isUp && (vk == Keys.LWin || vk == Keys.RWin)) return (IntPtr)1;
            }
        }

        return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool ShouldBlock(Keys vk)
    {
        // Windows key (alone or combined) - blocks Start menu and Win+* shortcuts.
        if (_winDown || vk == Keys.LWin || vk == Keys.RWin) return true;

        // Alt+Tab / Alt+Esc / Alt+F4 / Alt+Space.
        if (_altDown && (vk == Keys.Tab || vk == Keys.Escape || vk == Keys.F4 || vk == Keys.Space)) return true;

        // Ctrl+Esc (Start menu) and Ctrl+Shift+Esc (Task Manager).
        if (_ctrlDown && vk == Keys.Escape) return true;
        if (_ctrlDown && _shiftDown && vk == Keys.Escape) return true;

        // Ctrl+Alt+Del cannot be blocked by a hook (Secure Attention Sequence); documented limitation.

        // Developer / print / tab management accelerators.
        if (_ctrlDown)
        {
            switch (vk)
            {
                case Keys.P:            // print
                case Keys.S:            // save
                case Keys.O:            // open
                case Keys.N:            // new window
                case Keys.T:            // new tab
                case Keys.W:            // close tab
                case Keys.U:            // view source
                case Keys.J:            // downloads
                case Keys.H:            // history
                    return true;
                case Keys.R:            // reload
                    return !_settings.AllowReload;
                case Keys.Add:
                case Keys.Subtract:
                case Keys.Oemplus:
                case Keys.OemMinus:
                case Keys.D0:
                    return !_settings.AllowZoom;
            }
        }

        if (vk == Keys.F12) return true;
        if (vk == Keys.F5) return !_settings.AllowReload;
        if (vk == Keys.BrowserBack) return !_settings.AllowBackNavigation;
        if (vk == Keys.Apps) return true; // context-menu key

        return false;
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _settings.BlockNavigationKeys)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void BlockShellKeys()
    {
        // Best-effort: try to disable Task Manager / hotkeys for the current session.
        try
        {
            Microsoft.Win32.Registry.SetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\System",
                "DisableTaskMgr", 1, Microsoft.Win32.RegistryValueKind.DWord);
            Logger.Info("Task Manager disabled for the current user (restored on quit).");
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not disable Task Manager: " + ex.Message);
        }
    }

    private void UnblockShellKeys()
    {
        try
        {
            Microsoft.Win32.Registry.SetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\System",
                "DisableTaskMgr", 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { /* ignore */ }
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (!_quitting)
        {
            // Let the OS through on shutdown / logoff / application exit so we never wedge the machine.
            var osClosing = e.CloseReason == CloseReason.WindowsShutDown
                            || e.CloseReason == CloseReason.ApplicationExitCall
                            || e.CloseReason == CloseReason.TaskManagerClosing;

            if (!osClosing)
            {
                // Block Alt+F4 and any other close request that bypasses the password.
                Logger.Warn($"Close attempt blocked (reason: {e.CloseReason}).");
                e.Cancel = true;
                return;
            }

            _quitting = true;
            Logger.Info($"Closing due to {e.CloseReason}.");
        }

        RemoveKeyboardHook();
        UnblockShellKeys();
    }
}
