namespace DiscordBot.Bot.Middleware;

/// <summary>
/// Extension methods for registering <see cref="CorrelationIdMiddleware"/> in the application pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// This middleware should be registered early in the pipeline, ideally before logging middleware
    /// (such as Serilog request logging) to ensure the correlation ID is available for all subsequent
    /// middleware and logging operations.
    /// </remarks>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
