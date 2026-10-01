$ErrorActionPreference = "Stop"

$Root = "C:\Users\hp\Desktop\AlSaqarAccountingV4"
$Src  = Join-Path $Root "src\AlSaqarAccounting"
$UI   = Join-Path $Src "UI"
$Forms = Join-Path $Src "Forms"
$Report = Join-Path $Root "Release\Screen-Wiring-Final-Report.txt"

Set-Location $Root

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " AlSaqarAccounting - ربط وفحص جميع الشاشات" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# 1. التأكد من الملفات الأساسية
# ------------------------------------------------------------

$router = Join-Path $UI "ScreenRouter.cs"
$map    = Join-Path $UI "ScreenEntityMap.cs"

foreach ($file in @($router, $map)) {
    if (!(Test-Path $file)) {
        throw "الملف غير موجود: $file"
    }
}

Write-Host "[OK] ScreenRouter.cs" -ForegroundColor Green
Write-Host "[OK] ScreenEntityMap.cs" -ForegroundColor Green

# ------------------------------------------------------------
# 2. نسخ احتياطية
# ------------------------------------------------------------

$backupDir = Join-Path $Root "Release\WireBackup"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

Copy-Item $router "$backupDir\ScreenRouter.cs.bak" -Force
Copy-Item $map    "$backupDir\ScreenEntityMap.cs.bak" -Force

Write-Host "[OK] تم إنشاء النسخ الاحتياطية" -ForegroundColor Green

# ------------------------------------------------------------
# 3. قراءة ScreenEntityMap
# ------------------------------------------------------------

$mapText = Get-Content $map -Raw

# إزالة BOM فقط إذا كان موجوداً
$mapText = $mapText.TrimStart([char]0xFEFF)

# ------------------------------------------------------------
# 4. الخرائط الأساسية المعروفة من التقرير
# ------------------------------------------------------------

$Mappings = [ordered]@{
    "مناطق المناديب"          = "Account_Place"
    "المندوبون"              = "Account_SalesMan"
    "المندوبين"              = "Account_SalesMan"
    "المبيعات"               = "Order_Orders"
    "المشتريات"              = "Order_Purchases"
    "مرتجعات المبيعات"       = "Order_OrderReturn"
    "مرتجعات المبيعات بدون فاتورة" = "Order_OrderReturn"
    "مرتجعات المبيعات بفاتورة"     = "Order_OrderReturn"
    "مرتجعات المشتريات"      = "Order_PurchasesReturn"
    "مرتجعات المشتريات بدون فاتورة" = "Order_PurchasesReturn"
    "مرتجعات المشتريات بفاتورة"     = "Order_PurchasesReturn"
    "الحسابات"               = "Account_Accounts"
    "شجرة الحسابات"          = "Account_Accounts"
    "الفروع"                 = "Account_Branch"
    "العملاء"                = "Account_DefualtCustomer"
    "الموردون"               = "Account_CustSup"
    "الموردين"               = "Account_CustSup"
    "المخازن"                = "Account_Stores"
    "المستودعات"             = "Account_Stores"
    "مراكز التكلفة"          = "Account_CostCenters"
    "المشاريع"               = "Account_Projects"
    "الأصناف"                = "Item_Items"
    "الصنف"                  = "Item_Items"
    "الوحدات"                = "Item_Unit"
    "الفئات"                 = "Item_Class"
    "المجموعات"              = "Item_Groups"
    "الشركات"                = "Item_Company"
    "الجرد"                  = "Order_Gard"
    "الكميات الافتتاحية"     = "Order_OpenQuantity"
    "كميات المخزون"          = "ItemQuantity"
    "عروض الأسعار"           = "Order_PriceOffer"
    "عروض اسعار"             = "Order_PriceOffer"
    "عروض أسعار"             = "Order_PriceOffer"
    "تحويلات المخازن"        = "Order_StoreTransfer"
    "تحويل إلى فرع"          = "Order_TransferToBranch"
    "تحويل من فرع"           = "Order_TransferFromBranch"
    "طلب التحويل إلى فرع"    = "Order_TransferToBranch"
    "طلب الاستقبال من فرع"   = "Order_TransferFromBranch"
    "طلبات التحويل للفروع"   = "Order_TransferToBranch"
    "أمر توريد المخازن"      = "Order_RecItem"
    "أمر توريد للمخازن"      = "Order_RecItem"
    "أمر صرف للمخازن"        = "Order_PaymentItem"
    "المستخدمون"             = "User_Login"
    "المستخدمين"             = "User_Login"
    "المستخدمون النظام"      = "User_Login"
    "مستخدم جديد"            = "User_Login"
    "إضافة مستخدم"           = "User_Login"
    "تعديل مستخدم"           = "User_Login"
    "مجموعات المستخدمين"     = "User_Groups"
    "مجموعة المستخدمين"      = "User_Groups"
    "الشاشات"                = "User_Screens"
    "شاشات النظام"           = "User_Screens"
    "الصلاحيات"              = "User_Permission"
    "صلاحيات الشاشات"        = "User_Permission"
    "التراخيص"               = "App_Licenses"
    "إدارة التراخيص"         = "App_Licenses"
    "العقود"                 = "Contract_Contract"
    "ضمانات العقود"          = "Contract_Guarantee"
    "بونص العقود"            = "Contract_Bounce"
    "الطابعات"               = "Printers"
    "الطابعة"                = "Printers"
    "إعدادات الطابعات"       = "Printers"
    "طابعات الكاشير"         = "PrintersCook"
    "طابعات المطبخ"          = "PrintersCook"
    "إعدادات طابعة الكاشير"  = "PrintersCook"
    "إعدادات طابعات المطبخ"  = "PrintersCook"
}

# ------------------------------------------------------------
# 5. إضافة الخرائط فقط إذا لم تكن موجودة
# ------------------------------------------------------------

$added = @()

foreach ($entry in $Mappings.GetEnumerator()) {

    $screen = $entry.Key
    $entity = $entry.Value

    $escapedScreen = [regex]::Escape($screen)

    $exists = $mapText -match ('\["' + $escapedScreen + '"\]\s*=')

    if (!$exists) {

        $line =
            '            ["' +
            $screen.Replace('"','\"') +
            '"] = "' +
            $entity +
            '",'

        # نحاول إدراجها قبل آخر عناصر القاموس
        $marker = "            // AUTO-WIRED SCREENS"

        if ($mapText.Contains($marker)) {
            $mapText = $mapText.Replace(
                $marker,
                $line + [Environment]::NewLine + $marker
            )
        }
        else {
            $pos = $mapText.LastIndexOf("        };")

            if ($pos -ge 0) {
                $mapText =
                    $mapText.Substring(0,$pos) +
                    $line +
                    [Environment]::NewLine +
                    $mapText.Substring($pos)
            }
        }

        $added += "$screen -> $entity"
    }
}

Set-Content -Path $map -Value $mapText -Encoding UTF8

Write-Host ""
Write-Host "=== الخرائط ===" -ForegroundColor Cyan

if ($added.Count -eq 0) {
    Write-Host "[OK] لا توجد خرائط أساسية ناقصة." -ForegroundColor Green
}
else {
    foreach ($x in $added) {
        Write-Host "[ADD] $x" -ForegroundColor Yellow
    }
}

# ------------------------------------------------------------
# 6. قراءة Forms الموجودة
# ------------------------------------------------------------

$formsFound = @()

if (Test-Path $Forms) {

    $formsFound = Get-ChildItem $Forms -Filter "*.cs" -Recurse |
        ForEach-Object {
            [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        }
}

Write-Host ""
Write-Host "=== FORMS ===" -ForegroundColor Cyan
Write-Host "عدد Forms: $($formsFound.Count)"

# ------------------------------------------------------------
# 7. فحص أسماء الشاشات الموجودة في Router
# ------------------------------------------------------------

$routerText = Get-Content $router -Raw

$routerChecks = [ordered]@{
    "RealScreenCatalog"       = ($routerText -match "RealScreenCatalog\.TryCreate")
    "DynamicErpScreenForm"    = ($routerText -match "DynamicErpScreenForm")
    "FindFormType"            = ($routerText -match "FindFormType")
    "TryCreateForm"           = ($routerText -match "TryCreateForm")
    "UserManagementForm"      = ($routerText -match "UserManagementForm")
    "ItemMasterForm"          = ($routerText -match "ItemMasterForm")
    "InvoicesForm"            = ($routerText -match "InvoicesForm")
}

Write-Host ""
Write-Host "=== ROUTER ===" -ForegroundColor Cyan

foreach ($x in $routerChecks.GetEnumerator()) {

    if ($x.Value) {
        Write-Host "[OK]   $($x.Key)" -ForegroundColor Green
    }
    else {
        Write-Host "[MISS] $($x.Key)" -ForegroundColor Red
    }
}

# ------------------------------------------------------------
# 8. فحص ScreenEntityMap
# ------------------------------------------------------------

$mapText = Get-Content $map -Raw

$mapCount = ([regex]::Matches(
    $mapText,
    '\["[^"]+"\]\s*=\s*"[^"]+"'
)).Count

Write-Host ""
Write-Host "=== ENTITY MAP ===" -ForegroundColor Cyan
Write-Host "عدد الخرائط: $mapCount"

# ------------------------------------------------------------
# 9. البحث عن DynamicErpScreenForm
# ------------------------------------------------------------

$dynamicForm = Join-Path $Forms "DynamicErpScreenForm.cs"

if (Test-Path $dynamicForm) {
    Write-Host "[OK] DynamicErpScreenForm.cs" -ForegroundColor Green
}
else {
    Write-Host "[MISS] DynamicErpScreenForm.cs" -ForegroundColor Red
}

# ------------------------------------------------------------
# 10. فحص أخطاء C# الواضحة
# ------------------------------------------------------------

Write-Host ""
Write-Host "=== فحص تعارضات Git داخل C# ===" -ForegroundColor Cyan

$conflicts = Get-ChildItem $Src -Filter "*.cs" -Recurse |
    Select-String -Pattern '<<<<<<<|=======|>>>>>>>' -SimpleMatch

if ($conflicts) {

    Write-Host "[ERROR] توجد تعارضات Git:" -ForegroundColor Red

    $conflicts |
        Select-Object Path,LineNumber,Line |
        Format-Table -AutoSize

}
else {
    Write-Host "[OK] لا توجد تعارضات Git في ملفات C#." -ForegroundColor Green
}

# ------------------------------------------------------------
# 11. حفظ التقرير
# ------------------------------------------------------------

$reportLines = @()

$reportLines += "AlSaqarAccounting - Screen Wiring Report"
$reportLines += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += ""
$reportLines += "Repository: $Root"
$reportLines += "Forms: $($formsFound.Count)"
$reportLines += "Entity mappings: $mapCount"
$reportLines += ""
$reportLines += "=== Added mappings ==="

if ($added.Count -eq 0) {
    $reportLines += "None"
}
else {
    $reportLines += $added
}

$reportLines += ""
$reportLines += "=== Router checks ==="

foreach ($x in $routerChecks.GetEnumerator()) {
    $reportLines += "$($x.Key) = $($x.Value)"
}

$reportLines += ""
$reportLines += "=== Git conflicts ==="

if ($conflicts) {
    foreach ($c in $conflicts) {
        $reportLines += "$($c.Path):$($c.LineNumber): $($c.Line)"
    }
}
else {
    $reportLines += "None"
}

$reportLines | Set-Content $Report -Encoding UTF8

Write-Host ""
Write-Host "[OK] التقرير:" -ForegroundColor Green
Write-Host $Report

# ------------------------------------------------------------
# 12. فحص وجود MSBuild
# ------------------------------------------------------------

$msbuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)

$MSBuild = $msbuildCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if (!$MSBuild) {
    throw "لم يتم العثور على MSBuild."
}

Write-Host ""
Write-Host "=== BUILD NET48 ===" -ForegroundColor Cyan
Write-Host $MSBuild

# ------------------------------------------------------------
# 13. تحديد ملف المشروع
# ------------------------------------------------------------

$project = Get-ChildItem $Src -Filter "*.csproj" -File |
    Select-Object -First 1

if (!$project) {
    throw "لم يتم العثور على ملف csproj داخل $Src"
}

Write-Host "[PROJECT] $($project.FullName)" -ForegroundColor Green

# ------------------------------------------------------------
# 14. تنظيف وبناء
# ------------------------------------------------------------

& $MSBuild $project.FullName `
    /t:Clean,Build `
    /p:Configuration=Release `
    /p:Platform="Any CPU" `
    /m

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Write-Host "لم يتم إنشاء إصدار نهائي لأن البناء فشل." -ForegroundColor Red
    exit 2
}

Write-Host ""
Write-Host "BUILD SUCCESS" -ForegroundColor Green

# ------------------------------------------------------------
# 15. البحث عن EXE
# ------------------------------------------------------------

$exeCandidates = @(
    "$Src\bin\Any CPU\Release\net48\AlSaqarAccounting.exe",
    "$Src\bin\Release\net48\AlSaqarAccounting.exe"
)

$exe = $exeCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if (!$exe) {

    $exe = Get-ChildItem $Src -Filter "AlSaqarAccounting.exe" -Recurse -File |
        Where-Object {
            $_.FullName -notmatch "\\obj\\"
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (!$exe) {
    throw "BUILD نجح لكن لم يتم العثور على AlSaqarAccounting.exe"
}

Write-Host ""
Write-Host "=== EXE ===" -ForegroundColor Cyan
Get-Item $exe | Select-Object FullName,Length,LastWriteTime

# ------------------------------------------------------------
# 16. اختبار appsettings
# ------------------------------------------------------------

$appSettings = Join-Path (Split-Path $exe) "appsettings.json"

if (Test-Path $appSettings) {
    Write-Host "[OK] appsettings.json موجود." -ForegroundColor Green
}
else {
    Write-Host "[WARNING] appsettings.json غير موجود بجانب EXE." -ForegroundColor Yellow
}

# ------------------------------------------------------------
# 17. ملخص نهائي
# ------------------------------------------------------------

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " انتهى الفحص والبناء" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "Forms           : $($formsFound.Count)"
Write-Host "Entity mappings : $mapCount"
Write-Host "EXE             : $exe"
Write-Host "Report          : $Report"

Write-Host ""
Write-Host "لم يتم إنشاء Release جديد أو Tag تلقائياً." -ForegroundColor Yellow
Write-Host "يجب اختبار البرنامج أولاً." -ForegroundColor Yellow
Write-Host ""

# فتح التقرير تلقائياً
Start-Process notepad.exe $Report