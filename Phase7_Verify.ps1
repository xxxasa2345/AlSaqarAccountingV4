#requires -Version 5.1
[CmdletBinding()]
param([string]$Root = "")

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).Path

$csproj = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$sln = Join-Path $Root 'AlSaqarAccounting.sln'
$output = Join-Path $Root 'src\AlSaqarAccounting\bin\Any CPU\Release\net48'
if (-not (Test-Path $csproj)) { throw "Missing project: $csproj" }
if (-not (Test-Path $sln)) { throw "Missing solution: $sln" }

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

Push-Location $Root
try {
    Write-Host '=== AlSaqarAccounting local verification: .NET Framework 4.8 ===' -ForegroundColor Cyan
    & $msbuild $sln /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Restore failed.' }

    & $msbuild $sln /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Build failed.' }
}
finally { Pop-Location }

$exe = Join-Path $output 'AlSaqarAccounting.exe'
$cfg = Join-Path $output 'appsettings.json'
if (-not (Test-Path $exe)) { throw "Release EXE not found: $exe" }
if (-not (Test-Path $cfg)) { throw "Release appsettings.json not found: $cfg" }

Write-Host 'Release EXE         : OK' -ForegroundColor Green
Write-Host 'Release appsettings : OK' -ForegroundColor Green
Write-Host 'TARGET               : net48' -ForegroundColor Green
Write-Host 'PHASE 7 VERIFY OK' -ForegroundColor Green
