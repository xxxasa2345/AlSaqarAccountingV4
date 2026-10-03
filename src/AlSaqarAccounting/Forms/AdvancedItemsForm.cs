using System.Data;
using System.Text;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Advanced Items Management Form with full inventory control.
/// Handles items, categories, units, and stock management.
/// </summary>
public sealed class AdvancedItemsForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly ItemsService _itemsService;
    private readonly ItemUnitService _unitService;
    private readonly ItemMasterService _masterService;
    
    private readonly DataGridView _itemsGrid = new();
    private readonly DataGridView _stockGrid = new();
    private readonly TextBox _searchText = new();
    private readonly TextBox _barcodeText = new();
    private readonly TextBox _nameText = new();
    private readonly TextBox _englishNameText = new();
    private readonly ComboBox _categoryCombo = new();
    private readonly ComboBox _unitCombo = new();
    private readonly ComboBox _companyCombo = new();
    private readonly TextBox _costPriceText = new();
    private readonly TextBox _sellPriceText = new();
    private readonly TextBox _minStockText = new();
    private readonly TextBox _currentStockText = new();
    private readonly CheckBox _taxCheck = new();
    private readonly TextBox _taxValueText = new();
    private readonly TextBox _notesText = new();
    private readonly Label _status = new();
    
    private DataTable? _itemsData;
    private DataTable? _unitsData;
    private DataTable? _categoriesData;
    private DataTable? _companiesData;
    private int? _selectedItemId;

    public AdvancedItemsForm(
        AppSession session,
        ScreenAccess access,
        ItemsService itemsService,
        ItemUnitService unitService,
        ItemMasterService masterService)
    {
        _session = session;
        _access = access;
        _itemsService = itemsService;
        _unitService = unitService;
        _masterService = masterService;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        ErpTheme.ApplyForm(this);
        Text = "الصقر للمحاسبة — إدارة الأصناف المتقدمة";
        Width = 1400;
        Height = 900;
        MinimumSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header Panel
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "\u0001", // "إدارة الاصناف المتقدمة"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"\u0001: {_session.UserName} | \u0001: {_session.BranchId?.ToString() ?? "-"}", // "المستخدم: ... | الفرع: ..."
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title);
        header.Controls.Add(info);

        // Filter Panel
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        
        var filterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        
        var searchLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "بحث:"
        _searchText.Dock = DockStyle.Fill;
        _searchText.RightToLeft = RightToLeft.Yes;
        
        var barcodeLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "باركود:"
        _barcodeText.Dock = DockStyle.Fill;
        _barcodeText.RightToLeft = RightToLeft.Yes;
        _barcodeText.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SearchByBarcode(); };
        
        var refreshBtn = new Button { Text = "\u0001", Width = 80, Height = 30 }; // "تحديث"
        refreshBtn.Click += async (_, _) => await LoadItems();
        
        filterLayout.Controls.Add(searchLabel, 0, 0);
        filterLayout.Controls.Add(_searchText, 1, 0);
        filterLayout.Controls.Add(barcodeLabel, 2, 0);
        filterLayout.Controls.Add(_barcodeText, 3, 0);
        filterPanel.Controls.Add(filterLayout);

        // Split Container
        var splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 450 };
        
        // Top Panel - Items List
        var itemsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var itemsTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "قائمة الاصناف"
        
        _itemsGrid.Dock = DockStyle.Fill;
        _itemsGrid.ReadOnly = true;
        _itemsGrid.AllowUserToAddRows = false;
        _itemsGrid.AllowUserToDeleteRows = false;
        _itemsGrid.AutoGenerateColumns = true;
        _itemsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _itemsGrid.RightToLeft = RightToLeft.Yes;
        _itemsGrid.RowHeadersVisible = false;
        _itemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _itemsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) LoadItemDetails(); };
        _itemsGrid.SelectionChanged += (_, _) => LoadItemDetails();
        ErpTheme.ConfigureGrid(_itemsGrid);
        
        itemsPanel.Controls.Add(_itemsGrid);
        itemsPanel.Controls.Add(itemsTitle);
        splitContainer.Panel1.Controls.Add(itemsPanel);

        // Bottom Panel - Item Details
        var detailsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var detailsTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "تفاصيل الصنف"
        
        var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 200, ColumnCount = 4, RowCount = 4, Padding = new Padding(4) };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(detailsLayout, "\u0001", _nameText, 0, 0); // "اسم الصنف"
        AddField(detailsLayout, "\u0001", _englishNameText, 1, 0); // "الاسم الانجليزي"
        AddField(detailsLayout, "\u0001", _categoryCombo, 2, 0); // "الفئة"
        AddField(detailsLayout, "\u0001", _companyCombo, 3, 0); // "الشركة"
        AddField(detailsLayout, "\u0001", _unitCombo, 0, 1); // "وحدة القياس"
        AddField(detailsLayout, "\u0001", _barcodeText, 1, 1); // "الباركود"
        AddField(detailsLayout, "\u0001", _costPriceText, 2, 1); // "سعر التكلفة"
        AddField(detailsLayout, "\u0001", _sellPriceText, 3, 1); // "سعر البيع"
        AddField(detailsLayout, "\u0001", _minStockText, 0, 2); // "الحد الأدنى للمخزون"
        AddField(detailsLayout, "\u0001", _currentStockText, 1, 2); // "المخزون الحالي"
        AddField(detailsLayout, "", _taxCheck, 2, 2); // "خاضع للضريبة"
        AddField(detailsLayout, "\u0001", _taxValueText, 3, 2); // "قيمة الضريبة %"
        AddField(detailsLayout, "\u0001", _notesText, 0, 3, true); // "ملاحظات"
        detailsLayout.SetColumnSpan(_notesText, 3);

        // Stock Grid
        var stockLabel = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "المخزون في الفروع"
        
        _stockGrid.Dock = DockStyle.Fill;
        _stockGrid.ReadOnly = true;
        _stockGrid.AllowUserToAddRows = false;
        _stockGrid.AllowUserToDeleteRows = false;
        _stockGrid.AutoGenerateColumns = true;
        _stockGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        ErpTheme.ConfigureGrid(_stockGrid);
        _stockGrid.RightToLeft = RightToLeft.Yes;
        _stockGrid.RowHeadersVisible = false;

        var stockPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        stockPanel.Controls.Add(_stockGrid);
        stockPanel.Controls.Add(stockLabel);

        detailsPanel.Controls.Add(detailsLayout);
        detailsPanel.Controls.Add(stockPanel);
        detailsPanel.Controls.Add(detailsTitle);
        splitContainer.Panel2.Controls.Add(detailsPanel);

        // Toolbar Panel
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false, BackColor = Color.FromArgb(240, 248, 255) };
        
        AddToolbarButton(toolbar, "\u0001", _access.AllowSave, SaveItem); // "حفظ"
        AddToolbarButton(toolbar, "\u0001", _access.AllowEdit, EditItem); // "تعديل"
        AddToolbarButton(toolbar, "\u0001", _access.AllowDelete, DeleteItem); // "حذف"
        AddToolbarButton(toolbar, "\u0001", true, ImportItems); // "استيراد"
        AddToolbarButton(toolbar, "\u0001", _access.AllowExport, ExportItems); // "تصدير"
        AddToolbarButton(toolbar, "\u0001", true, PrintBarcode); // "طباعة باركود"

        // Status Bar
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.FromArgb(220, 230, 241);

        Controls.Add(header);
        Controls.Add(filterPanel);
        Controls.Add(splitContainer);
        Controls.Add(toolbar);
        Controls.Add(_status);

        // Event handlers
        _searchText.TextChanged += (_, _) => ApplyFilter();
        Shown += async (_, _) => await LoadInitialDataAsync();
    }

    private static void AddToolbarButton(FlowLayoutPanel panel, string text, bool enabled, Action action)
    {
        var button = new Button { Text = text, Width = text.Length > 8 ? 120 : 100, Height = 36, Enabled = enabled, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        ErpTheme.ConfigureToolbarButton(button);
        panel.Controls.Add(button);
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, int column, int row, bool multiLine = false)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var text = new Label { Text = label, Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleRight };
        control.Dock = DockStyle.Fill;
        control.RightToLeft = RightToLeft.Yes;
        if (multiLine && control is TextBox tb)
        {
            tb.Multiline = true;
            tb.Height = 60;
        }
        if (control is CheckBox cb)
        {
            cb.TextAlign = ContentAlignment.MiddleRight;
            box.Controls.Add(cb);
            return;
        }
        box.Controls.Add(control);
        box.Controls.Add(text);
        panel.Controls.Add(box, column, row);
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            
            _itemsData = await _itemsService.ListAsync();
            _unitsData = await _unitService.ListAsync();
            _categoriesData = await _masterService.ListAsync("Item_Class");
            _companiesData = await _masterService.ListAsync("Item_Company");
            
            // Fill combos
            FillCombo(_categoryCombo, _categoriesData, "Name", "ID");
            FillCombo(_unitCombo, _unitsData, "Name", "ID");
            FillCombo(_companyCombo, _companiesData, "Name", "ID");
            
            _itemsGrid.DataSource = _itemsData;
            FormatItemsGrid();
            
            _status.Text = "\u0001"; // "النظام جاهز"
        }
        catch (Exception ex)
        {
            _status.Text = "\u0001: " + ex.GetBaseException().Message; // "خطأ: "
        }
        finally { UseWaitCursor = false; }
    }

    private void FillCombo(ComboBox combo, DataTable data, string displayMember, string valueMember)
    {
        combo.DataSource = data;
        combo.DisplayMember = displayMember;
        combo.ValueMember = valueMember;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
    }

    private async Task LoadItems()
    {
        try
        {
            UseWaitCursor = true;
            _itemsData = await _itemsService.ListAsync();
            _itemsGrid.DataSource = _itemsData;
            FormatItemsGrid();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _status.Text = "\u0001: " + ex.GetBaseException().Message; // "خطأ في تحميل الاصناف: "
        }
        finally { UseWaitCursor = false; }
    }

    private void FormatItemsGrid()
    {
        if (_itemsGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "ItemId", "UserID_Add", "UserBranch_Add", "UserMacAddress_Add", "UserDate_Add", 
                                     "UserID_Update", "UserBranch_Update", "UserMacAddress_Update", "UserDate_Update" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_itemsGrid.Columns.Contains(colName))
                _itemsGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_itemsGrid.Columns.Contains("Item_code")) _itemsGrid.Columns["Item_code"].HeaderText = "\u0001"; // "الباركود"
        if (_itemsGrid.Columns.Contains("item_Name")) _itemsGrid.Columns["item_Name"].HeaderText = "\u0001"; // "اسم الصنف"
        if (_itemsGrid.Columns.Contains("item_Name_English")) _itemsGrid.Columns["item_Name_English"].HeaderText = "\u0001"; // "الاسم الانجليزي"
        if (_itemsGrid.Columns.Contains("SellPriceSmall")) _itemsGrid.Columns["SellPriceSmall"].HeaderText = "\u0001"; // "سعر البيع"
        if (_itemsGrid.Columns.Contains("Item_ClassId")) _itemsGrid.Columns["Item_ClassId"].HeaderText = "\u0001"; // "الفئة"
        if (_itemsGrid.Columns.Contains("Item_CompanyId")) _itemsGrid.Columns["Item_CompanyId"].HeaderText = "\u0001"; // "الشركة"
        if (_itemsGrid.Columns.Contains("Item_UnitId")) _itemsGrid.Columns["Item_UnitId"].HeaderText = "\u0001"; // "وحدة القياس"
    }

    private Item_Items BuildItemFromRow()
        => new()
        {
            Item_code = _barcodeText.Text.Trim(),
            item_Name = _nameText.Text.Trim(),
            item_Name_English = _englishNameText.Text.Trim(),
            SellPriceSmall = TryDecimal(_sellPriceText.Text),
            Is_Tax = _taxCheck.Checked,
            Tax_Value = TryDecimal(_taxValueText.Text),
            Note = _notesText.Text.Trim()
        };

    private void ApplyFilter()
    {
        if (_itemsData == null) return;
        
        var filter = string.Empty;
        var searchTerm = _searchText.Text.Trim();
        var barcodeTerm = _barcodeText.Text.Trim();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filter = $"Convert([item_Name], 'System.String') LIKE '%{searchTerm}%' OR Convert([item_Name_English], 'System.String') LIKE '%{searchTerm}%' ";
        }
        
        if (!string.IsNullOrWhiteSpace(barcodeTerm))
        {
            if (!string.IsNullOrWhiteSpace(filter))
                filter += "OR ";
            filter += $"Convert([Item_code], 'System.String') LIKE '%{barcodeTerm}%' ";
        }
        
        _itemsData.DefaultView.RowFilter = filter;
    }

    private void SearchByBarcode()
    {
        ApplyFilter();
        if (_itemsData != null && _itemsData.DefaultView.Count > 0)
        {
            _itemsGrid.CurrentCell = _itemsGrid.Rows[0].Cells[0];
            LoadItemDetails();
        }
    }

    private void LoadItemDetails()
    {
        if (_itemsGrid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;
        
        _selectedItemId = Convert.ToInt32(row.Row["ItemId"]);
        _nameText.Text = Convert.ToString(row.Row["item_Name"]) ?? string.Empty;
        _englishNameText.Text = Convert.ToString(row.Row["item_Name_English"]) ?? string.Empty;
        _barcodeText.Text = Convert.ToString(row.Row["Item_code"]) ?? string.Empty;
        _costPriceText.Text = Convert.ToString(row.Row["LastCost"]) ?? string.Empty;
        _sellPriceText.Text = Convert.ToString(row.Row["SellPriceSmall"]) ?? string.Empty;
        _minStockText.Text = Convert.ToString(row.Row["MinStock"]) ?? string.Empty;
        _currentStockText.Text = Convert.ToString(row.Row["Quantity"]) ?? string.Empty;
        _taxCheck.Checked = Convert.ToBoolean(row.Row["Is_Tax"] ?? false);
        _taxValueText.Text = Convert.ToString(row.Row["Tax_Value"] ?? 0);
        _notesText.Text = Convert.ToString(row.Row["Note"]) ?? string.Empty;
        
        // Set combos
        if (row.Row["Item_ClassId"] != DBNull.Value)
            _categoryCombo.SelectedValue = Convert.ToInt32(row.Row["Item_ClassId"]);
        if (row.Row["Item_CompanyId"] != DBNull.Value)
            _companyCombo.SelectedValue = Convert.ToInt32(row.Row["Item_CompanyId"]);
        if (row.Row["Item_UnitId"] != DBNull.Value)
            _unitCombo.SelectedValue = Convert.ToInt32(row.Row["Item_UnitId"]);
        
        // Load stock data (simplified - in real app would query database)
        LoadStockData();
    }

    private async void LoadStockData()
    {
        if (!_selectedItemId.HasValue)
        {
            _stockGrid.DataSource = null;
            return;
        }

        try
        {
            var stockData = await _itemsService.ListStockAsync(_selectedItemId.Value, _session);
            _stockGrid.DataSource = stockData;
            _status.Text = $"مخزون الصنف: {stockData.Rows.Count:N0} مخزن";
        }
        catch (Exception ex)
        {
            _stockGrid.DataSource = null;
            _status.Text = "تعذر تحميل المخزون: " + ex.GetBaseException().Message;
        }
    }

    private async void SaveItem()
    {
        if (!_access.AllowSave) return;
        try
        {
            var item = BuildItemFromEditor();
            UseWaitCursor = true;

            if (_selectedItemId.HasValue)
                await _itemsService.UpdateAsync(_selectedItemId.Value, item, _session, _access.Id);
            else
            {
                await _itemsService.CreateAsync(item, _session, _access.Id);
                ClearItemEditor();
            }

            await LoadItems();
            _status.Text = "تم حفظ الصنف في قاعدة البيانات.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "تعذر حفظ الصنف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async void EditItem()
    {
        if (!_access.AllowEdit || !_selectedItemId.HasValue)
        {
            MessageBox.Show(this, "حدد الصنف المطلوب تعديله أولاً.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await SaveItemCoreAsync();
    }

    private async void DeleteItem()
    {
        if (!_access.AllowDelete || !_selectedItemId.HasValue)
        {
            MessageBox.Show(this, "حدد الصنف المطلوب حذفه أولاً.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                this,
                $"هل تريد حذف الصنف «{_nameText.Text.Trim()}»؟",
                "تأكيد الحذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _itemsService.DeleteAsync(_selectedItemId.Value, _session, _access.Id);
            _selectedItemId = null;
            ClearItemEditor();
            await LoadItems();
            _stockGrid.DataSource = null;
            _status.Text = "تم حذف الصنف من قاعدة البيانات.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "تعذر حذف الصنف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async Task SaveItemCoreAsync()
    {
        var item = BuildItemFromEditor();
        UseWaitCursor = true;
        try
        {
            await _itemsService.UpdateAsync(_selectedItemId!.Value, item, _session, _access.Id);
            await LoadItems();
            _status.Text = "تم تعديل الصنف في قاعدة البيانات.";
        }
        finally { UseWaitCursor = false; }
    }

    private Item_Items BuildItemFromEditor()
        => new()
        {
            Item_code = _barcodeText.Text.Trim(),
            item_Name = _nameText.Text.Trim(),
            item_Name_English = _englishNameText.Text.Trim(),
            SellPriceSmall = TryDecimal(_sellPriceText.Text),
            Is_Tax = _taxCheck.Checked,
            Tax_Value = TryDecimal(_taxValueText.Text),
            Note = _notesText.Text.Trim()
        };

    private static decimal? TryDecimal(string value)
        => decimal.TryParse(value, out var number) ? number : null;

    private void ClearItemEditor()
    {
        _selectedItemId = null;
        _nameText.Clear();
        _englishNameText.Clear();
        _barcodeText.Clear();
        _costPriceText.Clear();
        _sellPriceText.Clear();
        _minStockText.Clear();
        _currentStockText.Clear();
        _taxCheck.Checked = false;
        _taxValueText.Clear();
        _notesText.Clear();
        _categoryCombo.SelectedIndex = -1;
        _unitCombo.SelectedIndex = -1;
        _companyCombo.SelectedIndex = -1;
        _stockGrid.DataSource = null;
    }

    private async void ImportItems()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (!dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "الاستيراد التشغيلي الحالي يعتمد CSV. احفظ ملف Excel بصيغة CSV ثم استورده.",
                "استيراد الأصناف",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var imported = 0;
            var skipped = 0;

            foreach (var line in File.ReadLines(dialog.FileName, Encoding.UTF8).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line);
                if (fields.Count < 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
                {
                    skipped++;
                    continue;
                }

                var item = new Item_Items
                {
                    Item_code = fields[0].Trim(),
                    item_Name = fields[1].Trim(),
                    item_Name_English = fields.Count > 2 ? fields[2].Trim() : null,
                    SellPriceSmall = fields.Count > 3 ? TryDecimal(fields[3]) : null,
                    Is_Tax = fields.Count > 4 && decimal.TryParse(fields[4], out var tax) && tax > 0,
                    Tax_Value = fields.Count > 4 ? TryDecimal(fields[4]) : null,
                    Note = fields.Count > 5 ? fields[5].Trim() : null
                };

                try
                {
                    await _itemsService.CreateAsync(item, _session, _access.Id);
                    imported++;
                }
                catch
                {
                    skipped++;
                }
            }

            await LoadItems();
            _status.Text = $"تم استيراد {imported:N0} صنف، وتجاوز {skipped:N0} سجل.";
            MessageBox.Show(
                this,
                $"تم الاستيراد بنجاح.\r\nالمضاف: {imported:N0}\r\nالمتجاوز: {skipped:N0}",
                "استيراد الأصناف",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "تعذر استيراد الأصناف",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                    quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private void ExportItems()
    {
        if (!_access.AllowExport || _itemsData == null) return;
        
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = "Items.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        
        var sb = new System.Text.StringBuilder();
        
        // Header
        var headers = new[] { "\u0001", "\u0001", "\u0001", "\u0001", "\u0001", "\u0001", "\u0001" }; 
        // "الباركود", "اسم الصنف", "الاسم الانجليزي", "الفئة", "سعر التكلفة", "سعر البيع", "المخزون"
        sb.AppendLine(string.Join(",", headers));
        
        // Data
        foreach (DataRowView view in _itemsData.DefaultView)
        {
            var values = new[]
            {
                view.Row["Item_code"]?.ToString() ?? string.Empty,
                view.Row["item_Name"]?.ToString() ?? string.Empty,
                view.Row["item_Name_English"]?.ToString() ?? string.Empty,
                view.Row["Item_ClassId"]?.ToString() ?? string.Empty,
                view.Row["LastCost"]?.ToString() ?? string.Empty,
                view.Row["SellPriceSmall"]?.ToString() ?? string.Empty,
                view.Row["Quantity"]?.ToString() ?? string.Empty
            };
            sb.AppendLine(string.Join(",", values.Select(v => EscapeCsv(v))));
        }
        
        File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        _status.Text = "\u0001: " + dialog.FileName; // "تم التصدير إلى: ..."
    }

    private void PrintBarcode()
    {
        if (!_selectedItemId.HasValue || _itemsGrid.CurrentRow?.DataBoundItem is not DataRowView view)
        {
            MessageBox.Show(
                this,
                "حدد صنفاً أولاً.",
                "طباعة الباركود",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var table = new DataTable();
        foreach (DataColumn sourceColumn in view.Row.Table.Columns)
            table.Columns.Add(sourceColumn.ColumnName, sourceColumn.DataType);

        table.Rows.Add(view.Row.ItemArray);
        ScreenToolbox.ShowPrintPreview(this, $"بطاقة الصنف — {_nameText.Text.Trim()}", table);
        _status.Text = "تم فتح معاينة طباعة بطاقة الصنف.";
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
