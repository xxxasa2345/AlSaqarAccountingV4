using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class LicenseService
{
    private const string ProductName = "AlSaqarAccounting";
    private const string LocalFolder = "AlSaqarAccounting";
    private const string LocalFile = "license.dat";

    private readonly DbExecutor _db;

    public LicenseService(DbExecutor db) => _db = db;

    public static string Product => ProductName;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'dbo.App_Licenses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.App_Licenses
    (
        LicenseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_App_Licenses PRIMARY KEY,
        LicenseKeyHash CHAR(64) NOT NULL CONSTRAINT UQ_App_Licenses_KeyHash UNIQUE,
        LicenseType NVARCHAR(20) NOT NULL,
        ProductName NVARCHAR(100) NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        IssuedOn DATETIME2(0) NOT NULL,
        ExpiresOn DATETIME2(0) NULL,
        MaxUsers INT NOT NULL CONSTRAINT DF_App_Licenses_MaxUsers DEFAULT(1),
        MachineFingerprint NVARCHAR(200) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_App_Licenses_IsActive DEFAULT(1),
        Notes NVARCHAR(1000) NULL,
        CreatedByUserId INT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_App_Licenses_CreatedAt DEFAULT(SYSDATETIME())
    );
    CREATE INDEX IX_App_Licenses_ActiveExpiry ON dbo.App_Licenses(IsActive, ExpiresOn);
END";
        await _db.ExecuteAsync(sql, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureTrialAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var machine = GetMachineFingerprint();
        var count = await _db.QuerySingleAsync<LicenseCount>(
            "SELECT COUNT(*) AS LicenseCount FROM dbo.App_Licenses WHERE MachineFingerprint=@Machine AND IsActive=1;",
            p => p.AddWithValue("@Machine", machine), cancellationToken).ConfigureAwait(false);

        if (count != null && count.LicenseCount > 0)
            return;

        var key = GenerateKey();
        await CreateLicenseAsync(key, "Trial", "الصقر للمحاسبة", "تجربة محلية",
            DateTime.Now.Date.AddDays(30), 1, machine,
            "ترخيص تجريبي أُنشئ تلقائيًا عند أول تشغيل.", null, cancellationToken).ConfigureAwait(false);
        await SaveLocalLicenseAsync(key).ConfigureAwait(false);
    }

    public async Task<LicenseStatus> ValidateInstalledAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var local = await ReadLocalLicenseAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(local))
            return LicenseStatus.Invalid("لا يوجد ترخيص مثبت على هذا الجهاز.");

        var row = await _db.QuerySingleAsync<LicenseRow>(
            @"SELECT TOP (1) LicenseId, LicenseType, ProductName, CompanyName, CustomerName,
                     IssuedOn, ExpiresOn, MaxUsers, MachineFingerprint, IsActive
              FROM dbo.App_Licenses WHERE LicenseKeyHash=@Hash;",
            p => p.AddWithValue("@Hash", HashKey(local)), cancellationToken).ConfigureAwait(false);

        if (row == null) return LicenseStatus.Invalid("مفتاح الترخيص غير موجود في قاعدة البيانات.");
        if (!row.IsActive) return LicenseStatus.Invalid("الترخيص غير نشط.");
        if (row.ExpiresOn.HasValue && row.ExpiresOn.Value.Date < DateTime.Now.Date)
            return LicenseStatus.Invalid($"انتهى الترخيص بتاريخ {row.ExpiresOn.Value:yyyy-MM-dd}.");
        if (!string.IsNullOrWhiteSpace(row.MachineFingerprint) &&
            !string.Equals(row.MachineFingerprint, GetMachineFingerprint(), StringComparison.OrdinalIgnoreCase))
            return LicenseStatus.Invalid("الترخيص مرتبط بجهاز آخر.");

        var days = row.ExpiresOn.HasValue
            ? Math.Max(0, (row.ExpiresOn.Value.Date - DateTime.Now.Date).Days)
            : int.MaxValue;
        return LicenseStatus.Valid(row.LicenseId, row.LicenseType, row.CompanyName, row.CustomerName,
            row.ExpiresOn, row.MaxUsers, days);
    }

    public async Task<string> CreateLicenseAsync(string licenseKey, string licenseType, string companyName,
        string? customerName, DateTime? expiresOn, int maxUsers, string? machineFingerprint,
        string? notes, int? createdByUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey)) throw new ArgumentException("مفتاح الترخيص مطلوب.", nameof(licenseKey));
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("اسم الشركة مطلوب.", nameof(companyName));
        if (maxUsers < 1) throw new ArgumentOutOfRangeException(nameof(maxUsers));

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeKey(licenseKey);
        const string sql = @"
INSERT INTO dbo.App_Licenses
(LicenseId, LicenseKeyHash, LicenseType, ProductName, CompanyName, CustomerName,
 IssuedOn, ExpiresOn, MaxUsers, MachineFingerprint, IsActive, Notes, CreatedByUserId)
VALUES (@Id,@Hash,@Type,@Product,@Company,@Customer,SYSDATETIME(),@Expires,@MaxUsers,@Machine,@Active,@Notes,@CreatedBy);";

        await _db.ExecuteAsync(sql, p =>
        {
            p.AddWithValue("@Id", Guid.NewGuid());
            p.AddWithValue("@Hash", HashKey(normalized));
            p.AddWithValue("@Type", licenseType ?? "Standard");
            p.AddWithValue("@Product", ProductName);
            p.AddWithValue("@Company", companyName.Trim());
            p.AddWithValue("@Customer", (object?)customerName?.Trim() ?? DBNull.Value);
            p.AddWithValue("@Expires", (object?)expiresOn ?? DBNull.Value);
            p.AddWithValue("@MaxUsers", maxUsers);
            p.AddWithValue("@Machine", (object?)machineFingerprint ?? DBNull.Value);
            p.AddWithValue("@Active", true);
            p.AddWithValue("@Notes", (object?)notes ?? DBNull.Value);
            p.AddWithValue("@CreatedBy", (object?)createdByUserId ?? DBNull.Value);
        }, cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    public async Task<DataTable> GetLicensesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        return await _db.QueryAsync(
            @"SELECT LicenseId, LicenseType AS [النوع], CompanyName AS [الشركة], CustomerName AS [العميل],
                     IssuedOn AS [الإصدار], ExpiresOn AS [الانتهاء], MaxUsers AS [المستخدمون],
                     IsActive AS [نشط], MachineFingerprint AS [الجهاز], Notes AS [ملاحظات]
              FROM dbo.App_Licenses ORDER BY CreatedAt DESC;", cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeactivateAsync(Guid licenseId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await _db.ExecuteAsync("UPDATE dbo.App_Licenses SET IsActive=0 WHERE LicenseId=@Id;",
            p => p.AddWithValue("@Id", licenseId), cancellationToken).ConfigureAwait(false);
    }

    public async Task ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeKey(licenseKey);
        var row = await _db.QuerySingleAsync<LicenseRow>(
            @"SELECT TOP (1) LicenseId, LicenseType, ProductName, CompanyName, CustomerName,
                     IssuedOn, ExpiresOn, MaxUsers, MachineFingerprint, IsActive
              FROM dbo.App_Licenses WHERE LicenseKeyHash=@Hash;",
            p => p.AddWithValue("@Hash", HashKey(normalized)), cancellationToken).ConfigureAwait(false);
        if (row == null) throw new InvalidOperationException("مفتاح الترخيص غير موجود.");
        if (!row.IsActive) throw new InvalidOperationException("الترخيص غير نشط.");
        if (row.ExpiresOn.HasValue && row.ExpiresOn.Value.Date < DateTime.Now.Date) throw new InvalidOperationException("الترخيص منتهي.");
        if (!string.IsNullOrWhiteSpace(row.MachineFingerprint) &&
            !string.Equals(row.MachineFingerprint, GetMachineFingerprint(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("الترخيص مرتبط بجهاز آخر.");
        await SaveLocalLicenseAsync(normalized).ConfigureAwait(false);
    }

    public static string GenerateKey()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        var raw = BitConverter.ToString(bytes).Replace("-", string.Empty).ToUpperInvariant();
        return string.Join("-", Enumerable.Range(0, 4).Select(i => raw.Substring(i * 8, 8)));
    }

    public static string GetMachineFingerprint()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(guid)) return guid.Trim();
        }
        catch { }
        return Environment.MachineName;
    }

    private static string NormalizeKey(string key) => new string(key.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string HashKey(string key)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(NormalizeKey(key))))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string LocalPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), LocalFolder, LocalFile);

    private static Task SaveLocalLicenseAsync(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LocalPath)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(NormalizeKey(key)), null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(LocalPath, bytes);
        return Task.CompletedTask;
    }

    private static Task<string?> ReadLocalLicenseAsync()
    {
        if (!File.Exists(LocalPath)) return Task.FromResult<string?>(null);
        try
        {
            var bytes = File.ReadAllBytes(LocalPath);
            var clear = ProtectedData.Unprotect(bytes, null, DataProtectionScope.LocalMachine);
            return Task.FromResult<string?>(NormalizeKey(Encoding.UTF8.GetString(clear)));
        }
        catch { return Task.FromResult<string?>(null); }
    }

    private sealed class LicenseCount { public int LicenseCount { get; set; } }
    private sealed class LicenseRow
    {
        public Guid LicenseId { get; set; }
        public string LicenseType { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string? CustomerName { get; set; }
        public DateTime IssuedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public int MaxUsers { get; set; }
        public string? MachineFingerprint { get; set; }
        public bool IsActive { get; set; }
    }
}

public sealed class LicenseStatus
{
    private LicenseStatus(bool valid, string message) { IsValid = valid; Message = message; }
    public bool IsValid { get; }
    public string Message { get; }
    public Guid? LicenseId { get; private init; }
    public string LicenseType { get; private init; } = "";
    public string CompanyName { get; private init; } = "";
    public string? CustomerName { get; private init; }
    public DateTime? ExpiresOn { get; private init; }
    public int MaxUsers { get; private init; }
    public int DaysRemaining { get; private init; }
    public static LicenseStatus Invalid(string message) => new(false, message);
    public static LicenseStatus Valid(Guid id, string type, string company, string? customer, DateTime? expires, int maxUsers, int days)
        => new(true, "الترخيص صالح.") { LicenseId = id, LicenseType = type, CompanyName = company, CustomerName = customer, ExpiresOn = expires, MaxUsers = maxUsers, DaysRemaining = days };
}
