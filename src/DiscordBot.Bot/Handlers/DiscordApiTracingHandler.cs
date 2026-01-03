using System.Diagnostics;
using System.Net;
using System.Text.Json;
using DiscordBot.Bot.Tracing;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Delegating handler that enriches HTTP client spans with Discord-specific
/// attributes including rate limit information and retry tracking.
/// </summary>
public class DiscordApiTracingHandler : DelegatingHandler
{
    private readonly ILogger<DiscordApiTracingHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordApiTracingHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DiscordApiTracingHandler(ILogger<DiscordApiTracingHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        // Extract endpoint for tracing (normalize path parameters)
        var endpoint = NormalizeEndpoint(request.RequestUri?.AbsolutePath ?? "unknown");
        var method = request.Method.Method;

        // Set Discord API attributes on the current activity
        activity?.SetTag(TracingConstants.Attributes.DiscordApiEndpoint, endpoint);
        activity?.SetTag(TracingConstants.Attributes.DiscordApiMethod, method);

        try
        {
            var (response, attemptCount) = await SendWithRetryAsync(request, cancellationToken, activity);

            // Set response status
            activity?.SetTag(TracingConstants.Attributes.DiscordApiResponseStatus, (int)response.StatusCode);

            // Parse and attach rate limit headers
            AttachRateLimitAttributes(activity, response);

            // Record final attempt count if retries occurred
            if (attemptCount > 1)
            {
                activity?.SetTag(TracingConstants.Attributes.DiscordApiRetryAttempt, attemptCount);
            }

            // Handle error responses
            if (!response.IsSuccessStatusCode)
            {
                await AttachErrorAttributesAsync(activity, response);
            }

            return response;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Sends the request with automatic retry handling for rate limits.
    /// </summary>
    /// <returns>The response and the number of attempts made.</returns>
    private async Task<(HttpResponseMessage Response, int AttemptCount)> SendWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        Activity? parentActivity)
    {
        const int MaxRetries = 3;
        var attempt = 0;
        HttpResponseMessage response;

        while (true)
        {
            attempt++;
            response = await base.SendAsync(request, cancellationToken);

            // Check for rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt >= MaxRetries)
                {
                    _logger.LogWarning(
                        "Discord API rate limit exceeded after {Attempts} attempts for {Method} {Endpoint}",
                        attempt, request.Method, request.RequestUri?.AbsolutePath);
                    break;
                }

                // Parse retry-after
                var retryAfterMs = GetRetryAfterMs(response);

                // Create retry span
                using var retryActivity = BotActivitySource.Source.StartActivity(
                    TracingConstants.Spans.DiscordApiRetry,
                    ActivityKind.Internal);

                retryActivity?.SetTag(TracingConstants.Attributes.DiscordApiRetryAttempt, attempt);
                retryActivity?.SetTag(TracingConstants.Attributes.DiscordApiRetryBackoffMs, retryAfterMs);

                // Check if global rate limit
                var isGlobal = response.Headers.Contains("X-RateLimit-Global");
                if (isGlobal)
                {
                    parentActivity?.SetTag(TracingConstants.Attributes.DiscordApiRateLimitGlobal, true);
                    _logger.LogWarning(
                        "Global Discord API rate limit hit, backing off for {BackoffMs}ms",
                        retryAfterMs);
                }
                else
                {
                    _logger.LogDebug(
                        "Discord API rate limited, retry {Attempt} after {BackoffMs}ms for {Method} {Endpoint}",
                        attempt, retryAfterMs, request.Method, request.RequestUri?.AbsolutePath);
                }

                await Task.Delay(retryAfterMs, cancellationToken);

                // Clone the request for retry (original is disposed after send)
                request = await CloneRequestAsync(request);
                continue;
            }

            break;
        }

        return (response, attempt);
    }

    /// <summary>
    /// Attaches Discord rate limit header values to the activity.
    /// </summary>
    private void AttachRateLimitAttributes(Activity? activity, HttpResponseMessage response)
    {
        if (activity is null)
            return;

        // X-RateLimit-Limit: Max requests per window
        if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues))
        {
            if (int.TryParse(limitValues.FirstOrDefault(), out var limit))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiRateLimitLimit, limit);
            }
        }

        // X-RateLimit-Remaining: Requests remaining in window
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
        {
            if (int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiRateLimitRemaining, remaining);

                // Log warning if running low on rate limit
                if (remaining <= 2)
                {
                    _logger.LogWarning(
                        "Discord API rate limit nearly exhausted: {Remaining} remaining",
                        remaining);
                }
            }
        }

        // X-RateLimit-Reset: Unix timestamp for reset
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            if (double.TryParse(resetValues.FirstOrDefault(), out var reset))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiRateLimitReset, reset);
            }
        }

        // X-RateLimit-Reset-After: Seconds until reset
        if (response.Headers.TryGetValues("X-RateLimit-Reset-After", out var resetAfterValues))
        {
            if (double.TryParse(resetAfterValues.FirstOrDefault(), out var resetAfter))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiRateLimitResetAfter, resetAfter);
            }
        }

        // X-RateLimit-Bucket: Rate limit bucket ID
        if (response.Headers.TryGetValues("X-RateLimit-Bucket", out var bucketValues))
        {
            var bucket = bucketValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(bucket))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiRateLimitBucket, bucket);
            }
        }
    }

    /// <summary>
    /// Attaches Discord error information to the activity.
    /// </summary>
    private async Task AttachErrorAttributesAsync(Activity? activity, HttpResponseMessage response)
    {
        if (activity is null)
            return;

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
                return;

            // Parse Discord error response
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var codeElement))
            {
                activity.SetTag(TracingConstants.Attributes.DiscordApiErrorCode, codeElement.GetInt32());
            }

            if (root.TryGetProperty("message", out var messageElement))
            {
                var message = messageElement.GetString();
                activity.SetTag(TracingConstants.Attributes.DiscordApiErrorMessage, message);
                activity.SetStatus(ActivityStatusCode.Error, message);
            }
        }
        catch (JsonException)
        {
            // Not a JSON response, ignore
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse Discord error response");
        }
    }

    /// <summary>
    /// Gets the retry-after duration from the response headers.
    /// </summary>
    private static int GetRetryAfterMs(HttpResponseMessage response)
    {
        // Try X-RateLimit-Reset-After first (more precise)
        if (response.Headers.TryGetValues("X-RateLimit-Reset-After", out var resetAfterValues))
        {
            if (double.TryParse(resetAfterValues.FirstOrDefault(), out var seconds))
            {
                return (int)(seconds * 1000);
            }
        }

        // Fall back to Retry-After header
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return (int)delta.TotalMilliseconds;
        }

        // Default backoff
        return 1000;
    }

    /// <summary>
    /// Normalizes a Discord API endpoint path for consistent tracing.
    /// Replaces snowflake IDs with placeholders to reduce cardinality.
    /// </summary>
    private static string NormalizeEndpoint(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "unknown";

        // Remove /api/v{version}/ prefix
        var normalized = path;
        if (normalized.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase))
        {
            var versionEnd = normalized.IndexOf('/', 6);
            if (versionEnd > 0)
            {
                normalized = normalized[versionEnd..];
            }
        }

        // Replace snowflake IDs (17-19 digit numbers) with {id}
        // This reduces metric cardinality significantly
        var parts = normalized.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length >= 17 && parts[i].Length <= 19 && parts[i].All(char.IsDigit))
            {
                parts[i] = "{id}";
            }
        }

        return string.Join("/", parts);
    }

    /// <summary>
    /// Clones an HTTP request message for retry purposes.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Clone headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Clone content if present
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
