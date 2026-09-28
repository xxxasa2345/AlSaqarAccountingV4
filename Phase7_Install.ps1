#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Root = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).Path

$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$Project = Join-Path $Root 'src\AlSaqarAccounting\AlSaqarAccounting.csproj'
$BuildDir = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net48'
$Workflow = Join-Path $Root '.github\workflows\AlSaqarAccounting-NET48.yml'
$LogFile = Join-Path $Root 'phase7-install.log'

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

function Log([string]$Message) {
    $line = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
}

Log '=== AlSaqarAccounting .NET Framework 4.8 verification/install ==='
Log "Root: $Root"

if (-not (Test-Path $Solution)) { throw "الحل غير موجود: $Solution" }
if (-not (Test-Path $Project)) { throw "المشروع غير موجود: $Project" }

if (-not (Test-Path (Join-Path $Root '.gitignore'))) {
    @'
bin/
obj/
publish/
backup/
*.user
*.suo
phase7-install.log
'@ | Set-Content -Path (Join-Path $Root '.gitignore') -Encoding UTF8
    Log '.gitignore created.'
}

if (-not $SkipBuild) {
    Push-Location $Root
    try {
        Log 'MSBuild Restore'
        & $msbuild $Solution /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m 2>&1 |
            ForEach-Object { Write-Host $_; Add-Content -LiteralPath $LogFile -Value $_ -Encoding UTF8 }
        if ($LASTEXITCODE -ne 0) { throw 'MSBuild Restore فشل.' }

        Log 'MSBuild Build Release'
        & $msbuild $Solution /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal 2>&1 |
            ForEach-Object { Write-Host $_; Add-Content -LiteralPath $LogFile -Value $_ -Encoding UTF8 }
        if ($LASTEXITCODE -ne 0) { throw 'MSBuild Build فشل.' }
    }
    finally { Pop-Location }
}

$exe = Join-Path $BuildDir 'AlSaqarAccounting.exe'
$cfg = Join-Path $BuildDir 'appsettings.json'
if (-not (Test-Path $exe)) { throw "EXE غير موجود: $exe" }
if (-not (Test-Path $cfg)) { throw "appsettings.json غير موجود: $cfg" }

Log "EXE verified: $exe"
Log "Config verified: $cfg"
if (Test-Path $Workflow) { Log "GitHub workflow verified: $Workflow" }

Write-Host '============================================' -ForegroundColor Green
Write-Host 'NET48 verification completed successfully.' -ForegroundColor Green
Write-Host "EXE: $exe" -ForegroundColor Green
Write-Host "Log: $LogFile" -ForegroundColor Green
Write-Host '============================================' -ForegroundColor Green
