# AlSaqarAccounting — Phase 5

الهدف: تحويل أول مجموعة من عناصر القائمة العربية الظاهرة من قاعدة `GTSdb2026` إلى شاشات تشغيلية حقيقية لقراءة البيانات، مع إبقاء الصلاحيات مرتبطة بالقاعدة.

تشغيل:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\PHASE5_BUILD_RUN.ps1
```

للبناء وتشغيل نسخة Release:

```powershell
.\PHASE5_BUILD_RUN.ps1 -Run
```

هذه المرحلة لا تُغير قاعدة البيانات ولا تُنفذ عمليات كتابة.
