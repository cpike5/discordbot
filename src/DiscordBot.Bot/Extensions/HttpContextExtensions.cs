using DiscordBot.Bot.Middleware;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for <see cref="HttpContext"/> to simplify access to request context data.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Retrieves the correlation ID for the current request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>
    /// The correlation ID if available in HttpContext.Items; otherwise, falls back to HttpContext.TraceIdentifier.
    /// </returns>
    /// <remarks>
    /// This method should be used in controllers and services to retrieve the correlation ID for logging
    /// and error responses. The correlation ID is set by <see cref="CorrelationIdMiddleware"/> and stored
    /// in HttpContext.Items for the duration of the request.
    /// </remarks>
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId)
            && correlationId is string id)
        {
            return id;
        }

        // Fallback to TraceIdentifier if correlation ID is not set
        return context.TraceIdentifier;
    }
}
