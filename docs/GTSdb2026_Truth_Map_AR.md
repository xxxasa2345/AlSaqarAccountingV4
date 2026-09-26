# خريطة الحقيقة لقاعدة GTSdb2026

## هوية قاعدة البيانات

- الاسم: `dboGTSdb2026`
- Database ID: `5`
- تاريخ الإنشاء: `2026-08-17 11:07:37.567`
- Compatibility Level: `120`
- Recovery Model: `FULL`
- Collation: `Latin1_General_CI_AS`
- الحالة: `ONLINE`
- SQL Server: `DESKTOP-KBU5DH6`
- Product Version: `15.0.2000.5`
- Edition: `Enterprise Evaluation Edition (64-bit)`

## ما تم إثباته من الملف

- عدد الجداول: **180** (يشمل `__EFMigrationsHistory`)
- عدد صفوف الأعمدة المفحوصة: **3241**
- عدد PK/UNIQUE records التي ظهرت في التصدير الحالي: **175**
- مجموع الصفوف التقريبي في الجداول: **1,319**
- الجداول التي بها بيانات: **53**
- الجدول `__EFMigrationsHistory` موجود، ما يدعم وجود تاريخ EF Migrations في القاعدة.
- الجدول `dbo.User_Login` موجود وبه 27 حقلاً في التصدير الحالي.

## ملاحظة مهمة على الاستخراج

هذا الملف الحالي يحتوي بشكل موثوق على **قائمة الجداول + الأعمدة + مفاتيح PK/Unique الظاهرة**.
ولا يحتوي على نتائج كاملة مستقلة لـ:
Stored Procedures / Procedure Parameters / Views / Functions / Triggers / Foreign Keys / Dependencies.
لذلك لا ينبغي توليد كود الأعمال النهائي منها وحدها.

## أكبر الوحدات حسب عدد الجداول

| Module | Tables | Approx Rows |
|---|---:|---:|
| Order | 46 | 3 |
| Account | 20 | 293 |
| Item | 11 | 16 |
| Virg | 9 | 9 |
| Emp | 9 | 0 |
| Contract | 8 | 1 |
| User | 6 | 471 |
| OrderRent | 6 | 0 |
| Scaffold | 6 | 0 |
| Scaffolds | 6 | 0 |
| Tran | 5 | 34 |
| Restaurant | 5 | 5 |
| OrderDalala | 4 | 0 |
| Repairs | 4 | 0 |
| Orders | 2 | 0 |
| Ordert | 2 | 0 |
| TempTrialBalanceByTotal | 1 | 207 |
| RebuildAllIndexes | 1 | 143 |
| TempBudget | 1 | 74 |
| LoginLogs | 1 | 47 |

## الجداول الأكثر احتواءً على بيانات

| Table | Approx Rows | Columns |
|---|---:|---:|
| User_Permission | 301 | 18 |
| Account_Accounts | 217 | 26 |
| TempTrialBalanceByTotal | 207 | 14 |
| User_Screens | 159 | 6 |
| RebuildAllIndexes | 143 | 2 |
| TempBudget | 74 | 21 |
| LoginLogs | 47 | 6 |
| Account_DefualtAccount2 | 40 | 5 |
| Tran_Type | 28 | 2 |
| Virg_Virgins | 9 | 4 |
| User_SellPrice | 5 | 2 |
| Tran_TranDetails | 5 | 11 |
| Account_DefualtCustomer | 5 | 5 |
| CashReceiptsType | 5 | 2 |
| Account_Type | 4 | 2 |
| Item_Add | 4 | 9 |
| Account_DefualtAccount | 4 | 15 |
| Restaurant_Type | 4 | 2 |
| Account_Final | 3 | 2 |
| Account_Nature | 3 | 2 |
| Tafqit | 3 | 2 |
| Item_Type | 3 | 2 |
| User_Login | 3 | 27 |
| User_Groups | 3 | 18 |
| Items_ContentAlignment_ForCashier | 3 | 2 |

## جداول النواة المحاسبية

- `Account_Accounts`
- `Account_AccountType`
- `Account_Branch`
- `Account_CostCenters`
- `Account_CustSup`
- `Account_CustTailor`
- `Account_DefualtAccount`
- `Account_DefualtAccount2`
- `Account_DefualtCustomer`
- `Account_Final`
- `Account_Nature`
- `Account_Place`
- `Account_Projects`
- `Account_ProjectsDetails`
- `Account_Receipts`
- `Account_ReceiptsDetails`
- `Account_SalesMan`
- `Account_Stores`
- `Account_Suspended`
- `Account_Type`
- `AccountStartBalance`
- `AccountYearEndClosing`
- `AccountYears`
- `Tran_Tran`
- `Tran_TranDetails`
- `Tran_TranTemp`
- `Tran_TranTempDetails`
- `Tran_Type`

## جداول المستخدمين والصلاحيات

- `User_DayClose`
- `User_Groups`
- `User_Login`
- `User_Permission`
- `User_Screens`
- `User_SellPrice`

## جداول الطلبات/المبيعات/المشتريات

- `Order_CheckoutOrders`
- `Order_CheckoutOrdersDetails`
- `Order_Extension`
- `Order_Gard`
- `Order_GardDetails`
- `Order_InventorySettlementMinus`
- `Order_InventorySettlementMinusDetails`
- `Order_Manufacturing`
- `Order_ManufacturingDetailsItems`
- `Order_ManufacturingDetailsNew`
- `Order_ManufacturingOrder`
- `Order_ManufacturingOrderDetails`
- `Order_MoveHall`
- `Order_OpenQuantity`
- `Order_OpenQuantityDetails`
- `Order_OrderReturn`
- `Order_OrderReturnDetails`
- `Order_Orders`
- `Order_OrdersDetails`
- `Order_OrdersDetailsDraft`
- `Order_OrdersDraft`
- `Order_PaymentItem`
- `Order_PaymentItemDetails`
- `Order_PriceOffer`
- `Order_PriceOfferDetails`
- `Order_Purchases`
- `Order_PurchasesDetails`
- `Order_PurchasesOrder`
- `Order_PurchasesOrderDetails`
- `Order_PurchasesReturn`
- `Order_PurchasesReturnDetails`
- `Order_RecItem`
- `Order_RecItemDetails`
- `Order_Reservation`
- `Order_Reservations`
- `Order_StoreTransfer`
- `Order_StoreTransferDetails`
- `Order_Transfer_Master`
- `Order_Transfer_MasterDetails`
- `Order_Transfer_StatusType`
- `Order_Transfer_TrackingType`
- `Order_TransferFromBranch`
- `Order_TransferFromBranchDetails`
- `Order_TransferToBranch`
- `Order_TransferToBranchDetails`
- `Order_TypeElectronicInvoice`
- `OrderDalala_Dalala`
- `OrderDalala_DalalaDetails`
- `OrderDalala_Sending`
- `OrderDalala_SendingDetails`
- `OrderRent_Recipt`
- `OrderRent_Recipt3`
- `OrderRent_RecpitDetails`
- `OrderRent_RecpitDetails3`
- `OrderRent_Rent`
- `OrderRent_RentDetails`
- `Orders_PriceShowRental`
- `Orders_RentalInvoice`
- `OrdersReturn_RentalInvoice`
- `Ordert_PriceShowRentalDetails`
- `Ordert_RentalInvoiceDetails`
- `OrdertReturn_RentalInvoiceDetails`

## نتيجة هندسية للبناء

القاعدة لا ينبغي التعامل معها كـ CRUD بسيط.
المخطط الأنسب للمشروع:

`WinForms → Application Services → Repositories/SQL → GTSdb2026`

مع إبقاء منطق SQL الأصلي (خصوصًا Stored Procedures/Functions/Views) كمصدر لعقد الأعمال عندما يتم تصديره بالكامل.

## الجداول التي تحتاج عناية خاصة

- `Account_Accounts`: يحتوي على 217 سجلًا و26 حقلاً في التصدير الحالي.
- `Tran_Tran`: يحتوي على 20 حقلاً ويظهر فيه `ReferenceCode`, `TranTypeID`, `TranDate`, `LedgerID`, `ProjectId`, `YearId`.
- `Tran_TranDetails`: يحتوي على `Debit`, `Credit`, `Account_Sn`, `CostCentersID`, `ProjectId`, `YearId`.
- `Order_Orders`: يحتوي على 87 حقلاً في التصدير الحالي، ما يدل على أن كيان الطلب/الفاتورة مركب جدًا.
- `Order_OrdersDetails`: يحتوي على 40 حقلاً، منها الكمية والسعر والضريبة والخصم والوزن والأبعاد.
- `User_Permission`: يحتوي على صلاحيات صريحة مثل `Allow_Enter`, `Allow_Save`, `Allow_Edit`, `Allow_Delete`, `Allow_Print`, `Allow_Export`.
- `User_Login`: يحتوي على معلومات مستخدم/فرع/مجموعة وحالة فتح اليوم ومفاتيح/بيانات مرتبطة بالفوترة الإلكترونية.

> لا تتم قراءة أو استخراج قيم كلمات المرور من `User_Login` في هذه الخريطة.

