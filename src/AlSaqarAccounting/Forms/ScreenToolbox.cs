using System.Text;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Shared UI helpers for the concrete screens. They contain no data access of
/// any kind; all SQL stays inside the services behind DbExecutor.
/// </summary>
internal static class ScreenToolbox
{
    public static void ExportCsv(Form owner, DataTable? data, string displayName)
    {
        if (data is null || data.Rows.Count == 0)
        {
            MessageBox.Show(owner, "لا توجد بيانات للتصدير.", "تصدير CSV",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV UTF-8 (*.csv)|*.csv",
            FileName = SanitizeFileName(displayName) + ".csv",
            AddExtension = true
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;

        var builder = new StringBuilder();
        for (var i = 0; i < data.Columns.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(EscapeCsv(data.Columns[i].Caption));
        }
        builder.AppendLine();

        foreach (DataRowView view in data.DefaultView)
        {
            for (var i = 0; i < data.Columns.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(EscapeCsv(view.Row[i] == DBNull.Value
                    ? string.Empty
                    : Convert.ToString(view.Row[i]) ?? string.Empty));
            }
            builder.AppendLine();
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
        MessageBox.Show(owner, "تم التصدير بنجاح:\r\n" + dialog.FileName, "تصدير CSV",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ShowPrintPreview(Form owner, string title, DataTable? data)
    {
        using var preview = new Form
        {
            Text = "معاينة الطباعة — " + title,
            Width = 1100,
            Height = 750,
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        var copy = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            RightToLeft = RightToLeft.Yes,
            DataSource = data?.DefaultView.ToTable()
        };
        var close = new Button { Text = "إغلاق", Dock = DockStyle.Bottom, Height = 38 };
        close.Click += (_, _) => preview.Close();
        preview.Controls.Add(copy);
        preview.Controls.Add(close);
        preview.ShowDialog(owner);
    }

    public static void ShowRecordDetails(Form owner, string title, DataGridView grid)
    {
        if (grid.CurrentRow?.DataBoundItem is not DataRowView row)
        {
            MessageBox.Show(owner, "حدد سجلًا أولاً.", "تفاصيل",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var details = new RecordDetailsForm(title, row.Row, null);
        details.ShowDialog(owner);
    }

    public static void TranslateCommonColumns(DataTable table)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ID"] = "المعرف",
            ["SN"] = "المسلسل",
            ["ItemId"] = "رقم الصنف",
            ["Item_code"] = "كود الصنف",
            ["item_Name"] = "اسم الصنف",
            ["Account_No"] = "رقم الحساب",
            ["Account_Name"] = "اسم الحساب",
            ["Main_Account_No"] = "الحساب الأب",
            ["Account_Level"] = "مستوى الحساب",
            ["BranchID"] = "الفرع",
            ["BranchName"] = "اسم الفرع",
            ["Name"] = "الاسم",
            ["Store_Name"] = "اسم المخزن",
            ["CustSuppName"] = "اسم العميل/المورد",
            ["SupplierName"] = "اسم العميل/المورد",
            ["VatNum"] = "الرقم الضريبي",
            ["Phone"] = "الهاتف",
            ["Fax"] = "الفاكس",
            ["Address"] = "العنوان",
            ["AddressA"] = "العنوان",
            ["Note"] = "الملاحظات",
            ["DocCode"] = "رقم المستند",
            ["TranDate"] = "التاريخ",
            ["Purchases_Date"] = "التاريخ",
            ["TranTypeID"] = "نوع الحركة",
            ["ReceivedFrom"] = "استلمنا من / دفعنا إلى",
            ["Money"] = "المبلغ",
            ["Net"] = "الصافي",
            ["Tax"] = "الضريبة",
            ["TotalPrices"] = "الإجمالي",
            ["Safy"] = "الصافي",
            ["CostCentersName"] = "اسم مركز التكلفة",
            ["CostCentersNo"] = "رقم مركز التكلفة",
            ["ProjectName"] = "اسم المشروع",
            ["ProjectNo"] = "رقم المشروع",
            ["profit"] = "نسبة العمولة",
            ["CreditLimit"] = "حد الائتمان",
            ["UserDate_Add"] = "تاريخ الإضافة",
            ["UserDate_Update"] = "تاريخ آخر تعديل"
        };

        foreach (DataColumn column in table.Columns)
            if (map.TryGetValue(column.ColumnName, out var arabic))
                column.Caption = arabic;
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "export" : value;
    }
}
