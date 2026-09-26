[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$PublishDir = Join-Path $Root 'publish'
$BackupDir = Join-Path $Root ('.phase5-backup\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$Router = Join-Path $Root 'src\AlSaqarAccounting\UI\ScreenRouter.cs'
$Operational = Join-Path $Root 'src\AlSaqarAccounting\UI\OperationalDataScreen.cs'
$Definition = Join-Path $Root 'src\AlSaqarAccounting\UI\OperationalScreenDefinition.cs'

Write-Host '=== AlSaqarAccounting Phase 5 ===' -ForegroundColor Cyan
Write-Host "Root: $Root"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK غير موجود في PATH.'
}
if (-not (Test-Path $Project)) { throw "المشروع غير موجود: $Project" }
if (-not (Test-Path $Solution)) { throw "الحل غير موجود: $Solution" }
if (-not (Test-Path $Router)) { throw "ScreenRouter.cs غير موجود." }
if (-not (Test-Path $Operational)) { throw "OperationalDataScreen.cs غير موجود." }
if (-not (Test-Path $Definition)) { throw "OperationalScreenDefinition.cs غير موجود." }

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
Copy-Item $Router (Join-Path $BackupDir 'ScreenRouter.cs')
Copy-Item $Operational (Join-Path $BackupDir 'OperationalDataScreen.cs')
Copy-Item $Definition (Join-Path $BackupDir 'OperationalScreenDefinition.cs')

Write-Host 'Backup:' $BackupDir -ForegroundColor DarkGray

Push-Location $Root
try {
    dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore فشل.' }

    dotnet build $Solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build فشل.' }

    if ($Publish) {
        if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
        dotnet publish $Project -c Release -r win-x64 --self-contained true --no-restore -o $PublishDir
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish فشل.' }
    }
}
finally {
    Pop-Location
}

Write-Host 'Phase 5 build completed successfully.' -ForegroundColor Green

if ($Run) {
    if ($Publish) {
        $exe = Join-Path $PublishDir 'AlSaqarAccounting.exe'
    }
    else {
        $exe = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net8.0-windows\AlSaqarAccounting.exe'
    }

    if (-not (Test-Path $exe)) { throw "ملف التشغيل غير موجود: $exe" }
    Write-Host "Running: $exe" -ForegroundColor Yellow
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent)
}
