using DiscordBot.Core.Configuration;
using Elastic.Apm.Api;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Elastic APM transaction filter that implements priority-based sampling
/// similar to the <see cref="PrioritySampler"/> for OpenTelemetry.
/// </summary>
/// <remarks>
/// This filter replicates the PrioritySampler logic to ensure consistent sampling
/// behavior across both OpenTelemetry and Elastic APM during the dual-write transition period.
/// </remarks>
public class ElasticApmTransactionFilter
{
    private readonly SamplingOptions _options;
    private readonly ILogger<ElasticApmTransactionFilter> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticApmTransactionFilter"/> class.
    /// </summary>
    /// <param name="options">The sampling configuration options.</param>
    /// <param name="logger">The logger for recording sampling decisions.</param>
    public ElasticApmTransactionFilter(
        IOptions<SamplingOptions> options,
        ILogger<ElasticApmTransactionFilter> logger)
    {
        _options = options.Value;
        _logger = logger;

        _logger.LogDebug(
            "ElasticApmTransactionFilter initialized with DefaultRate={DefaultRate}, " +
            "HighPriorityRate={HighPriorityRate}, LowPriorityRate={LowPriorityRate}",
            _options.DefaultRate, _options.HighPriorityRate, _options.LowPriorityRate);
    }

    /// <summary>
    /// Filters transactions based on priority-based sampling rules.
    /// </summary>
    /// <param name="transaction">The APM transaction to filter.</param>
    /// <returns>The transaction if sampled, or null to drop it.</returns>
    public ITransaction? Filter(ITransaction transaction)
    {
        // Determine if this transaction should be sampled based on priority rules
        var samplingRate = DetermineSamplingRate(transaction);

        if (_random.NextDouble() > samplingRate)
        {
            // Mark transaction as not sampled (will still be counted but not stored)
            _logger.LogTrace(
                "Dropping APM transaction '{Name}' with rate {SamplingRate:P}",
                transaction.Name, samplingRate);
            return null; // Returning null drops the transaction
        }

        // Add sampling rate as label for observability
        transaction.SetLabel("sampling.rate", samplingRate.ToString("F2"));
        transaction.SetLabel("sampling.decision", "sampled");

        _logger.LogTrace(
            "Sampling APM transaction '{Name}' with rate {SamplingRate:P}",
            transaction.Name, samplingRate);

        return transaction;
    }

    /// <summary>
    /// Determines the appropriate sampling rate based on transaction characteristics.
    /// </summary>
    /// <param name="transaction">The transaction being evaluated.</param>
    /// <returns>The sampling rate to apply (0.0 to 1.0).</returns>
    private double DetermineSamplingRate(ITransaction transaction)
    {
        var name = transaction.Name ?? string.Empty;
        var type = transaction.Type ?? string.Empty;

        // Always sample (100%) - Critical operations and error conditions
        if (IsAlwaysSampleOperation(name, transaction))
        {
            return 1.0;
        }

        // High priority sampling (50% by default) - Important business operations
        if (IsHighPriorityOperation(name, type, transaction))
        {
            return _options.HighPriorityRate;
        }

        // Low priority sampling (1% by default) - Health checks and high-frequency operations
        if (IsLowPriorityOperation(name, type))
        {
            return _options.LowPriorityRate;
        }

        // Default sampling (10% in production, 100% in dev)
        return _options.DefaultRate;
    }

    /// <summary>
    /// Determines if an operation should always be sampled (100%).
    /// </summary>
    /// <param name="name">The transaction name.</param>
    /// <param name="transaction">The transaction to evaluate.</param>
    /// <returns>True if the operation should always be sampled.</returns>
    private static bool IsAlwaysSampleOperation(string name, ITransaction transaction)
    {
        // Always sample transactions with failures (100% error rate)
        // This ensures all exceptions are captured in APM for diagnostics
        if (transaction.Outcome == Outcome.Failure)
        {
            return true;
        }

        // Note: Using Labels dictionary is deprecated but necessary for reading labels.
        // The SetLabel method only supports writing, not reading.
#pragma warning disable CS0618 // Type or member is obsolete
        // Discord API rate limit hits - critical for debugging rate limit issues
        if (transaction.Labels.TryGetValue(TracingConstants.Attributes.DiscordApiRateLimitRemaining, out var remaining) &&
            int.TryParse(remaining, out var remainingValue) && remainingValue == 0)
        {
            return true;
        }

        // Discord API errors
        if (transaction.Labels.ContainsKey(TracingConstants.Attributes.DiscordApiErrorCode) ||
            transaction.Labels.ContainsKey(TracingConstants.Attributes.DiscordApiErrorMessage))
        {
            return true;
        }
#pragma warning restore CS0618 // Type or member is obsolete

        // Auto-moderation detections - critical security events
        if (name.Contains("automod", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(TracingConstants.Spans.DiscordEventAutoModSpamDetected, StringComparison.Ordinal) ||
            name.StartsWith(TracingConstants.Spans.DiscordEventAutoModRaidDetected, StringComparison.Ordinal) ||
            name.StartsWith(TracingConstants.Spans.DiscordEventAutoModContentFiltered, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an operation is high priority for sampling.
    /// </summary>
    /// <param name="name">The transaction name.</param>
    /// <param name="type">The transaction type.</param>
    /// <param name="transaction">The transaction to evaluate.</param>
    /// <returns>True if the operation is high priority.</returns>
    private static bool IsHighPriorityOperation(string name, string type, ITransaction transaction)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        // New user joins (welcome flow)
        if (name.StartsWith(TracingConstants.Spans.DiscordEventMemberJoined, StringComparison.Ordinal) ||
            name.StartsWith(TracingConstants.Spans.ServiceWelcomeSend, StringComparison.Ordinal) ||
            name.Contains("member.joined", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("welcome", StringComparison.OrdinalIgnoreCase) ||
            transaction.Labels.ContainsKey(TracingConstants.Attributes.WelcomeChannelId))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            return true;
        }

        // Moderation actions
        if (name.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("mod.", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("/warn", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("/kick", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("/ban", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("/mute", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Rat Watch operations
        if (name.Contains("ratwatch", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("rat-", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("rat_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Scheduled message executions
        if (name.Contains("scheduled", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("message", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an operation is low priority for sampling.
    /// </summary>
    /// <param name="name">The transaction name.</param>
    /// <param name="type">The transaction type.</param>
    /// <returns>True if the operation is low priority.</returns>
    private static bool IsLowPriorityOperation(string name, string type)
    {
        // Health check requests
        if (name.Contains("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Metrics scraping endpoints
        if (name.Contains("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // High-frequency caching operations
        if (name.Contains("cache", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("get", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("set", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Background service execution cycles - high frequency, sample at low rate
        if (type == "background" || name.StartsWith("background.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
