using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class PrinterSettingsForm : Form
{
    readonly AppSession _session; readonly ScreenAccess _access; readonly PrinterSettingsService _service;
    readonly DataGridView _grid=new(); readonly ComboBox _printer=new(); readonly ComboBox _item=new();
    readonly Label _status=new(); readonly TextBox _name=new(); readonly TextBox _ip=new(); readonly TextBox _port=new();
    DataTable? _printers; DataTable? _items; DataTable? _maps; int? _printerId;

    public PrinterSettingsForm(AppSession session,ScreenAccess access,PrinterSettingsService service)
    {
        _session=session;_access=access;_service=service;
        Text="إعدادات طابعات الكاشير والمطبخ"; Width=1250; Height=760; MinimumSize=new Size(1050,650);
        StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;
        Build(); Shown+=async(_,_)=>{await LoadPrinters();await LoadMappings();};
    }

    void Build()
    {
        var header=new Panel{Dock=DockStyle.Top,Height=72,BackColor=Color.FromArgb(28,54,86),Padding=new Padding(12)};
        header.Controls.Add(new Label{Text="إعدادات طابعات الكاشير والمطبخ",Dock=DockStyle.Top,Height=32,
            ForeColor=Color.White,Font=new Font("Tahoma",16,FontStyle.Bold),TextAlign=ContentAlignment.MiddleRight});
        header.Controls.Add(new Label{Text=$"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString()??"-"}",
            Dock=DockStyle.Fill,ForeColor=Color.WhiteSmoke,TextAlign=ContentAlignment.MiddleRight});

        var tabs=new TabControl{Dock=DockStyle.Fill};
        var p1=new TabPage("الطابعات"); var p2=new TabPage("ربط الطابعات بالأصناف");
        p1.Controls.Add(BuildPrinterPage()); p2.Controls.Add(BuildMappingPage()); tabs.TabPages.Add(p1);tabs.TabPages.Add(p2);
        _status.Dock=DockStyle.Bottom;_status.Height=32;_status.BorderStyle=BorderStyle.FixedSingle;_status.Padding=new Padding(8);_status.TextAlign=ContentAlignment.MiddleRight;
        Controls.Add(tabs);Controls.Add(_status);Controls.Add(header);
    }

    Control BuildPrinterPage()
    {
        var panel=new Panel{Dock=DockStyle.Fill};
        var edit=new TableLayoutPanel{Dock=DockStyle.Top,Height=105,ColumnCount=6,RowCount=2,Padding=new Padding(8)};
        for(int i=0;i<6;i++)edit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,16.666f));
        edit.Controls.Add(Field("اسم الطابعة",_name),0,0);edit.Controls.Add(Field("عنوان IP / الاسم",_ip),1,0);edit.Controls.Add(Field("المنفذ",_port),2,0);
        var n=Btn("جديدة",_access.AllowSave); n.Click+=(_,_)=>ClearPrinter();
        var s=Btn("حفظ",_access.AllowSave||_access.AllowEdit); s.Click+=async(_,_)=>await SavePrinter();
        var d=Btn("حذف",_access.AllowDelete); d.Click+=async(_,_)=>await DeletePrinter();
        var r=Btn("تحديث",true); r.Click+=async(_,_)=>await LoadPrinters();
        edit.Controls.Add(n,3,0);edit.Controls.Add(s,4,0);edit.Controls.Add(d,5,0);edit.Controls.Add(r,5,1);
        _grid.Dock=DockStyle.Fill;_grid.ReadOnly=true;_grid.AllowUserToAddRows=false;_grid.AllowUserToDeleteRows=false;
        _grid.AutoGenerateColumns=true;_grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;_grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_grid.RowHeadersVisible=false;_grid.RightToLeft=RightToLeft.Yes;
        _grid.SelectionChanged+=(_,_)=>BindPrinter();
        panel.Controls.Add(_grid);panel.Controls.Add(edit);return panel;
    }

    Control BuildMappingPage()
    {
        var panel=new Panel{Dock=DockStyle.Fill}; var top=new TableLayoutPanel{Dock=DockStyle.Top,Height=110,ColumnCount=4,RowCount=2,Padding=new Padding(8)};
        for(int i=0;i<4;i++)top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        _printer.DropDownStyle=ComboBoxStyle.DropDownList;_printer.Dock=DockStyle.Fill;
        _item.DropDownStyle=ComboBoxStyle.DropDownList;_item.Dock=DockStyle.Fill;
        top.Controls.Add(Field("الطابعة",_printer),0,0);top.Controls.Add(Field("الصنف",_item),1,0);
        var a=Btn("إضافة ربط",_access.AllowSave);a.Click+=async(_,_)=>await AddMap();
        var d=Btn("حذف الربط",_access.AllowDelete);d.Click+=async(_,_)=>await DeleteMap();
        var r=Btn("تحديث",true);r.Click+=async(_,_)=>await LoadMappings();
        top.Controls.Add(a,2,0);top.Controls.Add(d,3,0);top.Controls.Add(r,3,1);
        panel.Controls.Add(top);
        var grid2=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,RightToLeft=RightToLeft.Yes};
        grid2.SelectionChanged+=(_,_)=>{if(grid2.CurrentRow?.DataBoundItem is DataRowView v){Sel(_printer,Convert.ToInt32(v.Row["PrinterID"]));Sel(_item,Convert.ToInt32(v.Row["ItemID"]));}};
        panel.Controls.Add(grid2); _mapGrid=grid2; return panel;
    }
    DataGridView? _mapGrid;

    async Task LoadPrinters(){try{_printers=await _service.ListPrintersAsync(_session.BranchId);_grid.DataSource=_printers;Translate(_printers);FillPrinters();_status.Text=$"الطابعات: {_printers.Rows.Count:N0}";}catch(Exception e){Err(e,"تحميل الطابعات");}}
    async Task LoadMappings(){try{_items??=await _service.ListItemsAsync();FillItems();_maps=await _service.ListCookMappingsAsync(_session.BranchId);_mapGrid!.DataSource=_maps;Translate(_maps);_status.Text=$"روابط الكاشير/المطبخ: {_maps.Rows.Count:N0}";}catch(Exception e){Err(e,"تحميل روابط الطابعات");}}

    async Task SavePrinter(){if(_session.BranchId is null||(_printerId.HasValue?!_access.AllowEdit:!_access.AllowSave))return;try{_printerId=await _service.SavePrinterAsync(_name.Text,_ip.Text,_port.Text,_session.BranchId,_printerId);await LoadPrinters();_status.Text=$"تم حفظ الطابعة رقم {_printerId}";}catch(Exception e){Err(e,"حفظ الطابعة");}}
    async Task DeletePrinter(){if(!_access.AllowDelete||_session.BranchId is null||_grid.CurrentRow?.DataBoundItem is not DataRowView v)return;var id=Convert.ToInt32(v.Row["ID"]);if(MessageBox.Show(this,"حذف الطابعة المحددة؟","تأكيد",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;try{await _service.DeletePrinterAsync(id,_session.BranchId);ClearPrinter();await LoadPrinters();}catch(Exception e){Err(e,"حذف الطابعة");}}
    async Task AddMap(){if(!_access.AllowSave||_session.BranchId is null||!Get(_printer,out var p)||!Get(_item,out var i)){MessageBox.Show(this,"حدد الطابعة والصنف.","تنبيه");return;}try{await _service.AddCookMappingAsync(p,i,_session.BranchId);await LoadMappings();}catch(Exception e){Err(e,"حفظ الربط");}}
    async Task DeleteMap(){if(!_access.AllowDelete||_session.BranchId is null||_mapGrid?.CurrentRow?.DataBoundItem is not DataRowView v)return;var id=Convert.ToInt32(v.Row["ID"]);if(MessageBox.Show(this,"حذف الربط المحدد؟","تأكيد",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;try{await _service.DeleteCookMappingAsync(id,_session.BranchId);await LoadMappings();}catch(Exception e){Err(e,"حذف الربط");}}

    void BindPrinter(){if(_grid.CurrentRow?.DataBoundItem is not DataRowView v)return;_printerId=Convert.ToInt32(v.Row["ID"]);_name.Text=Convert.ToString(v.Row["Name"])??"";_ip.Text=Convert.ToString(v.Row["IPAddress"])??"";_port.Text=Convert.ToString(v.Row["PortNum"])??"";}
    void ClearPrinter(){_printerId=null;_name.Clear();_ip.Clear();_port.Clear();_name.Focus();}
    void FillPrinters(){if(_printers is null)return;_printer.DataSource=_printers.DefaultView.ToTable(false,"ID","Name");_printer.DisplayMember="Name";_printer.ValueMember="ID";}
    void FillItems(){if(_items is null)return;_item.DataSource=_items.DefaultView.ToTable(false,"ItemID","Item_code","item_Name");_item.DisplayMember="item_Name";_item.ValueMember="ItemID";}
    static bool Get(ComboBox c,out int id){id=0;return c.SelectedValue!=null&&int.TryParse(Convert.ToString(c.SelectedValue),out id)&&id>0;}
    static void Sel(ComboBox c,int id){if(c.DataSource is DataTable t){foreach(DataRow r in t.Rows)if(Convert.ToInt32(r[c.ValueMember])==id){c.SelectedValue=id;return;}}}
    static Panel Field(string label,Control c){var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(4)};p.Controls.Add(c);p.Controls.Add(new Label{Text=label,Dock=DockStyle.Top,Height=22,TextAlign=ContentAlignment.MiddleRight});c.RightToLeft=RightToLeft.Yes;return p;}
    static Button Btn(string t,bool e)=>new(){Text=t,Width=120,Height=32,Enabled=e,Margin=new Padding(4)};
    void Err(Exception e,string where)=>MessageBox.Show(this,"تعذر "+where+":\r\n"+e.GetBaseException().Message,"خطأ",MessageBoxButtons.OK,MessageBoxIcon.Error);
    static void Translate(DataTable t){var m=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"ID","المعرف"},{"Name","اسم الطابعة"},{"BranchID","الفرع"},{"IPAddress","عنوان IP"},{"PortNum","المنفذ"},{"PrinterID","رقم الطابعة"},{"PrinterName","الطابعة"},{"ItemID","رقم الصنف"},{"Item_code","كود الصنف"},{"item_Name","اسم الصنف"}};foreach(DataColumn c in t.Columns)if(m.TryGetValue(c.ColumnName,out var a))c.Caption=a;}
}