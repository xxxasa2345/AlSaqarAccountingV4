#requires -Version 5.1
[CmdletBinding()]
param([switch]$Run)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "src\AlSaqarAccounting\AlSaqarAccounting.csproj"
$GlobalUsings = Join-Path $Root "src\AlSaqarAccounting\GlobalUsings.cs"
$InitCompat = Join-Path $Root "src\AlSaqarAccounting\Compat\IsExternalInit.cs"
$Solution = Join-Path $Root "AlSaqarAccounting.sln"
$Log = Join-Path $Root "local-build-log.txt"

function Find-MSBuild {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null |
            Select-Object -First 1
        if ($found) { return $found }
    }

    throw "MSBuild غير موجود. افتح Developer PowerShell 2022 أو ثبّت workload: Desktop development with .NET."
}

function Ensure-File($Path, $Content) {
    if (-not (Test-Path -LiteralPath $Path)) {
        $dir = Split-Path -Parent $Path
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
        Write-Host "RESTORED $Path" -ForegroundColor Yellow
    } else {
        Write-Host "OK       $Path" -ForegroundColor Green
    }
}

Write-Host "=== AlSaqarAccounting Local Build Guard ===" -ForegroundColor Cyan
if (-not (Test-Path -LiteralPath $Project)) { throw "المشروع غير موجود: $Project" }
if (-not (Test-Path -LiteralPath $Solution)) { throw "الحل غير موجود: $Solution" }

Ensure-File $GlobalUsings @"
global using System;
global using System.Collections.Generic;
global using System.Data;
global using System.Drawing;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;
"@

Ensure-File $InitCompat @"
// Compatibility type required for C# record/init features on .NET Framework 4.8.
namespace System.Runtime.CompilerServices;
internal static class IsExternalInit
{
}
"@

$msbuild = Find-MSBuild
Write-Host "MSBuild: $msbuild" -ForegroundColor DarkGray

Push-Location $Root
try {
    Remove-Item (Join-Path $Root "src\AlSaqarAccounting\bin") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Root "src\AlSaqarAccounting\obj") -Recurse -Force -ErrorAction SilentlyContinue

    & $msbuild $Solution /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m 2>&1 |
        Tee-Object -FilePath $Log
    if ($LASTEXITCODE -ne 0) { throw "Restore فشل. راجع $Log" }

    & $msbuild $Solution /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal 2>&1 |
        Tee-Object -FilePath $Log -Append
    if ($LASTEXITCODE -ne 0) { throw "Build فشل. راجع $Log" }

    $exe = Join-Path $Root "src\AlSaqarAccounting\bin\Release\net48\AlSaqarAccounting.exe"
    if (-not (Test-Path -LiteralPath $exe)) { throw "تم البناء لكن EXE غير موجود: $exe" }

    Write-Host ""
    Write-Host "BUILD SUCCESS" -ForegroundColor Green
    Write-Host "EXE: $exe" -ForegroundColor Green

    if ($Run) {
        Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
    }
}
finally {
    Pop-Location
}
