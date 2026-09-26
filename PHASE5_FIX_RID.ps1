#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Root = "",
    [switch]$Run
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Get-Location).Path
}

$Root = [IO.Path]::GetFullPath($Root)
$Project = Join-Path $Root "src\AlSaqarAccounting\AlSaqarAccounting.csproj"
$Solution = Join-Path $Root "AlSaqarAccounting.sln"
$PublishDir = Join-Path $Root "publish"
$BackupDir = Join-Path $Root ".phase5-rid-backup"

Write-Host "=== AlSaqarAccounting Phase 5 - RID Publish Fix ===" -ForegroundColor Cyan
Write-Host "Root: $Root"

if (-not (Test-Path -LiteralPath $Project)) {
    throw "لم يتم العثور على المشروع: $Project"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet غير موجود في PATH."
}

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

# Backup csproj before any optional change.
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupProject = Join-Path $BackupDir "AlSaqarAccounting.csproj.$stamp.bak"
Copy-Item -LiteralPath $Project -Destination $backupProject -Force
Write-Host "Backup: $backupProject" -ForegroundColor DarkGray

Push-Location $Root
try {
    Write-Host ""
    Write-Host "1) Restore مع Runtime Identifier win-x64" -ForegroundColor Yellow
    dotnet restore $Project -r win-x64
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore -r win-x64 فشل."
    }

    Write-Host ""
    Write-Host "2) Build Release" -ForegroundColor Yellow
    dotnet build $Solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build فشل."
    }

    if (Test-Path -LiteralPath $PublishDir) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }

    Write-Host ""
    Write-Host "3) Publish win-x64 self-contained" -ForegroundColor Yellow
    dotnet publish $Project -c Release -r win-x64 --self-contained true -o $PublishDir --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish فشل."
    }

    $Exe = Join-Path $PublishDir "AlSaqarAccounting.exe"
    if (-not (Test-Path -LiteralPath $Exe)) {
        throw "تمت عملية publish لكن ملف التشغيل غير موجود: $Exe"
    }

    Write-Host ""
    Write-Host "تم النشر بنجاح." -ForegroundColor Green
    Write-Host "EXE: $Exe" -ForegroundColor Green

    if ($Run) {
        Write-Host ""
        Write-Host "4) تشغيل البرنامج" -ForegroundColor Yellow
        & $Exe
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "انتهى السكربت بنجاح." -ForegroundColor Green
