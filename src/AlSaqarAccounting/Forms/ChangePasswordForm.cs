using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete FrmPassword entry point for the original Security module.
/// Credential changes are intentionally not performed by this compatibility
/// form until the original password-change contract is verified.
/// </summary>
public sealed class ChangePasswordForm : Form
{
    private readonly AppSession _session;

    public ChangePasswordForm(AppSession session, object unusedService = null)
    {
        _session = session;

        Text = "كلمة المرور — FrmPassword";
        Width = 560;
        Height = 280;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };

        layout.Controls.Add(new Label
        {
            Text = "إدارة كلمة المرور",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = $"المستخدم الحالي: {_session.UserName} (ID: {_session.UserId})",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 1);

        var close = new Button
        {
            Text = "إغلاق",
            Width = 110,
            Height = 34,
            Anchor = AnchorStyles.None
        };
        close.Click += (_, _) => Close();
        layout.Controls.Add(close, 0, 2);

        Controls.Add(layout);
    }
}
