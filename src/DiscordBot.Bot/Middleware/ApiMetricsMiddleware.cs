using System.Diagnostics;
using System.Text.RegularExpressions;
using DiscordBot.Bot.Metrics;

namespace DiscordBot.Bot.Middleware;

/// <summary>
/// Middleware that records API request metrics for Discord bot-specific endpoints.
/// Complements ASP.NET Core built-in HTTP metrics with custom measurements.
/// </summary>
public partial class ApiMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiMetrics _metrics;
    private readonly ILogger<ApiMetricsMiddleware> _logger;

    public ApiMetricsMiddleware(
        RequestDelegate next,
        ApiMetrics metrics,
        ILogger<ApiMetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only track API endpoints (skip static files, metrics endpoint, etc.)
        var path = context.Request.Path.Value ?? "";
        if (!ShouldTrackRequest(path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementActiveRequests();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _metrics.DecrementActiveRequests();

            var endpoint = NormalizeEndpoint(path);
            _metrics.RecordRequest(
                endpoint: endpoint,
                method: context.Request.Method,
                statusCode: context.Response.StatusCode,
                durationMs: stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogTrace(
                "API request completed: {Method} {Endpoint} -> {StatusCode} in {DurationMs}ms",
                context.Request.Method,
                endpoint,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static bool ShouldTrackRequest(string path)
    {
        // Track API endpoints and key Razor pages
        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase)
            || path == "/" || path == "/Index";
    }

    /// <summary>
    /// Normalizes endpoint paths to prevent cardinality explosion from IDs.
    /// Replaces GUIDs, Discord snowflakes, and other numeric IDs with placeholders.
    /// </summary>
    /// <param name="path">The original request path.</param>
    /// <returns>The normalized path with ID placeholders.</returns>
    private static string NormalizeEndpoint(string path)
    {
        // Replace GUIDs
        var normalized = GuidRegex().Replace(path, "{id}");

        // Replace numeric IDs (Discord snowflakes - typically 15-20 digits)
        normalized = SnowflakeRegex().Replace(normalized, "/{id}");

        // Replace shorter numeric IDs
        normalized = NumericIdRegex().Replace(normalized, "/{id}");

        return normalized;
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"/\d{15,20}(?=/|$)")]
    private static partial Regex SnowflakeRegex();

    [GeneratedRegex(@"/\d+(?=/|$)")]
    private static partial Regex NumericIdRegex();
}

/// <summary>
/// Extension methods for registering API metrics middleware.
/// </summary>
public static class ApiMetricsMiddlewareExtensions
{
    /// <summary>
    /// Adds the API metrics middleware to the application pipeline.
    /// Should be registered early in the pipeline to track all requests.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiMetrics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiMetricsMiddleware>();
    }
}
