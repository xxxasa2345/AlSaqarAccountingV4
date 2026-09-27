[CmdletBinding()]
param([string]$Root = "")

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$resolved = Resolve-Path -LiteralPath $Root -ErrorAction Stop
$Root = $resolved.Path

$csproj = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
if (-not (Test-Path -LiteralPath $csproj -PathType Leaf)) {
    throw "Missing project: $csproj"
}

Write-Host '=== Phase 7 local verification ==='
Write-Host "Project: $csproj"

& dotnet restore $csproj -p:EnableWindowsTargeting=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& dotnet build $csproj -c Release --no-restore -p:EnableWindowsTargeting=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

$exe = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net8.0-windows\AlSaqarAccounting.exe'
$cfg = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net8.0-windows\appsettings.json'

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Release EXE not found: $exe" }
if (-not (Test-Path -LiteralPath $cfg -PathType Leaf)) { throw "Release appsettings.json not found: $cfg" }

Write-Host 'Release EXE        : OK'
Write-Host 'Release appsettings: OK'
Write-Host 'PHASE 7 VERIFY OK'
