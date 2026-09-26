#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$ProgramFile = Join-Path $Root 'src\AlSaqarAccounting\Program.cs'
$BackupDir = Join-Path $Root 'backup\phase4-fix'

Write-Host '=== AlSaqarAccounting Phase 4 - Fix Program + Build ===' -ForegroundColor Cyan
Write-Host "Root: $Root"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet غير موجود في PATH.'
}
if (-not (Test-Path $Project)) { throw "لم يتم العثور على المشروع: $Project" }
if (-not (Test-Path $Solution)) { throw "لم يتم العثور على الحل: $Solution" }
if (-not (Test-Path $ProgramFile)) { throw "لم يتم العثور على Program.cs: $ProgramFile" }

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
Copy-Item $ProgramFile (Join-Path $BackupDir "Program.cs.$stamp.bak") -Force

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
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            "appsettings.json",
            optional: false,
            reloadOnChange: true);

        builder.Services.AddSingleton<SqlConnectionFactory>();
        builder.Services.AddSingleton<StoredProcedureExecutor>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<SchemaService>();
        builder.Services.AddSingleton<SecurityService>();

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var schema = scope.ServiceProvider.GetRequiredService<SchemaService>();
        var sp = scope.ServiceProvider.GetRequiredService<StoredProcedureExecutor>();
        var security = scope.ServiceProvider.GetRequiredService<SecurityService>();

        Application.Run(new LoginForm(auth, schema, sp, security));
    }
}
'@

Set-Content -LiteralPath $ProgramFile -Value $ProgramContent -Encoding UTF8
Write-Host 'تم إصلاح Program.cs: تسجيل SecurityService وتمريره إلى LoginForm.' -ForegroundColor Green

Push-Location $Root
try {
    Write-Host ''
    Write-Host '1) dotnet restore' -ForegroundColor Yellow
    dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore فشل.' }

    Write-Host ''
    Write-Host '2) dotnet build -c Release' -ForegroundColor Yellow
    dotnet build $Solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build فشل.' }

    if ($Publish) {
        $PublishDir = Join-Path $Root 'publish'
        if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

        Write-Host ''
        Write-Host '3) dotnet publish win-x64 self-contained' -ForegroundColor Yellow
        dotnet publish $Project -c Release -r win-x64 --self-contained true -o $PublishDir --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish فشل.' }

        $Exe = Join-Path $PublishDir 'AlSaqarAccounting.exe'
        Write-Host "EXE: $Exe" -ForegroundColor Green

        if ($Run) {
            Write-Host ''
            Write-Host '4) تشغيل البرنامج' -ForegroundColor Yellow
            & $Exe
        }
    }
    elseif ($Run) {
        $Exe = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net8.0-windows\AlSaqarAccounting.exe'
        if (-not (Test-Path $Exe)) { throw "EXE غير موجود: $Exe" }
        & $Exe
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host 'تم الإصلاح والبناء بنجاح.' -ForegroundColor Green
