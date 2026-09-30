# المرحلة 11 — الكميات الافتتاحية وتسوية الجرد

أضيفت شاشة **الكميات الافتتاحية** كمسار حقيقي مع:
- قراءة عبر `Select_Order_OpenQuantity`
- إضافة عبر `Insert_OpenQuantity` باستخدام `Items_OpenQuantity`
- تعديل عبر `Update_OpenQuantity` باستخدام `Items_OpenQuantity`
- حذف عبر `Delete_Order_OpenQuantity`
- طباعة/تفاصيل عبر `Print_Order_OpenQuantity`
- تحميل تفاصيل السطور من `Order_OpenQuantityDetails` بقراءة parameterized داخل الخدمة.

كما أضيفت شاشة **تسوية جرد بالنقص** للقراءة والطباعة باستخدام:
`Select_Order_InventorySettlementMinus` و`Print_Order_InventorySettlementMinus`.

لم تتم إضافة كتابة غير موثقة إلى تسوية الجرد بالنقص؛ كتالوج الإجراءات المتاح يثبت القراءة والطباعة فقط لهذه العملية.
