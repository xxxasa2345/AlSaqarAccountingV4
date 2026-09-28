# المرحلة الثانية — الشاشات الرئيسية الفعلية

تم ترحيل ثلاث شاشات رئيسية من المسار التشغيلي العام إلى تنفيذ ERP فعلي:

- `FrmCompany` → `dbo.Item_Company`
- `FrmClass` → `dbo.Item_Class`
- `FrmGroups` → `dbo.Item_Groups`

## المعمارية

`Form → ItemMasterService → DbExecutor → SQL Server`

الـ Form لا ينشئ `SqlConnection` ولا `SqlCommand` مباشرة.

## الحماية

أسماء الجداول لا تأتي من المستخدم. الخدمة تقبل فقط الجداول الثلاثة المعرفة في whitelist، بينما قيم الإدخال تستخدم SQL parameters.

## الصلاحيات

يتم احترام `AllowEnter`, `AllowSave`, `AllowEdit`, و`AllowDelete` القادمة من `ScreenAccess`.

## الترحيل التدريجي

`OperationalDataScreen` و`CatalogDataScreen` ما زالا fallback للشاشات التي لم تُرحّل بعد. لا يتم اعتبارهما تنفيذًا نهائيًا للشاشة.
