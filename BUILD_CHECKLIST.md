# Build checklist — .NET Framework 4.8

1. Windows + Visual Studio 2022 مع workload **Desktop development with .NET**.
2. SQL Server يحتوي على قاعدة **GTSdb2026**.
3. عدّل `src/AlSaqarAccounting/appsettings.json` حسب جهازك.
4. افتح `AlSaqarAccounting.sln` في Visual Studio 2022.
5. Restore NuGet ثم Build بنمط Release.
6. ملف التشغيل المتوقع: `src/AlSaqarAccounting/bin/Release/net48/AlSaqarAccounting.exe`.
7. استخدم `Phase7_Verify.ps1` للتحقق من البناء محلياً.
8. GitHub Actions يستخدم MSBuild على Windows للتحقق من نفس مشروع .NET Framework 4.8.
