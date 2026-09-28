# AlSaqarAccounting — Phase 7 (NET48)

## الهدف
تثبيت طبقة CI/CD بسيطة للمشروع بحيث يتحقق GitHub Actions من:

- Restore عبر MSBuild على Windows لمشروع .NET Framework 4.8
- Build Release
- إنشاء ZIP من ملفات Release الخاصة بـ .NET Framework 4.8
- التحقق من وجود `AlSaqarAccounting.exe`
- إنشاء Artifact للتجربة

## الأمان
الـ workflow لا يتصل بقاعدة `GTSdb2026` المحلية، ولا يرفع `appsettings.json` إلى Artifact. بعد عملية publish داخل GitHub يتم حذف `appsettings.json` من مجلد الـ Artifact لأن الملف المحلي قد يحتوي على إعداد اتصال بقاعدة البيانات.

## التشغيل المحلي
ضع الملفات في مجلد المشروع:

```text
Phase7_Install.ps1
Phase7_Run.cmd
```

ثم شغّل:

```text
Phase7_Run.cmd
```

يمكن أيضًا التنفيذ من PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Phase7_Install.ps1 -Root (Get-Location).Path
```

## الناتج
يُنشأ:

```text
.github\workflows\AlSaqarAccounting-NET48.yml
Phase7_Verify.ps1
phase7-install.log
backup\phase7-YYYYMMDD-HHMMSS
```

بعد `git push` سيظهر Workflow باسم **AlSaqarAccounting .NET Framework 4.8** في GitHub Actions، وسيُنشر Artifact باسم:

```text
AlSaqarAccounting-NET48
```

## لا يغيّر قاعدة البيانات
Phase 7 لا ينفّذ SQL على `GTSdb2026` ولا ينشئ جداول أو Stored Procedures جديدة.


### إصلاح V7.1
تم إصلاح مشكلة GetFullPath مع مسار المشروع. مشغل CMD لا يمرر Root أصلًا؛ يعتمد على مجلد السكربت بعد cd /d.
