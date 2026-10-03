using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Collections.Generic;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational purchase invoices screen (فواتير المشتريات). Lists through the
/// original Select_Order_Purchases procedure, creates invoices through
/// PurchasesEntryForm → Insert_Order_Purchases, and deletes through
/// Delete_Order_Purchases.
/// </summary>
public sealed class PurchasesForm : BrowseScreenBase
{
    private readonly PurchasesService _purchases;
    private readonly StoresService _stores;
    private readonly CustSupService _custSup;
    private readonly ScreenAccessResolver _accessResolver;

    public PurchasesForm(
        AppSession session,
        ScreenAccess access,
        PurchasesService purchases,
        StoresService stores,
        CustSupService custSup)
        : base(session, access)
    {
        _purchases = purchases;
        _stores = stores;
        _custSup = custSup;
        _accessResolver = new ScreenAccessResolver(
            new SqlConnectionFactory(_purchases.ConnectionString));
    }

    protected override string ScreenTitle => "فواتير المشتريات";

    protected override Task<DataTable> LoadDataAsync()
        => _purchases.ListAsync(Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddButton(toolbar, "فاتورة جديدة", Access.AllowSave, async () =>
        {
            using var form = new PurchasesEntryForm(Session, Access, _purchases, _stores, _custSup);
            form.ShowDialog(this);
            if (form.Saved)
                await ReloadAsync();
        });

        AddButton(toolbar, "مورد جديد", Access.AllowEnter, async () => await OpenRelatedAsync("الموردون"));

        AddButton(toolbar, "طباعة", Access.AllowPrint, async () => await PrintSelectedInvoiceAsync());

        AddButton(toolbar, "مرتجع مشتريات", Access.AllowEnter, async () => await OpenRelatedAsync("مرتجعات المشتريات بفاتورة"));
        AddButton(toolbar, "فتح المبيعات", Access.AllowEnter, async () => await OpenRelatedAsync("المبيعات"));
        AddButton(toolbar, "المخازن", Access.AllowEnter, async () => await OpenRelatedAsync("المخازن"));

        AddButton(toolbar, "حذف الفاتورة", Access.AllowDelete, async () => await DeleteInvoiceAsync());
    }

    private string GetConnectionString()
    {
        // BrowseScreenBase لا يعرّض سلسلة الاتصال مباشرة؛ نستخرجها من نفس DbExecutor
        // المستخدمة في الخدمة الحالية عبر Session's configured database connection.
        return _purchases.ConnectionString;
    }

    private async Task OpenRelatedAsync(string screenName)
    {
        try
        {
            UseWaitCursor = true;
            var target = await _accessResolver.GetAsync(Session, screenName);

            if (target is null)
            {
                Status.Text = $"لا توجد صلاحية لفتح «{screenName}».";
                return;
            }

            var router = new ScreenRouter(_purchases.ConnectionString, Session);
            if (!router.TryOpen(this, target, out var message))
                Status.Text = string.IsNullOrWhiteSpace(message) ? "تعذر فتح الشاشة المرتبطة." : message;
            else
                Status.Text = $"تم فتح «{screenName}».";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر فتح العملية المرتبطة:\r\n" + ex.GetBaseException().Message,
                "المشتريات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task PrintSelectedInvoiceAsync()
    {
        var row = CurrentRow;
        if (!TryRowId(row, out var invoiceId))
        {
            Status.Text = "حدد فاتورة مشتريات أولاً.";
            return;
        }

        try
        {
            UseWaitCursor = true;
            var data = await _purchases.PrintAsync(invoiceId, Session.BranchId);

            using var document = new PrintDocument();
            document.DocumentName = $"فاتورة مشتريات {invoiceId}";

            document.PrintPage += (_, e) =>
            {
                using var titleFont = new Font("Tahoma", 16, FontStyle.Bold);
                using var headerFont = new Font("Tahoma", 11, FontStyle.Bold);
                using var bodyFont = new Font("Tahoma", 9, FontStyle.Regular);

                var y = (float)e.MarginBounds.Top;
                var right = (float)e.MarginBounds.Right;

                void DrawRight(string value, Font font)
                {
                    var size = e.Graphics.MeasureString(value, font);
                    e.Graphics.DrawString(value, font, Brushes.Black, right - size.Width, y);
                    y += size.Height + 5;
                }

                DrawRight("شركة الصقر للمحاسبة", titleFont);
                DrawRight($"فاتورة مشتريات رقم {invoiceId}", headerFont);

                foreach (DataRow dataRow in data.Rows)
                {
                    var parts = new List<string>();

                    foreach (DataColumn column in data.Columns)
                    {
                        if (dataRow[column] == DBNull.Value)
                            continue;

                        var value = Convert.ToString(dataRow[column]);
                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        parts.Add($"{column.ColumnName}: {value}");
                    }

                    if (parts.Count == 0)
                        continue;

                    DrawRight(string.Join(" | ", parts), bodyFont);

                    if (y >= e.MarginBounds.Bottom - 30)
                    {
                        e.HasMorePages = true;
                        return;
                    }
                }

                e.HasMorePages = false;
            };

            using var dialog = new PrintDialog
            {
                Document = document,
                UseEXDialog = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                document.Print();

            Status.Text = $"تم تجهيز طباعة الفاتورة {invoiceId}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "تعذر طباعة الفاتورة:\r\n" + ex.GetBaseException().Message,
                "طباعة المشتريات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task DeleteInvoiceAsync()
    {
        var row = CurrentRow;
        if (!TryRowId(row, out var invoiceId))
        {
            Status.Text = "حدد الفاتورة المطلوب حذفها أولاً.";
            return;
        }

        var label = RowValue(row, "SupplierName", "CustSuppName", "Name")?.ToString() ?? string.Empty;
        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف فاتورة المشتريات رقم {invoiceId}؟" +
            (string.IsNullOrWhiteSpace(label) ? string.Empty : $"\r\n{label}") +
            "\r\nسيتم حذف تفاصيلها أيضاً عبر الإجراء الأصلي Delete_Order_Purchases.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _purchases.DeleteAsync(invoiceId, Session, Access.Id);
            Status.Text = $"تم حذف فاتورة المشتريات {invoiceId}.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف الفاتورة:\r\n" + ex.GetBaseException().Message,
                "حذف الفاتورة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static void AddButton(FlowLayoutPanel toolbar, string text, bool enabled, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            Width = 115,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += async (_, _) => await action();
        toolbar.Controls.Add(button);
    }
}
