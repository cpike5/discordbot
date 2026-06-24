using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Helpers for applying a consistent Polly-based resilience pipeline to outbound
/// <see cref="HttpClient"/> instances (retry with jitter, per-attempt and total timeouts,
/// and a circuit breaker). Centralizes the cross-option timing constraints the standard
/// resilience handler validates so callers only need to specify a per-attempt timeout.
/// </summary>
public static class HttpResilienceExtensions
{
    /// <summary>
    /// Adds the standard resilience handler tuned for the bot's outbound HTTP calls.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="attemptTimeout">Timeout applied to each individual attempt.</param>
    /// <param name="maxRetryAttempts">Number of retries after the first attempt (default 2).</param>
    /// <returns>The HTTP client builder for chaining.</returns>
    /// <remarks>
    /// The standard resilience handler enforces that the total request timeout is at least the
    /// per-attempt timeout and that the circuit breaker sampling duration is at least twice the
    /// per-attempt timeout. We derive consistent values from <paramref name="attemptTimeout"/> so
    /// these invariants always hold and the pipeline never fails validation at startup.
    /// Retries automatically honor a <c>Retry-After</c> header (e.g. HTTP 429), so this is safe to
    /// apply to rate-limited APIs.
    /// </remarks>
    public static IHttpClientBuilder AddBotResilienceHandler(
        this IHttpClientBuilder builder,
        TimeSpan attemptTimeout,
        int maxRetryAttempts = 2)
    {
        // Allow the total budget to cover every attempt plus backoff slack, and keep the
        // circuit-breaker sampling window comfortably above the 2x-attempt minimum.
        var attemptTicks = attemptTimeout.Ticks;
        var samplingDuration = TimeSpan.FromTicks(attemptTicks * 4);
        var totalTimeout = TimeSpan.FromTicks(attemptTicks * (maxRetryAttempts + 2) * 2);
        if (totalTimeout < samplingDuration)
        {
            totalTimeout = samplingDuration;
        }

        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = attemptTimeout;
            options.TotalRequestTimeout.Timeout = totalTimeout;
            options.CircuitBreaker.SamplingDuration = samplingDuration;

            options.Retry.MaxRetryAttempts = maxRetryAttempts;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
        });

        return builder;
    }
}
