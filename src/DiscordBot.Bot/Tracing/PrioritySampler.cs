using System.Diagnostics;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Custom OpenTelemetry sampler that makes intelligent sampling decisions based on
/// operation type, error status, and latency to optimize trace collection costs.
/// </summary>
public class PrioritySampler : Sampler
{
    private readonly SamplingOptions _options;
    private readonly ILogger<PrioritySampler> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PrioritySampler"/> class.
    /// </summary>
    /// <param name="options">The sampling configuration options.</param>
    /// <param name="logger">The logger for recording sampling decisions.</param>
    public PrioritySampler(
        IOptions<SamplingOptions> options,
        ILogger<PrioritySampler> logger)
    {
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "PrioritySampler initialized with DefaultRate={DefaultRate}, ErrorRate={ErrorRate}, " +
            "SlowThresholdMs={SlowThresholdMs}, HighPriorityRate={HighPriorityRate}, LowPriorityRate={LowPriorityRate}",
            _options.DefaultRate, _options.ErrorRate, _options.SlowThresholdMs,
            _options.HighPriorityRate, _options.LowPriorityRate);
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var spanName = samplingParameters.Name;
        var spanKind = samplingParameters.Kind;
        var parentContext = samplingParameters.ParentContext;
        var tags = samplingParameters.Tags;

        // If parent was sampled, sample this span too (maintain trace continuity)
        if (parentContext.TraceId != default && parentContext.IsRemote)
        {
            var parentSampled = (parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0;
            if (parentSampled)
            {
                _logger.LogDebug("Sampling span '{SpanName}': parent was sampled", spanName);
                return new SamplingResult(SamplingDecision.RecordAndSample);
            }
        }

        // Determine priority and sampling rate based on span characteristics
        var samplingRate = DetermineSamplingRate(spanName, tags);

        // Make probabilistic decision
        var shouldSample = _random.NextDouble() < samplingRate;

        if (shouldSample)
        {
            _logger.LogDebug(
                "Sampling span '{SpanName}' with rate {SamplingRate:P}",
                spanName, samplingRate);
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
        else
        {
            _logger.LogTrace(
                "Dropping span '{SpanName}' with rate {SamplingRate:P}",
                spanName, samplingRate);
            return new SamplingResult(SamplingDecision.Drop);
        }
    }

    /// <summary>
    /// Determines the appropriate sampling rate based on span name and attributes.
    /// </summary>
    /// <param name="spanName">The name of the span being sampled.</param>
    /// <param name="tags">The span's initial tags/attributes.</param>
    /// <returns>The sampling rate to apply (0.0 to 1.0).</returns>
    private double DetermineSamplingRate(string spanName, IEnumerable<KeyValuePair<string, object?>>? tags)
    {
        // Convert tags to dictionary for easier lookup
        var attributes = tags?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>();

        // Always sample (100%) - Critical operations and error conditions
        if (IsAlwaysSampleOperation(spanName, attributes))
        {
            return 1.0;
        }

        // High priority sampling (50% by default) - Important business operations
        if (IsHighPriorityOperation(spanName, attributes))
        {
            return _options.HighPriorityRate;
        }

        // Low priority sampling (1% by default) - Health checks and high-frequency operations
        if (IsLowPriorityOperation(spanName, attributes))
        {
            return _options.LowPriorityRate;
        }

        // Default sampling (10% in production, 100% in dev)
        return _options.DefaultRate;
    }

    /// <summary>
    /// Determines if an operation should always be sampled (100%).
    /// </summary>
    /// <param name="spanName">The span name.</param>
    /// <param name="attributes">The span attributes.</param>
    /// <returns>True if the operation should always be sampled.</returns>
    private static bool IsAlwaysSampleOperation(string spanName, Dictionary<string, string> attributes)
    {
        // Discord API rate limit hits - critical for debugging rate limit issues
        if (attributes.TryGetValue(TracingConstants.Attributes.DiscordApiRateLimitRemaining, out var remaining) &&
            int.TryParse(remaining, out var remainingValue) && remainingValue == 0)
        {
            return true;
        }

        // Discord API errors
        if (attributes.ContainsKey(TracingConstants.Attributes.DiscordApiErrorCode) ||
            attributes.ContainsKey(TracingConstants.Attributes.DiscordApiErrorMessage))
        {
            return true;
        }

        // Auto-moderation detections - critical security events
        if (spanName.Contains("automod", StringComparison.OrdinalIgnoreCase) ||
            spanName.StartsWith(TracingConstants.Spans.DiscordEventAutoModSpamDetected, StringComparison.Ordinal) ||
            spanName.StartsWith(TracingConstants.Spans.DiscordEventAutoModRaidDetected, StringComparison.Ordinal) ||
            spanName.StartsWith(TracingConstants.Spans.DiscordEventAutoModContentFiltered, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an operation is high priority for sampling.
    /// </summary>
    /// <param name="spanName">The span name.</param>
    /// <param name="attributes">The span attributes.</param>
    /// <returns>True if the operation is high priority.</returns>
    private static bool IsHighPriorityOperation(string spanName, Dictionary<string, string> attributes)
    {
        // New user joins (welcome flow)
        if (spanName.StartsWith(TracingConstants.Spans.DiscordEventMemberJoined, StringComparison.Ordinal) ||
            spanName.StartsWith(TracingConstants.Spans.ServiceWelcomeSend, StringComparison.Ordinal) ||
            attributes.ContainsKey(TracingConstants.Attributes.WelcomeChannelId))
        {
            return true;
        }

        // Moderation actions
        if (spanName.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("mod.", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("/warn", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("/kick", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("/ban", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("/mute", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Rat Watch operations
        if (spanName.Contains("ratwatch", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("rat-", StringComparison.OrdinalIgnoreCase) ||
            spanName.Contains("rat_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Scheduled message executions
        if (spanName.Contains("scheduled", StringComparison.OrdinalIgnoreCase) &&
            spanName.Contains("message", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an operation is low priority for sampling.
    /// </summary>
    /// <param name="spanName">The span name.</param>
    /// <param name="attributes">The span attributes.</param>
    /// <returns>True if the operation is low priority.</returns>
    private static bool IsLowPriorityOperation(string spanName, Dictionary<string, string> attributes)
    {
        // Health check requests
        if (spanName.Contains("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Metrics scraping endpoints
        if (spanName.Contains("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // High-frequency caching operations
        if (spanName.Contains("cache", StringComparison.OrdinalIgnoreCase) &&
            (spanName.Contains("get", StringComparison.OrdinalIgnoreCase) ||
             spanName.Contains("set", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Background service execution cycles - high frequency, sample at low rate
        if (spanName.StartsWith("background.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
