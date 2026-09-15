namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Validates that a URL taken from user-controlled input (most commonly a
/// <c>[SupplyParameterFromQuery]</c> <c>returnUrl</c>) is a same-origin relative path before it is
/// ever rendered into an <c>href</c> or passed to <c>NavigationManager.NavigateTo</c>. Unvalidated,
/// such a value lets an attacker craft a link whose <c>returnUrl</c> is a <c>javascript:</c> URI (XSS
/// on click) or an absolute <c>https://evil.example</c>/protocol-relative <c>//evil.example</c> URL
/// (open redirect) - see the Phase 4 cluster 4a review finding on
/// <c>Blazor/Pages/Admin/AuditLogs/Details.razor.cs</c>. Reusable by every later cluster that
/// echoes a return/redirect URL back into markup.
/// </summary>
public static class LocalUrl
{
    /// <summary>
    /// True only for a same-origin, path-relative URL: starts with a single <c>/</c> (not
    /// <c>//host/...</c>, which the browser treats as protocol-relative to another host, and not
    /// <c>/\host/...</c>, which some browsers normalize into <c>//host/...</c>), and is
    /// well-formed as a relative URI. Rejects everything else, including <c>javascript:</c> and
    /// other schemed URIs, absolute URLs, empty/whitespace, and <see langword="null"/>.
    /// </summary>
    public static bool IsLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/\\", StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.IsWellFormedUriString(value, UriKind.Relative);
    }
}
