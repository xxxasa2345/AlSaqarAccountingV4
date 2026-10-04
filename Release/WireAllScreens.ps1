$ErrorActionPreference = "Stop"

$Root = "C:\Users\hp\Desktop\AlSaqarAccountingV4"
Set-Location $Root

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " AlSaqarAccounting - فحص وربط جميع الشاشات" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$SourceRoot = Join-Path $Root "src\AlSaqarAccounting"

if (-not (Test-Path $SourceRoot)) {
    throw "مجلد المشروع غير موجود: $SourceRoot"
}

# البحث عن الملفات الحقيقية بدل افتراض مسارها
$routerFile = Get-ChildItem $SourceRoot -Recurse -Filter "ScreenRouter.cs" -File |
    Select-Object -First 1

$mainFile = Get-ChildItem $SourceRoot -Recurse -Filter "MainForm.cs" -File |
    Select-Object -First 1

if (-not $routerFile) {
    throw "لم يتم العثور على ScreenRouter.cs"
}

Write-Host "[OK] ScreenRouter:" -ForegroundColor Green
Write-Host "     $($routerFile.FullName)"

if ($mainFile) {
    Write-Host "[OK] MainForm:" -ForegroundColor Green
    Write-Host "     $($mainFile.FullName)"
}
else {
    Write-Host "[INFO] لا يوجد MainForm.cs — سنعتمد على بنية المشروع الفعلية." -ForegroundColor Yellow
}

$routerText = Get-Content $routerFile.FullName -Raw

if ($mainFile) {
    $mainText = Get-Content $mainFile.FullName -Raw
}
else {
    $mainText = ""
}

# ------------------------------------------------------------
# Router
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== ROUTER ===" -ForegroundColor Yellow

$routerMatches = [regex]::Matches(
    $routerText,
    'case\s+"([^"]+)"\s*:'
)

$routerScreens = @(
    $routerMatches |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique
)

if ($routerScreens.Count -eq 0) {
    Write-Host "لم يتم العثور على case نصية." -ForegroundColor Yellow
}
else {
    foreach ($screen in $routerScreens) {
        Write-Host "  $screen" -ForegroundColor Green
    }
}

# ------------------------------------------------------------
# Real catalog
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== REAL SCREEN CATALOG ===" -ForegroundColor Yellow

$catalogFile = Get-ChildItem $SourceRoot -Recurse -Filter "RealScreenCatalog.cs" -File |
    Select-Object -First 1

if (-not $catalogFile) {
    throw "لم يتم العثور على RealScreenCatalog.cs"
}

$catalogText = Get-Content $catalogFile.FullName -Raw
$catalogScreens = @(
    [regex]::Matches($catalogText, '["([^"]+)"]s*=s*(cs,s*s,s*a)') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique
)

foreach ($screen in $catalogScreens) {
    Write-Host "  $screen" -ForegroundColor Green
}

# ------------------------------------------------------------
# Forms
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== FORMS ===" -ForegroundColor Yellow

$formFiles = Get-ChildItem `
    $SourceRoot `
    -Recurse `
    -Filter "*Form.cs" `
    -File `
    -ErrorAction SilentlyContinue

$forms = @(
    $formFiles |
    ForEach-Object {
        [PSCustomObject]@{
            Name = $_.BaseName
            Path = $_.FullName
        }
    } |
    Sort-Object Name
)

foreach ($form in $forms) {
    Write-Host ("  {0}" -f $form.Name) -ForegroundColor Gray
}

# ------------------------------------------------------------
# Dynamic screen
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== DYNAMIC SCREEN ===" -ForegroundColor Yellow

$dynamic = $formFiles |
    Where-Object { $_.BaseName -eq "DynamicErpScreenForm" }

if ($dynamic) {
    Write-Host "[FOUND] DynamicErpScreenForm" -ForegroundColor Yellow
    Write-Host $dynamic.FullName
}
else {
    Write-Host "[OK] DynamicErpScreenForm غير موجود" -ForegroundColor Green
}

# ------------------------------------------------------------
# جميع ملفات C#
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== C# FILES ===" -ForegroundColor Yellow

$csFiles = Get-ChildItem `
    $SourceRoot `
    -Recurse `
    -Filter "*.cs" `
    -File `
    -ErrorAction SilentlyContinue

Write-Host "إجمالي ملفات C#: $($csFiles.Count)" -ForegroundColor Cyan

# ------------------------------------------------------------
# البحث عن مشاكل الربط الشائعة
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== SEARCH: ScreenRouter / DynamicErpScreenForm ===" -ForegroundColor Yellow

$routerRefs = Select-String `
    -Path $csFiles.FullName `
    -Pattern "ScreenRouter|DynamicErpScreenForm" `
    -SimpleMatch `
    -ErrorAction SilentlyContinue

foreach ($ref in $routerRefs) {
    Write-Host ("{0}:{1}" -f $ref.Path,$ref.LineNumber) -ForegroundColor Gray
}

# ------------------------------------------------------------
# التقرير
# ------------------------------------------------------------

$reportDir = Join-Path $Root "Release"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

$report = Join-Path $reportDir "Screen-Wiring-Report.txt"

$lines = New-Object System.Collections.Generic.List[string]

$lines.Add("AlSaqarAccounting - Screen Wiring Report")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("")

$lines.Add("=== ROUTER FILE ===")
$lines.Add($routerFile.FullName)
$lines.Add("")


$lines.Add("=== MAIN FORM ===")
if ($mainFile) {
    $lines.Add($mainFile.FullName)
}
else {
    $lines.Add("NOT FOUND")
}
$lines.Add("")

$lines.Add("=== ROUTER SCREENS ===")
foreach ($screen in $routerScreens) {
    $lines.Add($screen)
}
$lines.Add("")

$lines.Add("=== REAL SCREEN CATALOG ===")
foreach ($screen in $catalogScreens) { $lines.Add($screen) }
$lines.Add("")

$lines.Add("=== FORMS ===")
foreach ($form in $forms) {
    $lines.Add("$($form.Name) | $($form.Path)")
}
$lines.Add("")

$lines.Add("=== ALL C# FILES ===")
foreach ($cs in $csFiles) {
    $lines.Add($cs.FullName)
}

$lines | Set-Content $report -Encoding UTF8

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " انتهى الفحص" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "Router screens : $($routerScreens.Count)"
Write-Host "Entity mappings: $($mapEntries.Count)"
Write-Host "Forms          : $($forms.Count)"
Write-Host "C# files       : $($csFiles.Count)"

Write-Host ""
Write-Host "التقرير:"
Write-Host $report -ForegroundColor White

Write-Host ""
Read-Host "اضغط Enter للانتهاء"