using Serilog.Context;

namespace DiscordBot.Bot.Middleware;

/// <summary>
/// Middleware that extracts or generates correlation IDs for API requests and propagates them through logs and response headers.
/// </summary>
/// <remarks>
/// The middleware performs the following operations:
/// <list type="number">
/// <item>Extracts the X-Correlation-ID header from the incoming request, or generates a new 16-character hex ID if not present</item>
/// <item>Stores the correlation ID in HttpContext.Items for access by other middleware and controllers</item>
/// <item>Adds the X-Correlation-ID header to the response</item>
/// <item>Pushes the correlation ID to Serilog's LogContext for structured logging</item>
/// </list>
/// This enables end-to-end request tracing across the application and facilitates troubleshooting.
/// </remarks>
public class CorrelationIdMiddleware
{
    /// <summary>
    /// The header name used for correlation ID in requests and responses.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// The key used to store the correlation ID in HttpContext.Items.
    /// </summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request and manages correlation ID extraction, generation, and propagation.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract correlation ID from request headers or generate a new one
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = GenerateCorrelationId();
            _logger.LogTrace("Generated new correlation ID: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogTrace("Using existing correlation ID from request: {CorrelationId}", correlationId);
        }

        // Store correlation ID in HttpContext.Items for access by controllers and other middleware
        context.Items[ItemKey] = correlationId;

        // Add correlation ID to response headers
        context.Response.Headers[HeaderName] = correlationId;

        // Push correlation ID to Serilog LogContext for structured logging
        // The using statement ensures the property is disposed after the request completes
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Generates a new correlation ID as a 16-character hexadecimal string.
    /// </summary>
    /// <returns>A new correlation ID.</returns>
    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..16]; // 16-char hex string
    }
}
