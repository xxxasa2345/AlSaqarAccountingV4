#requires -Version 5.1
[CmdletBinding()]
param([switch]$Run)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionFile = Join-Path $ProjectRoot 'AlSaqarAccounting.sln'
$ProjectFile = Join-Path $ProjectRoot 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$OutputDir = Join-Path $ProjectRoot 'src\AlSaqarAccounting\bin\Release\net48'

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
if (-not (Test-Path -LiteralPath $SolutionFile)) { throw "الحل غير موجود: $SolutionFile" }
if (-not (Test-Path -LiteralPath $ProjectFile)) { throw "المشروع غير موجود: $ProjectFile" }

Push-Location $ProjectRoot
try {
    Write-Host '=== AlSaqarAccounting .NET Framework 4.8 Build ===' -ForegroundColor Cyan
    & $msbuild $SolutionFile /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Restore فشل.' }

    & $msbuild $SolutionFile /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Build فشل.' }

    $exe = Join-Path $OutputDir 'AlSaqarAccounting.exe'
    $cfg = Join-Path $OutputDir 'appsettings.json'
    if (-not (Test-Path -LiteralPath $exe)) { throw "EXE غير موجود: $exe" }
    if (-not (Test-Path -LiteralPath $cfg)) { throw "appsettings.json غير موجود: $cfg" }

    Write-Host "EXE: $exe" -ForegroundColor Green
    if ($Run) { Start-Process -FilePath $exe -WorkingDirectory $OutputDir }
}
finally { Pop-Location }
