using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Permanent redirects for Razor Pages retired in the Phase 0 UI cleanup
/// (docs/plans/blazor-port-plan.md), so old bookmarks and shared links keep
/// working after the legacy pages were deleted in favor of unified dashboards.
/// </summary>
public static class LegacyRedirectExtensions
{
    /// <summary>
    /// Maps the five legacy single-tab Performance pages to the unified
    /// /Admin/Performance dashboard with the matching tab selected via URL hash
    /// (the scheme wwwroot/js/performance/dashboard.js reads on page load), and
    /// the two Admin/AuditLogs and Admin/MessageLogs stub pages to the unified
    /// /Admin/Logs page, preserving any existing query string.
    /// </summary>
    /// <param name="app">The endpoint route builder to map redirects on.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapLegacyRouteRedirects(this IEndpointRouteBuilder app)
    {
        MapPerformanceTabRedirect(app, "/Admin/Performance/SystemHealth", "system");
        MapPerformanceTabRedirect(app, "/Admin/Performance/HealthMetrics", "health");
        MapPerformanceTabRedirect(app, "/Admin/Performance/Commands", "commands");
        MapPerformanceTabRedirect(app, "/Admin/Performance/ApiMetrics", "api");
        MapPerformanceTabRedirect(app, "/Admin/Performance/Alerts", "alerts");

        app.MapGet("/Admin/AuditLogs", (HttpContext context) =>
                Results.Redirect(BuildLogsTabUrl("audit", context.Request.QueryString), permanent: true))
            .RequireAuthorization("RequireAdmin");

        app.MapGet("/Admin/MessageLogs", (HttpContext context) =>
                Results.Redirect(BuildLogsTabUrl("messages", context.Request.QueryString), permanent: true))
            .RequireAuthorization("RequireAdmin");

        return app;
    }

    private static void MapPerformanceTabRedirect(IEndpointRouteBuilder app, string oldRoute, string tabId)
    {
        app.MapGet(oldRoute, (HttpContext context) =>
                Results.Redirect(BuildPerformanceTabUrl(tabId, context.Request.QueryString), permanent: true))
            .RequireAuthorization("RequireViewer");
    }

    /// <summary>
    /// Builds the unified Performance dashboard URL with the given tab selected,
    /// preserving any existing query string (e.g. <c>?hours=720</c>) from the old
    /// bookmarked URL.
    /// </summary>
    internal static string BuildPerformanceTabUrl(string tabId, QueryString existingQuery = default)
        => $"/Admin/Performance{existingQuery.Value}#{tabId}";

    /// <summary>
    /// Builds the unified Logs page URL for the given tab, preserving any query
    /// string carried over from the old bookmarked URL and appending the tab
    /// selector the unified page expects. <c>handler</c> is dropped - the old
    /// <c>?handler=Export</c> scheme bound different parameter names on the
    /// legacy stub pages than the unified page's export handler does, so
    /// forwarding it would silently produce an unfiltered export - and any
    /// pre-existing <c>tab</c> is dropped too, so the tab added here is never
    /// duplicated.
    /// </summary>
    internal static string BuildLogsTabUrl(string tab, QueryString existingQuery)
    {
        var parameters = new List<KeyValuePair<string, string?>>();

        if (existingQuery.HasValue)
        {
            foreach (var pair in QueryHelpers.ParseQuery(existingQuery.Value))
            {
                if (string.Equals(pair.Key, "handler", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "tab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var value in pair.Value)
                {
                    parameters.Add(new KeyValuePair<string, string?>(pair.Key, value));
                }
            }
        }

        parameters.Add(new KeyValuePair<string, string?>("tab", tab));

        return $"/Admin/Logs{QueryString.Create(parameters).ToUriComponent()}";
    }
}
