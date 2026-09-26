# AlSaqarAccounting V4

هذه النسخة مبنية على نتائج reverse engineering الفعلية لقاعدة `GTSdb2026`.

## مثبت من المصدر
- SQL Server ProductVersion: `15.0.2000.5`
- Compatibility Level: `120`
- Recovery Model: `FULL`
- Collation: `Latin1_General_CI_AS`
- Tables: `180`
- Stored Procedures: `341`
- Functions: `43`
- Views: `19`
- Foreign Keys: `12`
- PK/Unique records: `175`
- EF Migrations: `20260921012338_InitialAccountingSchema`, ProductVersion `8.0.12`

## التصميم
`WinForms (.NET 8) -> Application/SQL Services -> GTSdb2026`

لا يتم توليد منطق CRUD جديد بدل منطق SQL الأصلي عندما يكون الإجراء المخزن هو مصدر السلوك.

## الحالة
- تم توليد Models لكل الجداول الـ180.
- تم توليد فهرس الإجراءات وتواقيعها.
- تم إدراج وظائف وViews والعلاقات والاعتماديات المستخرجة.
- تعريفات أجسام Stored Procedures غير موجودة في ملفات CSV المرفوعة حتى الآن؛ يوجد في `database/Export_Procedure_Definitions_Chunks.sql` تصدير Read-Only لها على أجزاء 2000 حرف.

## تشغيل
يفتح في Visual Studio 2022 على Windows مع .NET 8 SDK.
عدل `src/AlSaqarAccounting/appsettings.json` حسب SQL Server لديك.

## ملاحظة أمنية
لا يتم عرض أو استخراج قيم `User_Login.PassWord` ضمن الكتالوجات.
طريقة التحقق الحالية تطابق العمود الموجود؛ يجب استبدالها منطقياً بالإجراء الأصلي إذا كشف تصدير الإجراءات طريقة تحقق مختلفة.

## أكبر الإجراءات حسب حجم التعريف
- `GetReport_OrderDetailsSmall` — 999 حرف
- `GetTotalAvergForSore_ByItem` — 991 حرف
- `GetAllBranches` — 97 حرف
- `Select_Account_CusTailor` — 957 حرف
- `Insert_Tran_Tran` — 946 حرف
- `GetQuantityFromItem_ByItemCode_StoreId` — 9,440 حرف
- `Update_OpenQuantity` — 944 حرف
- `Get_ContractCustomer` — 921 حرف
- `UpdateCsr` — 914 حرف
- `GetAccountBalance` — 914 حرف
- `GetRented` — 908 حرف
- `Select_SearchAccountTran` — 887 حرف
- `Get_ItemGroupToPrintCashier` — 885 حرف
- `Get_Item_BySearchOpenQauntity` — 884 حرف
- `Print_Order_OrderReturn` — 8,676 حرف
- `Get_MizanBarcode` — 861 حرف
- `Get_Items_ByCode` — 822 حرف
- `Select_Scaffolds_ContractNotMinutesStarted` — 798 حرف
- `Select_Order_TransferFromBranch` — 791 حرف
- `Select_Order_TransferToBranch` — 785 حرف
