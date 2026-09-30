# المساهمة في المشروع

شكراً لاهتمامك بالمساهمة في AlSaqar Accounting! 🎉

---

## نسخ من المستودع والعمل عليه

### 1. Fork المستودع
```bash
# من صفحة المستودع اضغط "Fork"
```

### 2. استنسخ نسختك المحلية
```bash
git clone https://github.com/YOUR_USERNAME/AlSaqarAccountingV4.git
cd AlSaqarAccountingV4
git remote add upstream https://github.com/xxxasa2345/AlSaqarAccountingV4.git
```

### 3. أنشئ فرع جديد
```bash
# استخدم أسماء واضحة للفروع
git checkout -b feature/add-new-feature
# أو
git checkout -b bugfix/fix-login-issue
```

---

## معايير الكود

### C# Coding Standards
```csharp
// استخدم PascalCase للأسماء العامة
public class UserAccount { }
public void ProcessTransaction() { }

// استخدم camelCase للمتغيرات المحلية
private string userName;
private int transactionAmount;

// أضف تعليقات واضحة
/// <summary>
/// معالجة تسجيل الدخول للمستخدم
/// </summary>
/// <param name="username">اسم المستخدم</param>
/// <param name="password">كلمة المرور</param>
/// <returns>نتيجة المحاولة</returns>
public bool Login(string username, string password)
{
    // الكود هنا
}

// احرص على معالجة الأخطاء
try
{
    // العملية
}
catch (SqlException ex)
{
    Logger.Error($"خطأ قاعدة بيانات: {ex.Message}");
    throw;
}
```

### عرض الملفات
```
src/
├── AlSaqarAccounting/
│   ├── Models/                 # نماذج البيانات
│   ├── Services/              # الخدمات
│   ├── Views/                 # واجهات المستخدم (WinForms)
│   ├── ViewModels/            # منطق الواجهة
│   ├── Data/                  # Entity Framework DbContext
│   ├── appsettings.json       # الإعدادات
│   └── Program.cs             # نقطة الدخول
│
├── database/
│   ├── InitialSchema.sql      # إنشاء الجداول الأساسية
│   └── StoredProcedures/      # الإجراءات المخزنة
│
└── docs/
    ├── API.md                 # توثيق الواجهات
    └── DATABASE.md            # توثيق قاعدة البيانات
```

---

## خطوات الإرسال (Commit)

### 1. اختبر التغييرات
```bash
# بناء المشروع
msbuild AlSaqarAccounting.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"

# تشغيل الاختبارات (إن وجدت)
لا توجد اختبارات dotnet test حالياً؛ استخدم `Phase7_Verify.ps1` للتحقق من البناء.
```

### 2. التزم بالتغييرات
```bash
# أضف الملفات المتغيرة
git add .

# أو أضف ملفات معينة
git add src/AlSaqarAccounting/Models/User.cs

# التزم بتعليق واضح
git commit -m "الميزة: إضافة نموذج المستخدم الجديد

- إضافة خصائص أساسية
- إضافة validation
- تحديث قاعدة البيانات"
```

### 3. ادفع التغييرات
```bash
git push origin feature/add-new-feature
```

---

## إرسال Pull Request (PR)

### 1. افتح Pull Request
```
على GitHub → "Compare & pull request"
```

### 2. ملء نموذج PR
```markdown
## الوصف
وصف مختصر للتغييرات...

## نوع التغيير
- [ ] إضافة ميزة جديدة
- [ ] إصلاح خطأ
- [ ] تحسين الأداء
- [ ] تحديث التوثيق

## الاختبار
- [ ] تم الاختبار محلياً
- [ ] بدون أخطاء Build
- [ ] الاتصال بقاعدة البيانات يعمل

## صور/فيديو (اختياري)
إذا كانت واجهة مستخدم، أرفق صورة

## قائمة التحقق
- [ ] قرأت التوثيق
- [ ] اتبعت معايير الكود
- [ ] لا توجد تكرارات
- [ ] أضفت تعليقات حيث لزم الأمر
```

---

## معايير الجودة

### ✅ قبل الإرسال، تأكد من:

1. **لا توجد أخطاء Build**
   ```bash
   msbuild AlSaqarAccounting.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"
   ```

2. **المشروع ينطلق بدون أخطاء**
   ```bash
   شغّل `src\AlSaqarAccounting\bin\Any CPU\Release\net48\AlSaqarAccounting.exe`
   ```

3. **الالتزام برعاية معايير الكود**
   - استخدام async/await عند الحاجة
   - تجنب hard-coded values
   - معالجة الأخطاء بشكل صحيح

4. **التعليقات واضحة**
   - XML Documentation
   - شرح المنطق المعقد

5. **التحديثات المرتبطة**
   - تحديث README إذا تغيرت الميزات
   - تحديث CHANGELOG

---

## معلومات إضافية

### هياكل البيانات الرئيسية
```csharp
// نموذج المستخدم
public class User
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedDate { get; set; }
}

// نموذج العملية المحاسبية
public class Transaction
{
    public int TransactionId { get; set; }
    public int AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public DateTime TransactionDate { get; set; }
}
```

### الإجراءات المخزنة الهامة
```sql
-- تسجيل الدخول
sp_ValidateUser
sp_GetUserPermissions

-- العمليات المحاسبية
sp_InsertTransaction
sp_UpdateAccount
sp_GetAccountBalance
```

---

## الاتصال والمساعدة

- **اسأل في Issues:** [GitHub Issues](https://github.com/xxxasa2345/AlSaqarAccountingV4/issues)
- **Discussions:** [مناقشات المشروع](https://github.com/xxxasa2345/AlSaqarAccountingV4/discussions)

---

## سياسة الفروع

| الفرع | الغرض | البيئة |
|-------|-------|--------|
| `main` | النسخة المستقرة | الإنتاج |
| `develop` | التطوير النشط | التطوير |
| `feature/*` | ميزات جديدة | التطوير |
| `bugfix/*` | إصلاحات | التطوير |
| `hotfix/*` | إصلاحات طارئة | الإنتاج |

---

## شكراً! 🙏

مساهمتك مهمة لنا. سيتم مراجعة PR في أقرب وقت ممكن.

**آخر تحديث:** 26 سبتمبر 2026
