# المرحلة 10 — شاشات المخزون والتحويلات والمرتجعات

تم ربط مجموعة إضافية من الشاشات الحقيقية مع إجراءات GTSdb2026 بدل شاشة الكتالوج العامة:

- التحويل إلى فرع: `Select_Order_TransferToBranch` و`Print_Order_TransferToBranch`
- التحويل من فرع: `Select_Order_TransferFromBranch` و`Print_Order_TransferFromBranch`
- تحويلات المخازن: `Select_Order_StoreTransfer` و`Print_Order_StoreTransfer`
- مرتجعات المبيعات: `Select_Order_OrderReturn` و`Print_Order_OrderReturn`
- مرتجعات المشتريات: `Select_Order_PurchasesReturn` و`Print_Order_PurchasesReturn`
- عروض الأسعار: `Select_Order_PriceOffer` و`GetPriceOffersWithDetails`
- ضمانات العقود: `Select_Contract_Guarantee` مع تفاصيل الضمان
- الجرد: قراءة رؤوس الجرد من `Order_Gard` وتفاصيلها عبر `Select_OrderGard`
- كميات المخزون: `Get_Items_Stock_Report`
- بونص العقود: شاشة مستقلة مرتبطة بجدول `Contract_Bounce` حتى لا يرث مسار العقود.

عمليات الحفظ/التعديل التي لا يوجد لها عقد Stored Procedure موثق في كتالوج GTSdb2026 لم يتم اختراعها أو استبدالها بكتابة مباشرة على الجداول.
