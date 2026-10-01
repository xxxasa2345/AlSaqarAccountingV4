using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete current-user password change form corresponding to FrmPassword
/// in the original GTSErpSystem security module.
/// </summary>
public sealed class ChangePasswordForm : Form
{
    private readonly AppSession _session;
    private readonly SecurityAdministrationService _service;

    private readonly TextBox _currentPassword = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox _newPassword = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox _confirmPassword = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };

    public ChangePasswordForm(AppSession session, SecurityAdministrationService service)
    {
        _session = session;
        _service = service;

        Text = "تغيير كلمة المرور — FrmPassword";
        Width = 560;
        Height = 330;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildUi();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

        AddField(layout, "كلمة المرور الحالية", _currentPassword, 0);
        AddField(layout, "كلمة المرور الجديدة", _newPassword, 1);
        AddField(layout, "تأكيد كلمة المرور", _confirmPassword, 2);

        var save = new Button
        {
            Text = "حفظ كلمة المرور",
            Width = 145,
            Height = 34,
            Anchor = AnchorStyles.Left
        };
        save.Click += async (_, _) => await SaveAsync();

        var close = new Button
        {
            Text = "إغلاق",
            Width = 100,
            Height = 34
        };
        close.Click += (_, _) => Close();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(close);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);

        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} (ID: {_session.UserId})",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.DimGray
        };
        layout.Controls.Add(info, 0, 3);
        layout.SetColumnSpan(info, 2);

        Controls.Add(layout);
    }

    private static void AddField(TableLayoutPanel layout, string caption, Control control, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 9, FontStyle.Bold)
        }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!string.Equals(_newPassword.Text, _confirmPassword.Text, StringComparison.Ordinal))
                throw new InvalidOperationException("تأكيد كلمة المرور الجديدة غير مطابق.");

            UseWaitCursor = true;
            await _service.ChangePasswordAsync(
                _session.UserId,
                _currentPassword.Text,
                _newPassword.Text,
                _session);

            MessageBox.Show(this, "تم تغيير كلمة المرور بنجاح.", "كلمة المرور",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تغيير كلمة المرور:
" + ex.GetBaseException().Message,
                "كلمة المرور", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
