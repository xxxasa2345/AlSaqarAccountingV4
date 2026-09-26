#requires -Version 5.1
[CmdletBinding()]
param([switch]$Publish,[switch]$Run)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$FixScript = Join-Path $Root 'FIX_PHASE4_AND_BUILD.ps1'

if (-not (Test-Path $FixScript)) {
    throw "لم يتم العثور على سكربت الإصلاح: $FixScript"
}

& $FixScript -Publish:$Publish -Run:$Run
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
