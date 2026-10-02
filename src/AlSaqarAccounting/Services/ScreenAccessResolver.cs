using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Resolves the current user's access for a destination screen before an
/// internal form is opened.
/// </summary>
public sealed class ScreenAccessResolver
{
    private readonly SecurityService _security;

    public ScreenAccessResolver(DbExecutor db)
        => _security = new SecurityService(db);

    public async Task<ScreenAccess?> GetAsync(
        AppSession session,
        string screenName,
        CancellationToken cancellationToken = default)
    {
        var normalized = screenName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var screens = await _security.GetAccessibleScreensAsync(
            session, cancellationToken).ConfigureAwait(false);

        return screens.FirstOrDefault(s =>
            string.Equals(s.ScreenName?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ScreenAccess> RequireAsync(
        AppSession session,
        string screenName,
        CancellationToken cancellationToken = default)
        => await GetAsync(session, screenName, cancellationToken).ConfigureAwait(false)
           ?? throw new UnauthorizedAccessException(
               $"لا تملك صلاحية فتح الشاشة «{screenName}».");
}
