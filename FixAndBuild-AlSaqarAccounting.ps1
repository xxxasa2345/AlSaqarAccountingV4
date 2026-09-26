$ErrorActionPreference = 'Stop'

# AlSaqarAccounting V4 - startup fix + build + publish + run
# Run this script from anywhere. It discovers the project root from its own location.

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectRoot 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$SolutionFile = Join-Path $ProjectRoot 'AlSaqarAccounting.sln'
$ProgramFile = Join-Path $ProjectRoot 'src\AlSaqarAccounting\Program.cs'
$PublishDir = Join-Path $ProjectRoot 'publish'
$BackupDir = Join-Path $ProjectRoot 'backup'
$StartupLog = Join-Path $PublishDir 'startup-error.log'

Write-Host ''
Write-Host '=== AlSaqarAccounting V4: Startup Fix / Build / Publish / Run ===' -ForegroundColor Cyan
Write-Host "Project: $ProjectRoot"
Write-Host ''

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK غير موجود في PATH. ثبّت .NET 8 SDK ثم أعد تشغيل PowerShell.'
}

if (-not (Test-Path $ProjectFile)) {
    throw "لم يتم العثور على المشروع: $ProjectFile"
}

if (-not (Test-Path $SolutionFile)) {
    throw "لم يتم العثور على الحل: $SolutionFile"
}

# Backup current Program.cs before changing it.
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$BackupProgram = Join-Path $BackupDir ("Program.cs.{0}.bak" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
Copy-Item $ProgramFile $BackupProgram -Force
Write-Host "Backup: $BackupProgram" -ForegroundColor DarkGray

# Replace startup entry point with a resilient version that logs startup exceptions.
$ProgramContent = @'
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
        try
        {
            ApplicationConfiguration.Initialize();

            var basePath = AppContext.BaseDirectory;

            var builder = Host.CreateApplicationBuilder(
                new HostApplicationBuilderSettings
                {
                    ContentRootPath = basePath
                });

            builder.Configuration
                .SetBasePath(basePath)
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false);

            builder.Services.AddSingleton<SqlConnectionFactory>();
            builder.Services.AddSingleton<StoredProcedureExecutor>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<SchemaService>();

            using var host = builder.Build();
            using var scope = host.Services.CreateScope();

            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
            var schema = scope.ServiceProvider.GetRequiredService<SchemaService>();
            var sp = scope.ServiceProvider.GetRequiredService<StoredProcedureExecutor>();

            Application.Run(new LoginForm(auth, schema, sp));
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "startup-error.log");

                File.WriteAllText(
                    logPath,
                    $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n\r\n{ex}");
            }
            catch
            {
                // Ignore logging errors.
            }

            MessageBox.Show(
                ex.ToString(),
                "خطأ عند تشغيل AlSaqarAccounting",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
'@

Set-Content -Path $ProgramFile -Value $ProgramContent -Encoding UTF8
Write-Host 'Program.cs تم تحديثه لإظهار وتسجيل أخطاء بدء التشغيل.' -ForegroundColor Green

Write-Host ''
Write-Host '1) dotnet restore' -ForegroundColor Yellow
dotnet restore $SolutionFile
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore فشل.' }

Write-Host ''
Write-Host '2) dotnet build -c Release' -ForegroundColor Yellow
dotnet build $SolutionFile -c Release
if ($LASTEXITCODE -ne 0) { throw 'dotnet build فشل.' }

Write-Host ''
Write-Host '3) dotnet publish win-x64 self-contained' -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish فشل.' }

$ExePath = Join-Path $PublishDir 'AlSaqarAccounting.exe'
if (-not (Test-Path $ExePath)) {
    throw "ملف التشغيل لم يتم إنشاؤه: $ExePath"
}

Write-Host ''
Write-Host '4) تشغيل البرنامج' -ForegroundColor Yellow
Write-Host "EXE: $ExePath" -ForegroundColor DarkGray
Write-Host ''

# Start and wait briefly for startup. This allows startup-error.log to be created if Main fails.
$Process = Start-Process -FilePath $ExePath -WorkingDirectory $PublishDir -PassThru
Start-Sleep -Seconds 3

if ($Process.HasExited) {
    Write-Host "البرنامج أغلق نفسه. ExitCode = $($Process.ExitCode)" -ForegroundColor Red
    if (Test-Path $StartupLog) {
        Write-Host ''
        Write-Host '=== startup-error.log ===' -ForegroundColor Red
        Get-Content $StartupLog -Raw
    } else {
        Write-Host 'لم يتم إنشاء startup-error.log.' -ForegroundColor Red
    }
} else {
    Write-Host 'البرنامج يعمل الآن.' -ForegroundColor Green
    Write-Host "مجلد التشغيل: $PublishDir"
    Write-Host "ملف السجل عند حدوث خطأ: $StartupLog"
}

Write-Host ''
Write-Host '=== انتهى التنفيذ ===' -ForegroundColor Cyan
