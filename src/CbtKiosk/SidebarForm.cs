using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CbtKiosk;

/// <summary>
/// Floating side tab pinned to the right edge of the kiosk window.
///
/// It is a SEPARATE top-level (owned) window on purpose: WebView2 hosts the page in its own
/// native child window, so a normal WinForms control placed on the form would be hidden behind
/// it. A small always-on-top window owned by the main form stays visible above the browser.
///
/// Collapsed it shows a single handle ("☰"); clicking it slides the menu out to the left with
/// the available actions (reload / quit). Clicking again collapses it.
/// </summary>
public sealed class SidebarForm : Form
{
    private static readonly Color Bg = Color.FromArgb(30, 41, 59);        // slate-800
    private static readonly Color BgHover = Color.FromArgb(51, 65, 85);   // slate-700
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);   // blue-600
    private static readonly Color Danger = Color.FromArgb(220, 38, 38);   // red-600
    private static readonly Color Fg = Color.White;

    private static readonly Size CollapsedSize = new(38, 104);
    private static readonly Size ExpandedSize = new(212, 150);
    private const int Inset = 6;

    private readonly Func<Rectangle> _boundsProvider;
    private readonly Action _onRefresh;
    private readonly Action _onExit;

    private Panel _collapsedPanel;
    private Panel _expandedPanel;
    private bool _expanded;

    public SidebarForm(Func<Rectangle> boundsProvider, Action onRefresh, Action onExit)
    {
        _boundsProvider = boundsProvider;
        _onRefresh = onRefresh;
        _onExit = onExit;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Bg;
        Size = CollapsedSize;
        Font = new Font("Segoe UI", 9.5F);
        DoubleBuffered = true;

        BuildUi();
        ApplyRoundedRegion();
    }

    private void BuildUi()
    {
        // ---- collapsed handle -------------------------------------------------
        var handle = new Button
        {
            Text = "\u2630", // ☰
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Bg,
            ForeColor = Fg,
            Font = new Font("Segoe UI Symbol", 14F),
            Cursor = Cursors.Hand,
        };
        handle.FlatAppearance.BorderSize = 0;
        handle.FlatAppearance.MouseOverBackColor = BgHover;
        handle.Click += (s, e) => SetExpanded(true);

        _collapsedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
        _collapsedPanel.Controls.Add(handle);

        // ---- expanded menu ----------------------------------------------------
        _expandedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Visible = false };

        var title = new Label
        {
            Text = "Menu Ujian",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI Semibold", 9.5F),
            Location = new Point(14, 11),
            AutoSize = true,
        };

        var collapse = new Button
        {
            Text = "\u00BB", // »
            Size = new Size(30, 26),
            Location = new Point(ExpandedSize.Width - 38, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = Bg,
            ForeColor = Fg,
            Font = new Font("Segoe UI", 11F),
            Cursor = Cursors.Hand,
        };
        collapse.FlatAppearance.BorderSize = 0;
        collapse.FlatAppearance.MouseOverBackColor = BgHover;
        collapse.Click += (s, e) => SetExpanded(false);

        var btnRefresh = MakeMenuButton("Muat ulang", Accent, 46);
        btnRefresh.Click += (s, e) => { SetExpanded(false); _onRefresh?.Invoke(); };

        var btnExit = MakeMenuButton("Keluar dari ujian", Danger, 92);
        btnExit.Click += (s, e) => { SetExpanded(false); _onExit?.Invoke(); };

        _expandedPanel.Controls.AddRange(new Control[] { title, collapse, btnRefresh, btnExit });

        Controls.Add(_expandedPanel);
        Controls.Add(_collapsedPanel);
    }

    private Button MakeMenuButton(string text, Color accent, int top)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(12, top),
            Size = new Size(ExpandedSize.Width - 24, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
        return b;
    }

    /// <summary>Expands or collapses the menu, keeping the tab pinned to the right edge.</summary>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _collapsedPanel.Visible = !expanded;
        _expandedPanel.Visible = expanded;
        Reposition();
    }

    /// <summary>Re-anchors the tab to the right edge of the kiosk window, vertically centred.</summary>
    public void Reposition()
    {
        var b = _boundsProvider();
        Size = _expanded ? ExpandedSize : CollapsedSize;
        var left = b.Right - Width - Inset;
        var top = b.Top + (b.Height - Height) / 2;
        Location = new Point(left, top);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        const int r = 10;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, r, r, 180, 90);            // top-left
        path.AddLine(r, 0, Width, 0);                 // top edge
        path.AddLine(Width, 0, Width, Height);        // right edge
        path.AddLine(Width, Height, r, Height);       // bottom edge
        path.AddArc(0, Height - r, r, r, 90, 90);     // bottom-left
        path.CloseFigure();
        Region = new Region(path);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Reposition();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Thin accent stripe on the left edge so the tab is easy to spot on any page.
        using var pen = new Pen(Color.FromArgb(59, 130, 246), 3);
        e.Graphics.DrawLine(pen, 1, 10, 1, Height - 10);
    }
}
