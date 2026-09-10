using Microsoft.AspNetCore.Http;

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
        app.MapGet(oldRoute, () => Results.Redirect(BuildPerformanceTabUrl(tabId), permanent: true))
            .RequireAuthorization("RequireViewer");
    }

    /// <summary>
    /// Builds the unified Performance dashboard URL with the given tab selected.
    /// </summary>
    internal static string BuildPerformanceTabUrl(string tabId) => $"/Admin/Performance#{tabId}";

    /// <summary>
    /// Builds the unified Logs page URL for the given tab, preserving any query
    /// string carried over from the old bookmarked URL and appending the tab
    /// selector the unified page expects.
    /// </summary>
    internal static string BuildLogsTabUrl(string tab, QueryString existingQuery)
    {
        return existingQuery.HasValue
            ? $"/Admin/Logs{existingQuery.Value}&tab={tab}"
            : $"/Admin/Logs?tab={tab}";
    }
}
