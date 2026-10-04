using System.Drawing;
using System.Windows.Forms;

namespace AlSaqarAccounting.UI;

public static class ErpTheme
{
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceSoft = Color.White;
    public static readonly Color Border = Color.FromArgb(220, 220, 220);
    public static readonly Color Text = Color.Black;
    public static readonly Color Muted = Color.Black;
    public static readonly Color Accent = Color.Black;
    public static readonly Color AccentSoft = Color.FromArgb(242, 242, 242);
    public static readonly Color Navigation = Color.White;
    public static readonly Color NavigationText = Color.Black;
    public static readonly Color NavigationMuted = Color.Black;

    public static Font RegularFont => new("Tahoma", 9.5f);
    public static Font TitleFont => new("Tahoma", 18f, FontStyle.Bold);

    public static void ApplyForm(Form form)
    {
        form.BackColor = Color.White;
        form.ForeColor = Color.Black;
        form.Font = RegularFont;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
    }

    public static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Tahoma", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Color.Black;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 235, 235);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.RowTemplate.Height = 34;
    }

    public static void ConfigureToolbarButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(242, 242, 242);
        button.BackColor = Color.White;
        button.ForeColor = Color.Black;
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
            BackColor = Color.White,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(12, 0, 12, 0),
            Cursor = Cursors.Hand,
            Font = new Font("Tahoma", 9.5f, FontStyle.Regular)
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(242, 242, 242);
        return button;
    }

    public static Panel CreateCard(string title, string value, string? hint = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(7),
            BackColor = Color.White,
            Padding = new Padding(14),
            BorderStyle = BorderStyle.FixedSingle
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Tahoma", 17f, FontStyle.Bold),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleRight
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            ForeColor = Color.Black,
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
                ForeColor = Color.Black,
                TextAlign = ContentAlignment.MiddleRight
            };
            card.Controls.Add(hintLabel);
        }

        return card;
    }

    public static void StyleRecursive(Control root)
    {
        root.BackColor = Color.White;
        root.ForeColor = Color.Black;

        foreach (Control control in root.Controls)
        {
            control.BackColor = Color.White;
            control.ForeColor = Color.Black;

            if (control is DataGridView grid)
                ConfigureGrid(grid);
            else if (control is Button button)
                ConfigureToolbarButton(button);

            if (control.HasChildren)
                StyleRecursive(control);
        }
    }
}