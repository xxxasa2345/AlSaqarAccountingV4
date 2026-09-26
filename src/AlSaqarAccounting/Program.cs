using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.Forms;

namespace AlSaqarAccounting;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        builder.Services.AddSingleton<SqlConnectionFactory>();
        builder.Services.AddSingleton<StoredProcedureExecutor>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<SchemaService>();
        builder.Services.AddSingleton<SecurityService>();

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Application.Run(new LoginForm(
            scope.ServiceProvider.GetRequiredService<AuthService>(),
            scope.ServiceProvider.GetRequiredService<SchemaService>(),
            scope.ServiceProvider.GetRequiredService<StoredProcedureExecutor>(),
            scope.ServiceProvider.GetRequiredService<SecurityService>()));
    }
}
