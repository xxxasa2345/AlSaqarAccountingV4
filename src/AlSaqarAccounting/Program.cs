using Microsoft.Extensions.Configuration;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.Forms;

namespace AlSaqarAccounting;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var connectionFactory = new SqlConnectionFactory(configuration);
            var storedProcedures = new StoredProcedureExecutor(connectionFactory);
            var auth = new AuthService(connectionFactory);
            var schema = new SchemaService(connectionFactory);
            var security = new SecurityService(connectionFactory);

            Application.Run(new LoginForm(auth, schema, storedProcedures, security, connectionFactory.ConnectionString));
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "startup-error.log");

                File.WriteAllText(
                    logPath,
                    $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n\r\n{ex}");
            }
            catch
            {
                // Ignore logging failures.
            }

            MessageBox.Show(
                ex.ToString(),
                "خطأ عند تشغيل AlSaqarAccounting",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
