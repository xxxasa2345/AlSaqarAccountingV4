#requires -Version 5.1
[CmdletBinding()]
param([switch]$Run)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$OutputDir = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net48'

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
if (-not (Test-Path $Solution)) { throw "الحل غير موجود: $Solution" }
if (-not (Test-Path $Project)) { throw "المشروع غير موجود: $Project" }

Push-Location $Root
try {
    Write-Host '=== AlSaqarAccounting Phase 4 - .NET Framework 4.8 ===' -ForegroundColor Cyan
    & $msbuild $Solution /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Restore فشل.' }

    & $msbuild $Solution /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Build فشل.' }

    $exe = Join-Path $OutputDir 'AlSaqarAccounting.exe'
    if (-not (Test-Path $exe)) { throw "EXE غير موجود: $exe" }

    Write-Host "تم البناء: $exe" -ForegroundColor Green
    if ($Run) { Start-Process -FilePath $exe -WorkingDirectory $OutputDir }
}
finally { Pop-Location }
