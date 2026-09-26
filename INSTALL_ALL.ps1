#requires -Version 5.1
<#!
.SYNOPSIS
  تثبيت وبناء ونشر AlSaqar Accounting V4 تلقائياً على Windows.

.DESCRIPTION
  - يتحقق من .NET 8 ووجود SQL Server وقاعدة GTSdb2026.
  - ينشئ نسخة احتياطية من appsettings.json.
  - يضبط connection string (لا يرفع كلمة المرور إلى Git).
  - يستعيد الحزم، يبني Release، وينشئ نسخة win-x64 self-contained.
  - لا ينشئ قاعدة بيانات فارغة ولا يشغّل ملفات Export/Read-Only تلقائياً.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\INSTALL_ALL.ps1

.EXAMPLE
  .\INSTALL_ALL.ps1 -SqlServer 'localhost\\SQLEXPRESS' -UseSqlAuth
#>

[CmdletBinding()]
param(
    [string]$SqlServer = '. ',
    [string]$Database = 'GTSdb2026',
    [ValidateSet('Integrated','Sql')]
    [string]$Authentication = 'Integrated',
    [string]$SqlUser = '',
    [SecureString]$SqlPassword,
    [switch]$SkipDatabaseCheck,
    [switch]$SkipPublish,
    [switch]$KeepPasswordInLocalConfig
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# إزالة المسافة الافتراضية غير المقصودة إن استُخدمت القيمة الافتراضية.
$SqlServer = $SqlServer.Trim()
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$Config = Join-Path $Root 'src\AlSaqarAccounting\appsettings.json'
$PublishDir = Join-Path $Root 'publish'
$BackupDir = Join-Path $Root 'backup\installer'

function Write-Step([string]$Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Fail([string]$Message) { throw "فشل التثبيت: $Message" }
function Require-Command([string]$Name, [string]$InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { Fail "$Name غير موجود. $InstallHint" }
}

Write-Host 'AlSaqar Accounting V4 - التثبيت والبناء التلقائي' -ForegroundColor Green
Write-Host "المجلد: $Root"

Write-Step 'التحقق من ملفات المشروع'
if (-not (Test-Path $Solution)) { Fail "لم يتم العثور على $Solution" }
if (-not (Test-Path $Project)) { Fail "لم يتم العثور على $Project" }
if (-not (Test-Path $Config)) { Fail "لم يتم العثور على $Config" }

Write-Step 'التحقق من .NET SDK'
Require-Command 'dotnet' 'ثبت .NET 8 SDK من https://dotnet.microsoft.com/download/dotnet/8.0 ثم أعد تشغيل PowerShell.'
$Sdks = dotnet --list-sdks
if (-not ($Sdks -match '^8\.')) { Fail 'يجب تثبيت .NET 8 SDK.' }
Write-Host ($Sdks -join [Environment]::NewLine)

Write-Step 'إعداد appsettings.json'
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
Copy-Item $Config (Join-Path $BackupDir "appsettings.$Stamp.json") -Force

if ($Authentication -eq 'Integrated') {
    $ConnectionString = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
} else {
    if ([string]::IsNullOrWhiteSpace($SqlUser)) { $SqlUser = Read-Host 'اسم مستخدم SQL Server' }
    if (-not $SqlPassword) { $SqlPassword = Read-Host 'كلمة مرور SQL Server' -AsSecureString }
    $Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
    try { $PlainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($Bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr) }
    $EscapedPassword = $PlainPassword.Replace(';',';;')
    $ConnectionString = "Server=$SqlServer;Database=$Database;User Id=$SqlUser;Password=$EscapedPassword;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
}

$Settings = [ordered]@{
    ConnectionStrings = [ordered]@{ GtsDb2026 = $ConnectionString }
}
$Settings | ConvertTo-Json -Depth 5 | Set-Content -Path $Config -Encoding UTF8
Write-Host "تم تحديث $Config"
if ($Authentication -eq 'Sql' -and -not $KeepPasswordInLocalConfig) {
    Write-Host 'تنبيه: كلمة المرور محفوظة مؤقتاً في appsettings.json المحلي فقط. لا ترفع الملف إلى Git.' -ForegroundColor Yellow
}

if (-not $SkipDatabaseCheck) {
    Write-Step "فحص الاتصال بـ SQL Server / $Database"
    try {
        Add-Type -AssemblyName System.Data
        $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
        $Connection.Open()
        $Command = $Connection.CreateCommand()
        $Command.CommandText = 'SELECT DB_NAME()'
        $CurrentDatabase = [string]$Command.ExecuteScalar()
        $Connection.Close()
        Write-Host "نجح الاتصال بقاعدة البيانات: $CurrentDatabase" -ForegroundColor Green
    } catch {
        Write-Host "تعذر الاتصال: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host 'تحقق من تشغيل SQL Server، اسم الخادم، وجود قاعدة البيانات، وصلاحيات الحساب.' -ForegroundColor Yellow
        Fail 'فشل فحص قاعدة البيانات. استخدم -SkipDatabaseCheck لتجاوز الفحص فقط إذا كنت تعرف ما تفعل.'
    }
}

Write-Step 'استعادة حزم NuGet'
Push-Location $Root
try {
    dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet restore فشل.' }

    Write-Step 'بناء نسخة Release'
    dotnet build $Solution --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet build فشل.' }

    if (-not $SkipPublish) {
        Write-Step 'إنشاء نسخة تشغيل مستقلة win-x64'
        if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
        dotnet publish $Project --configuration Release --runtime win-x64 --self-contained true --output $PublishDir
        if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish فشل.' }
    }
} finally { Pop-Location }

Write-Step 'اكتمل الإعداد'
Write-Host "ملف التشغيل: $(Join-Path $PublishDir 'AlSaqarAccounting.exe')" -ForegroundColor Green
Write-Host 'شغّل النسخة من مجلد publish بعد التأكد من اتصال قاعدة البيانات.'
Write-Host 'مهم: هذا السكربت لا يستخرج قاعدة البيانات الأصلية ولا يخمّن مخططها؛ يجب أن تكون GTSdb2026 موجودة مسبقاً.' -ForegroundColor Yellow

$RunNow = Read-Host 'هل تريد تشغيل النظام الآن؟ (Y/N)'
if ($RunNow -match '^[Yy]$' -and (Test-Path (Join-Path $PublishDir 'AlSaqarAccounting.exe'))) {
    Start-Process (Join-Path $PublishDir 'AlSaqarAccounting.exe')
}
