#requires -Version 5.1
[CmdletBinding()]
param([string]$Root = "",[switch]$Run)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = (Resolve-Path $Root).Path
$Solution = Join-Path $Root 'AlSaqarAccounting.sln'
$BuildDir = Join-Path $Root 'src\AlSaqarAccounting\bin\Release\net48'
$PublishDir = Join-Path $Root 'publish'
$LogFile = Join-Path $Root 'PHASE5_FIX_RID.log'

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
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $Message"
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
}

try {
    Set-Location $Root
    Remove-Item $LogFile -Force -ErrorAction SilentlyContinue
    Log '=== AlSaqarAccounting .NET Framework 4.8 build ==='

    & $msbuild $Solution /t:Restore /p:Configuration=Release /p:Platform="Any CPU" /m 2>&1 |
        ForEach-Object { Write-Host $_; Add-Content $LogFile $_ -Encoding UTF8 }
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Restore فشل.' }

    & $msbuild $Solution /t:Build /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal 2>&1 |
        ForEach-Object { Write-Host $_; Add-Content $LogFile $_ -Encoding UTF8 }
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild Build فشل.' }

    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    Copy-Item (Join-Path $BuildDir '*') $PublishDir -Recurse -Force

    $exe = Join-Path $PublishDir 'AlSaqarAccounting.exe'
    if (-not (Test-Path $exe)) { throw "EXE غير موجود: $exe" }
    Log "تم إنشاء: $exe"
    if ($Run) { Start-Process -FilePath $exe -WorkingDirectory $PublishDir; Log 'تم تشغيل البرنامج.' }
}
catch {
    Log ("ERROR: " + $_.Exception.Message)
    throw
}
finally {
    Write-Host "السجل: $LogFile" -ForegroundColor Green
}
