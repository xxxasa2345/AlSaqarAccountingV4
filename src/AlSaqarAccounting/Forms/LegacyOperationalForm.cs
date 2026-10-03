using System.Windows.Forms;
using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete DB-backed browse screen for a legacy module whose original
/// branch-scoped SELECT procedure is already verified in the GTSdb catalog.
/// It is intentionally read-only until the original write contract is migrated.
/// </summary>
public sealed class LegacyOperationalForm : BrowseScreenBase
{
    private readonly LegacyOperationalService _service;
    private readonly string _procedureName;
    private readonly LegacyOperationMode _mode;
    private readonly string _title;

    public LegacyOperationalForm(
        AppSession session,
        ScreenAccess access,
        LegacyOperationalService service,
        string title,
        string procedureName,
        LegacyOperationMode mode = LegacyOperationMode.Branch)
        : base(session, access)
    {
        _service = service;
        _title = title;
        _procedureName = procedureName;
        _mode = mode;
    }

    protected override string ScreenTitle => _title;

    protected override Task<DataTable> LoadDataAsync()
    {
        return _mode switch
        {
            LegacyOperationMode.Checkout =>
                _service.ListCheckoutAsync(Session.BranchId),

            LegacyOperationMode.RestaurantDelivery =>
                _service.ListRestaurantDeliveryAsync(Session.BranchId),

            _ =>
                _service.ListBranchAsync(_procedureName, Session.BranchId)
        };
    }

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        var state = new Label
        {
            Text = "مصدر البيانات: إجراء النظام الأصلي",
            AutoSize = true,
            Height = 30,
            Padding = new Padding(10, 7, 10, 0),
            ForeColor = ErpTheme.Muted,
            Margin = new Padding(8, 5, 0, 5)
        };
        toolbar.Controls.Add(state);
    }
}

public enum LegacyOperationMode
{
    Branch,
    Checkout,
    RestaurantDelivery
}
