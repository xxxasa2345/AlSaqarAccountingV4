using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

public sealed class LicenseActivationForm : Form
{
    private readonly LicenseService _service;
    private readonly TextBox _key = new() { Dock = DockStyle.Top, Height = 34 };
    private readonly Label _message = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray };

    public LicenseActivationForm(LicenseService service)
    {
        _service = service;
        Text = "تفعيل ترخيص الصقر للمحاسبة";
        Width = 560; Height = 280;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Font = new Font("Tahoma", 10);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        root.Controls.Add(new Label { Text = "أدخل مفتاح الترخيص", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Tahoma", 12, FontStyle.Bold) }, 0, 0);
        root.Controls.Add(_key, 0, 1);
        var activate = new Button { Text = "تفعيل", Width = 120, Height = 38, Anchor = AnchorStyles.None };
        activate.Click += async (_, _) => await ActivateAsync();
        root.Controls.Add(activate, 0, 2);
        root.Controls.Add(_message, 0, 3);
        root.Controls.Add(new Label { Text = $"الجهاز: {LicenseService.GetMachineFingerprint()}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray }, 0, 4);
        Controls.Add(root);
    }

    private async Task ActivateAsync()
    {
        try
        {
            await _service.ActivateAsync(_key.Text);
            var status = await _service.ValidateInstalledAsync();
            _message.Text = status.Message;
            if (status.IsValid)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            _message.Text = ex.GetBaseException().Message;
        }
    }
}
