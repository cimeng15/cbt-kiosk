using System.Drawing;
using System.Windows.Forms;

namespace CbtKiosk;

/// <summary>Modal password prompt used for the quit/unlock flow and for the settings window.</summary>
public sealed class PasswordPrompt : Form
{
    private readonly TextBox _input;
    public string EnteredPassword => _input.Text;

    public PasswordPrompt(string title, string message, IWin32Window owner = null)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(420, 190);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.White;

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 12F),
            Location = new Point(20, 16),
            AutoSize = true,
        };

        var lblMessage = new Label
        {
            Text = message,
            Location = new Point(20, 50),
            Size = new Size(380, 40),
        };

        _input = new TextBox
        {
            UseSystemPasswordChar = true,
            Location = new Point(20, 95),
            Width = 380,
            Font = new Font("Segoe UI", 11F),
        };
        _input.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; e.SuppressKeyPress = true; }
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(220, 138),
            Size = new Size(85, 32),
        };

        var btnCancel = new Button
        {
            Text = "Batal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(315, 138),
            Size = new Size(85, 32),
        };

        Controls.AddRange(new Control[] { lblTitle, lblMessage, _input, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ActiveControl = _input;

        Shown += (s, e) => { Activate(); _input.Focus(); };

        if (owner != null)
        {
            StartPosition = FormStartPosition.Manual;
            Shown += (s, e) =>
            {
                var b = owner is Control c ? c.RectangleToScreen(c.ClientRectangle)
                                           : Screen.FromHandle(owner.Handle).WorkingArea;
                Location = new Point(b.Left + (b.Width - Width) / 2, b.Top + (b.Height - Height) / 2);
            };
        }
    }
}
