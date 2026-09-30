#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$SqlServer = '.',
    [string]$Database = 'GTSdb2026',
    [ValidateSet('Integrated','Sql')]
    [string]$Authentication = 'Integrated',
    [string]$SqlUser = '',
    [SecureString]$SqlPassword,
    [switch]$SkipDatabaseCheck,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$Config = Join-Path $Root 'src\AlSaqarAccounting\appsettings.json'
$BuildDir = Join-Path $Root 'src\AlSaqarAccounting\bin\Any CPU\Release\net48'
$PublishDir = Join-Path $Root 'publish'
$BackupDir = Join-Path $Root 'backup\installer'

function Fail([string]$Message) { throw "فشل التثبيت: $Message" }
function Require([string]$Path,[string]$Message) { if (-not (Test-Path $Path)) { Fail $Message } }

function Find-MSBuild {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null |
            Select-Object -First 1
        if ($found) { return $found }
    }

    throw 'MSBuild غير موجود. ثبّت Visual Studio 2022 مع workload Desktop development with .NET ثم شغّل Developer PowerShell.'
}
$msbuild = Find-MSBuild

Require $Solution "الحل غير موجود: $Solution"
Require $Project "المشروع غير موجود: $Project"
Require $Config "appsettings.json غير موجود: $Config"

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
Copy-Item $Config (Join-Path $BackupDir ("appsettings." + (Get-Date -Format 'yyyyMMdd_HHmmss') + ".json")) -Force

if ($Authentication -eq 'Integrated') {
    $ConnectionString = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
}
else {
    if ([string]::IsNullOrWhiteSpace($SqlUser)) { $SqlUser = Read-Host 'اسم مستخدم SQL Server' }
    if (-not $SqlPassword) { $SqlPassword = Read-Host 'كلمة مرور SQL Server' -AsSecureString }
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
    try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    $ConnectionString = "Server=$SqlServer;Database=$Database;User Id=$SqlUser;Password=$plain;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
}

[ordered]@{ ConnectionStrings = [ordered]@{ GtsDb2026 = $ConnectionString } } |
    ConvertTo-Json -Depth 5 | Set-Content -Path $Config -Encoding UTF8

if (-not $SkipDatabaseCheck) {
    Add-Type -AssemblyName System.Data
    $cn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $cn.Open()
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = 'SELECT DB_NAME()'
        Write-Host "Database connection OK: $([string]$cmd.ExecuteScalar())" -ForegroundColor Green
    }
    catch { Fail "تعذر الاتصال بقاعدة البيانات: $($_.Exception.Message)" }
    finally { $cn.Dispose() }
}

Push-Location $Root
try {
    & $msbuild $Solution /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m
    if ($LASTEXITCODE -ne 0) { Fail 'MSBuild Restore فشل.' }

    & $msbuild $Solution /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { Fail 'MSBuild Build فشل.' }

    if (-not $SkipPublish) {
        if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
        Copy-Item (Join-Path $BuildDir '*') $PublishDir -Recurse -Force
    }
}
finally { Pop-Location }

Write-Host "تم البناء بنجاح: $(Join-Path $BuildDir 'AlSaqarAccounting.exe')" -ForegroundColor Green
if (-not $SkipPublish) { Write-Host "نسخة التشغيل: $(Join-Path $PublishDir 'AlSaqarAccounting.exe')" -ForegroundColor Green }
