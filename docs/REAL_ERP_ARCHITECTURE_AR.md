# معمارية ERP الحقيقية — AlSaqarAccounting V4

هذا المستند يحدد الانتقال من شاشات `OperationalDataScreen` العامة إلى شاشات ERP فعلية مرتبطة مباشرة بمنطق الأعمال وقاعدة `GTSdb2026`.

## التدفق المعتمد

```text
Windows Forms
    ↓
Screen / Form
    ↓
Business Service
    ↓
Validation + Permissions
    ↓
Database Boundary / Repository
    ↓
SQL Server
    ↓
GTSdb2026
    ↓
Result / Transaction Outcome
    ↓
Service
    ↓
Form
```

## قواعد التنفيذ

1. الشاشة لا تنفذ SQL مباشرة.
2. كل وحدة أعمال لها Service مستقل.
3. الوصول إلى SQL يمر عبر `DbExecutor` أو `StoredProcedureExecutor`.
4. جميع القيم المدخلة تستخدم Parameters ولا تستخدم string concatenation للبيانات.
5. الصلاحيات تأتي من `User_Groups / User_Permission / User_Screens` عبر طبقة الأمن الحالية.
6. `OperationalDataScreen` و`CatalogDataScreen` يبقيان كـ compatibility fallback فقط أثناء ترحيل الشاشات.
7. لا يتم افتراض أعمدة غير موجودة في forensic schema؛ كل شاشة جديدة تبدأ من `Models` و`catalogs` والتحقق من قاعدة البيانات.
8. عمليات الحفظ والتعديل والحذف يجب أن تسجل حقول التدقيق الموجودة في قاعدة البيانات عندما تكون متاحة.

## أول شاشة مهاجرة

`FrmUnit` → `Item_Unit`

تم تنفيذها كالتالي:

- `Forms/ItemUnitForm.cs` للواجهة.
- `Services/ItemUnitService.cs` لمنطق الأعمال.
- `Core/DbExecutor.cs` كحد مركزي لتنفيذ SQL.
- `UI/ScreenRouter.cs` يوجه `FrmUnit` إلى الشاشة الحقيقية قبل الـ fallback.

## خطة الترحيل التالية

1. `FrmCompany` → `Item_Company`
2. `FrmClass` → `Item_Class`
3. `FrmGroups` → `Item_Groups`
4. `FrmItems` → `Item_Items` / `Get_All_Items`
5. العملاء والموردون.
6. الحسابات والحركات المحاسبية.
7. المبيعات والمشتريات.
8. المخزون.
9. التقارير.

كل شاشة تُرحّل وتُختبر بشكل مستقل قبل إزالة fallback الخاص بها.
