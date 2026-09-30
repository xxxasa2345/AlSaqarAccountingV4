using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

public sealed class LoginForm : Form
{
    private readonly AuthService _auth;
    private readonly SchemaService _schema;
    private readonly StoredProcedureExecutor _sp;
    private readonly SecurityService _security;
    private readonly string _connectionString;

    private readonly ComboBox _user = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 260
    };

    private readonly ComboBox _branch = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 260
    };

    private readonly TextBox _pass = new()
    {
        UseSystemPasswordChar = true,
        Width = 260
    };

    private readonly Button _login = new()
    {
        Text = "دخول",
        Width = 120,
        Height = 34
    };

    private readonly Label _message = new()
    {
        AutoSize = true
    };

    private DataTable? _users;
    private DataTable? _branches;

    public LoginForm(
        AuthService auth,
        SchemaService schema,
        StoredProcedureExecutor sp,
        SecurityService security,
        string connectionString)
    {
        _auth = auth;
        _schema = schema;
        _sp = sp;
        _security = security;
        _connectionString = connectionString;

        Text = "الصقر للمحاسبة - تسجيل الدخول";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Width = 500;
        Height = 310;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildLayout();

        Shown += async (_, _) => await LoadLoginDataAsync();
        _user.SelectedValueChanged += (_, _) => SyncBranchFromSelectedUser();
        _login.Click += async (_, _) => await LoginAsync();
        AcceptButton = _login;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 5,
            ColumnCount = 2
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "الصقر للمحاسبة ERP",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        root.Controls.Add(new Label
        {
            Text = "اسم المستخدم",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 1);
        root.Controls.Add(_user, 1, 1);

        root.Controls.Add(new Label
        {
            Text = "الفرع",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 2);
        root.Controls.Add(_branch, 1, 2);

        root.Controls.Add(new Label
        {
            Text = "كلمة المرور",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 3);
        root.Controls.Add(_pass, 1, 3);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true
        };
        bottom.Controls.Add(_login);
        bottom.Controls.Add(_message);
        root.Controls.Add(bottom, 0, 4);
        root.SetColumnSpan(bottom, 2);

        Controls.Add(root);
    }

    private async Task LoadLoginDataAsync()
    {
        _login.Enabled = false;

        try
        {
            _users = await _auth.GetActiveUsersAsync();
            _branches = await _auth.GetBranchesAsync();

            _user.DataSource = _users;
            _user.DisplayMember = "Name";
            _user.ValueMember = "ID";

            _branch.DataSource = _branches;
            _branch.DisplayMember = "Name";
            _branch.ValueMember = "ID";

            SelectDefaultBranch(1);
            SyncBranchFromSelectedUser();

            _message.Text = string.Empty;
            _pass.Focus();
        }
        catch (Exception ex)
        {
            _message.Text = "تعذر تحميل المستخدمين والفروع.";
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "تسجيل الدخول",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _login.Enabled = true;
        }
    }

    private void SelectDefaultBranch(int preferredBranchId)
    {
        if (_branches is null || _branches.Rows.Count == 0)
            return;

        var index = -1;
        for (var i = 0; i < _branches.Rows.Count; i++)
        {
            if (Convert.ToInt32(_branches.Rows[i]["ID"]) == preferredBranchId)
            {
                index = i;
                break;
            }
        }

        _branch.SelectedIndex = index >= 0 ? index : 0;
    }

    private void SyncBranchFromSelectedUser()
    {
        if (_user.SelectedItem is not DataRowView row || !_branch.Enabled)
            return;

        if (row.Row.IsNull("BranchID"))
            return;

        var branchId = Convert.ToInt32(row["BranchID"]);
        for (var i = 0; i < _branch.Items.Count; i++)
        {
            if (_branch.Items[i] is DataRowView branch &&
                Convert.ToInt32(branch["ID"]) == branchId)
            {
                _branch.SelectedIndex = i;
                break;
            }
        }
    }

    private async Task LoginAsync()
    {
        _login.Enabled = false;
        _message.Text = string.Empty;

        try
        {
            if (_user.SelectedValue is null ||
                _branch.SelectedValue is null)
            {
                _message.Text = "اختر المستخدم والفرع.";
                return;
            }

            var userId = Convert.ToInt32(_user.SelectedValue);
            var branchId = Convert.ToInt32(_branch.SelectedValue);

            if (string.IsNullOrEmpty(_pass.Text))
            {
                _message.Text = "أدخل كلمة المرور.";
                _pass.Focus();
                return;
            }

            var session = await _auth.LoginAsync(
                userId,
                branchId,
                _pass.Text);

            if (session is null)
            {
                _message.Text = "اسم المستخدم أو كلمة المرور أو الفرع غير صحيح.";
                _pass.Clear();
                _pass.Focus();
                return;
            }

            await _auth.RecordLoginAsync(session);

            Hide();
            using var main = new MainForm(
                session,
                _schema,
                _sp,
                _security,
                _connectionString);

            main.ShowDialog(this);
            Show();
            _pass.Clear();
            _pass.Focus();
        }
        catch (Exception ex)
        {
            _message.Text = "تعذر إتمام تسجيل الدخول.";
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "خطأ الاتصال",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _login.Enabled = true;
        }
    }
}
