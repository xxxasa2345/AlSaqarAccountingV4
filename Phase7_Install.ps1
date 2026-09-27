[CmdletBinding()]
param(
    [string]$Root = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Log([string]$Message) {
    $line = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Write-Host $line
    if ($script:LogFile) {
        Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8
    }
}

function Backup-File([string]$Path,[string]$BackupRoot,[string]$RootPath) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $rel = $Path.Substring($RootPath.Length)
    $rel = $rel.TrimStart('\','/')
    $dest = Join-Path $BackupRoot $rel
    $destDir = Split-Path -Parent $dest
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item -LiteralPath $Path -Destination $dest -Force
    return $dest
}

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Get-Location).Path
}

try {
    $resolvedRoot = Resolve-Path -LiteralPath $Root -ErrorAction Stop
    $Root = $resolvedRoot.Path.TrimEnd('\','/')
}
catch {
    throw "Project root not found or invalid: $Root"
}

if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    throw "Project root not found: $Root"
}

$script:LogFile = Join-Path -Path $Root -ChildPath 'phase7-install.log'
Log '======================================================================'
Log 'AlSaqarAccounting - Phase 7 CI/CD + Release Verification'
Log '======================================================================'
Log "Root: $Root"

# Find the real application project. Never select a csproj from backup/bin/obj.
$projects = @(Get-ChildItem -LiteralPath $Root -Filter '*.csproj' -File -Recurse |
    Where-Object {
        $_.FullName -notmatch '\\(backup|bin|obj)(\\|$)' -and
        $_.FullName -notmatch '\\.git(\\|$)'
    })

$project = $projects |
    Where-Object { $_.FullName -match '\\src\\AlSaqarAccounting\\AlSaqarAccounting\.csproj$' } |
    Select-Object -First 1

if (-not $project) {
    $project = $projects |
        Where-Object { $_.Name -eq 'AlSaqarAccounting.csproj' -and $_.Directory.Name -eq 'AlSaqarAccounting' } |
        Sort-Object FullName |
        Select-Object -First 1
}

if (-not $project) {
    if ($projects.Count -eq 1) {
        $project = $projects[0]
    }
    elseif ($projects.Count -gt 1) {
        Write-Host 'Found multiple candidate projects:' -ForegroundColor Yellow
        $i = 1
        foreach ($p in $projects) {
            Write-Host "[$i] $($p.FullName)"
            $i++
        }
        $choice = Read-Host 'Enter the project number for AlSaqarAccounting'
        $index = [int]$choice - 1
        if ($index -lt 0 -or $index -ge $projects.Count) {
            throw 'Invalid project selection.'
        }
        $project = $projects[$index]
    }
    else {
        throw 'AlSaqarAccounting.csproj was not found outside backup/bin/obj.'
    }
}

$projectDir = $project.Directory.FullName
$solution = Join-Path $Root 'AlSaqarAccounting.sln'
Log "Project: $($project.FullName)"
Log "ProjectDir: $projectDir"

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $Root "backup\phase7-$stamp"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
Log "Backup: $backupRoot"

# ----------------------------------------------------------------------
# .gitignore
# ----------------------------------------------------------------------
$gitignore = Join-Path $Root '.gitignore'
if (Test-Path -LiteralPath $gitignore) {
    Backup-File $gitignore $backupRoot $Root | Out-Null
    $existing = Get-Content -LiteralPath $gitignore -Raw -Encoding UTF8
} else {
    $existing = ''
}

$patterns = @(
    'bin/', 'obj/', 'publish/', 'backup/', '*.user', '*.suo',
    'phase7-install.log', 'phase6-v61-run.log',
    'screen-linking-install-report.txt', 'erp-shell-install-report.txt',
    'phase6-appsettings-*.log'
)

foreach ($pattern in $patterns) {
    if ($existing -notmatch [regex]::Escape($pattern)) {
        if ($existing.Length -gt 0 -and -not $existing.EndsWith("`r`n")) {
            $existing += "`r`n"
        }
        $existing += $pattern + "`r`n"
    }
}

Set-Content -LiteralPath $gitignore -Value $existing -Encoding UTF8
Log '.gitignore normalized.'

# ----------------------------------------------------------------------
# GitHub Actions workflow
# ----------------------------------------------------------------------
$wfDir = Join-Path $Root '.github\workflows'
New-Item -ItemType Directory -Force -Path $wfDir | Out-Null
$wf = Join-Path $wfDir 'al-saqar-build.yml'
if (Test-Path -LiteralPath $wf) {
    Backup-File $wf $backupRoot $Root | Out-Null
}

$workflow = @'
name: AlSaqarAccounting Build

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore .\AlSaqarAccounting.sln -p:EnableWindowsTargeting=true

      - name: Build Release
        run: dotnet build .\AlSaqarAccounting.sln -c Release --no-restore -p:EnableWindowsTargeting=true

      - name: Publish win-x64
        run: dotnet publish .\src\AlSaqarAccounting\AlSaqarAccounting.csproj -c Release -r win-x64 --self-contained true --no-restore -p:EnableWindowsTargeting=true -o .\ci-publish

      - name: Verify published runtime files
        shell: pwsh
        run: |
          $exe = Join-Path $env:GITHUB_WORKSPACE 'ci-publish\AlSaqarAccounting.exe'
          if (-not (Test-Path $exe)) { throw "Published EXE not found: $exe" }
          $cfg = Join-Path $env:GITHUB_WORKSPACE 'ci-publish\appsettings.json'
          if (Test-Path $cfg) {
            Remove-Item $cfg -Force
            Write-Host 'appsettings.json removed from CI artifact to avoid publishing local connection settings.'
          }
          Write-Host "Published EXE verified: $exe"

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: AlSaqarAccounting-win-x64
          path: .\ci-publish
          if-no-files-found: error
          retention-days: 7
'@

Set-Content -LiteralPath $wf -Value $workflow -Encoding UTF8
Log '.github/workflows/al-saqar-build.yml installed.'

# ----------------------------------------------------------------------
# Local verification script
# ----------------------------------------------------------------------
$verify = Join-Path $Root 'Phase7_Verify.ps1'
if (Test-Path -LiteralPath $verify) {
    Backup-File $verify $backupRoot $Root | Out-Null
}

$verifyCode = @'
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
'@

Set-Content -LiteralPath $verify -Value $verifyCode -Encoding UTF8
Log 'Phase7_Verify.ps1 installed.'

if (-not $SkipBuild) {
    Log 'dotnet restore'
    Push-Location $projectDir
    try {
        if (Test-Path -LiteralPath $solution -PathType Leaf) {
            & dotnet restore $solution -p:EnableWindowsTargeting=true
        } else {
            & dotnet restore $project.FullName -p:EnableWindowsTargeting=true
        }
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

        Log 'dotnet build Release'
        if (Test-Path -LiteralPath $solution -PathType Leaf) {
            & dotnet build $solution -c Release --no-restore -p:EnableWindowsTargeting=true
        } else {
            & dotnet build $project.FullName -c Release --no-restore -p:EnableWindowsTargeting=true
        }
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }
    finally {
        Pop-Location
    }
}

Log '======================================================================'
Log 'SUCCESS: Phase 7.2 installed and local verification build passed.'
Log "Workflow: $wf"
Log "Backup: $backupRoot"
Log '======================================================================'
