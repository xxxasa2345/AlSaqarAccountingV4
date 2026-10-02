using System.Drawing;
using System.Windows.Forms;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Shared visual language for the Arabic ERP shell. This class changes only
/// presentation; it does not contain business logic or database access.
/// </summary>
public static class ErpTheme
{
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceSoft = Color.FromArgb(247, 249, 252);
    public static readonly Color Border = Color.FromArgb(218, 224, 232);
    public static readonly Color Text = Color.FromArgb(35, 43, 54);
    public static readonly Color Muted = Color.FromArgb(104, 114, 127);
    public static readonly Color Accent = Color.FromArgb(24, 78, 119);
    public static readonly Color AccentSoft = Color.FromArgb(231, 240, 248);
    public static readonly Color Navigation = Color.FromArgb(28, 39, 53);
    public static readonly Color NavigationText = Color.FromArgb(239, 243, 248);
    public static readonly Color NavigationMuted = Color.FromArgb(177, 188, 201);

    public static Font RegularFont => new("Tahoma", 9.5f);
    public static Font TitleFont => new("Tahoma", 18f, FontStyle.Bold);

    public static void ApplyForm(Form form)
    {
        form.BackColor = SurfaceSoft;
        form.ForeColor = Text;
        form.Font = RegularFont;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
    }

    public static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceSoft;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Tahoma", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.RowTemplate.Height = 34;
    }

    public static void ConfigureToolbarButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentSoft : SurfaceSoft;
        button.BackColor = primary ? Accent : Surface;
        button.ForeColor = primary ? Color.White : Text;
        button.Font = new Font("Tahoma", 9f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    public static Button CreateNavigationButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 228,
            Height = 42,
            Margin = new Padding(6, 3, 6, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = Navigation,
            ForeColor = NavigationText,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(12, 0, 12, 0),
            Cursor = Cursors.Hand,
            Font = new Font("Tahoma", 9.5f, FontStyle.Regular)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 56, 76);
        return button;
    }

    public static Panel CreateCard(string title, string value, string? hint = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(7),
            BackColor = Surface,
            Padding = new Padding(14),
            BorderStyle = BorderStyle.FixedSingle
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Tahoma", 17f, FontStyle.Bold),
            ForeColor = Accent,
            TextAlign = ContentAlignment.MiddleRight
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            ForeColor = Text,
            TextAlign = ContentAlignment.MiddleRight
        };

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var hintLabel = new Label
            {
                Text = hint,
                Dock = DockStyle.Bottom,
                Height = 22,
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleRight
            };
            card.Controls.Add(hintLabel);
        }

        return card;
    }

    public static void StyleRecursive(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is DataGridView grid)
                ConfigureGrid(grid);
            else if (control is Button button)
                ConfigureToolbarButton(button);

            if (control.HasChildren)
                StyleRecursive(control);
        }
    }
}
