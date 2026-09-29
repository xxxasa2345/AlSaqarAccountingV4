using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Small concrete-screen base for documents that already have verified
/// list/detail database operations. It is not a fallback catalog screen:
/// each ERP screen derives from it and supplies its own title and service calls.
/// </summary>
public abstract class ProcedureDocumentBrowseForm : BrowseScreenBase
{
    protected ProcedureDocumentBrowseForm(AppSession session, ScreenAccess access)
        : base(session, access)
    {
    }

    protected virtual bool SupportsDocumentDetails => true;

    protected abstract Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddAsyncButton(
            toolbar,
            "فتح المستند",
            SupportsDocumentDetails && Access.AllowEnter,
            ShowDocumentDetailsAsync);
    }

    private async Task ShowDocumentDetailsAsync()
    {
        var row = CurrentRow;
        if (row is null || !TryRowId(row, out _))
        {
            MessageBox.Show(this, "حدد مستندًا أولاً.", "تفاصيل المستند",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var data = await LoadDocumentDetailsAsync(row);
            if (data is null || data.Rows.Count == 0)
            {
                MessageBox.Show(this, "لا توجد تفاصيل لهذا المستند.", "تفاصيل المستند",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ScreenToolbox.TranslateCommonColumns(data);
            ScreenToolbox.ShowPrintPreview(this, ScreenTitle + " — التفاصيل", data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر تحميل تفاصيل المستند:
" + ex.GetBaseException().Message,
                "تفاصيل المستند",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    protected static void AddAsyncButton(
        FlowLayoutPanel toolbar,
        string text,
        bool enabled,
        Func<Task> action,
        int width = 115)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += async (_, _) => await action();
        toolbar.Controls.Add(button);
    }
}
