# AlSaqarAccounting — Phase 4: القائمة الرئيسية والصلاحيات

تمت إضافة طبقة قائمة رئيسية عربية تعتمد على جداول الأمان الأصلية في GTSdb2026:

- `dbo.User_Groups`
- `dbo.User_Screens`
- `dbo.User_Permission`

## سلوك الصلاحيات

يتم تحميل الشاشات التي تحقق:

- `User_Screens.ISShow = 1` (أو NULL)
- وجود سجل `User_Permission` للمجموعة الحالية
- `User_Permission.Allow_Enter = 1`

وتظهر صلاحيات الشاشة الحالية من:

`Allow_Save`, `Allow_Edit`, `Allow_Delete`, `Allow_Print`, `Allow_Export`, `Allow_Branch`.

## فتح الشاشة

يستخدم `ScreenRouter` المسار التالي:

1. إذا كان Form الأصلي المستخرج موجودًا داخل تجميعات التطبيق، يحاول فتحه بالاسم.
2. إذا لم يوجد تنفيذ Form بعد داخل V4، يفتح شاشة DB-backed للقراءة فقط حسب خريطة الكيان.

لا يتم تنفيذ أي INSERT/UPDATE/DELETE/TRUNCATE خلال طبقة القائمة أو شاشة fallback.

## ملاحظة مهمة

فهرس المشروع الأصلي يحتوي 369 Form، لكن V4 الحالي يحتوي البنية وقواعد البيانات والـModels ولا يحتوي تنفيذ كل Form الأصلي. لذلك تم تصميم الـRouter ليستقبل تنفيذات Form الأصلية فور إضافتها دون تغيير طبقة الصلاحيات.
