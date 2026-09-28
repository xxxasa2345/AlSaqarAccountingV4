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
    private readonly TextBox _user = new();
    private readonly TextBox _pass = new();
    private readonly Button _login = new();

    public LoginForm(AuthService auth, SchemaService schema, StoredProcedureExecutor sp, SecurityService security, string connectionString)
    {
        _auth = auth; _schema = schema; _sp = sp; _security = security; _connectionString = connectionString;
        Text = "الصقر للمحاسبة - تسجيل الدخول";
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Width = 420; Height = 230; StartPosition = FormStartPosition.CenterScreen;

        var panel = new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(20), RowCount=3, ColumnCount=2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        _pass.UseSystemPasswordChar = true;
        _login.Text = "دخول";
        _login.Dock = DockStyle.Fill;
        _login.Click += Login_Click;

        panel.Controls.Add(new Label{Text="اسم المستخدم",AutoSize=true},0,0);
        panel.Controls.Add(_user,1,0);
        panel.Controls.Add(new Label{Text="كلمة المرور",AutoSize=true},0,1);
        panel.Controls.Add(_pass,1,1);
        panel.Controls.Add(_login,1,2);
        Controls.Add(panel);
        AcceptButton = _login;
    }

    private async void Login_Click(object? sender, EventArgs e)
    {
        _login.Enabled = false;
        try
        {
            var session = await _auth.LoginAsync(_user.Text.Trim(), _pass.Text);
            if (session is null)
            {
                MessageBox.Show("بيانات الدخول غير صحيحة.", "تسجيل الدخول",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Hide();
            using var main = new MainForm(session, _schema, _sp, _security, _connectionString);
            main.ShowDialog(this);
            Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ الاتصال", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _login.Enabled = true; }
    }
}
