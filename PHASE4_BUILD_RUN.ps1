#requires -Version 5.1
[CmdletBinding()]
param([switch]$Publish,[switch]$Run)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildScript = Join-Path $Root 'FixAndBuild-AlSaqarAccounting.ps1'
$Output = Join-Path $Root 'src\AlSaqarAccounting\bin\Any CPU\Release\net48'
$PublishDir = Join-Path $Root 'publish'

if (-not (Test-Path $BuildScript)) { throw "لم يتم العثور على سكربت البناء: $BuildScript" }

& $BuildScript -Run:$Run
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    Copy-Item (Join-Path $Output '*') $PublishDir -Recurse -Force
    Write-Host "نسخة NET48: $(Join-Path $PublishDir 'AlSaqarAccounting.exe')" -ForegroundColor Green
}
