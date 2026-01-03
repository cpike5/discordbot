namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for OpenTelemetry distributed tracing sampling strategy.
/// Controls which spans are sampled for export to observability backends.
/// </summary>
public class SamplingOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Sampling";

    /// <summary>
    /// Gets or sets the default sampling rate for normal operations.
    /// Value must be between 0.0 (never sample) and 1.0 (always sample).
    /// Default is 0.1 (10%) for production, should be 1.0 (100%) for development.
    /// </summary>
    public double DefaultRate { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the sampling rate for operations that result in errors.
    /// Value must be between 0.0 (never sample) and 1.0 (always sample).
    /// Default is 1.0 (100%) - always sample errors for diagnostics.
    /// </summary>
    public double ErrorRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the threshold (in milliseconds) that defines a slow operation.
    /// Operations exceeding this duration are sampled at ErrorRate.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int SlowThresholdMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the sampling rate for high-priority operations.
    /// High-priority operations include: new user joins (welcome flow), moderation actions,
    /// Rat Watch verdicts, and scheduled message executions.
    /// Value must be between 0.0 (never sample) and 1.0 (always sample).
    /// Default is 0.5 (50%).
    /// </summary>
    public double HighPriorityRate { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the sampling rate for low-priority operations.
    /// Low-priority operations include: health checks, metrics scraping endpoints,
    /// and high-frequency caching operations.
    /// Value must be between 0.0 (never sample) and 1.0 (always sample).
    /// Default is 0.01 (1%).
    /// </summary>
    public double LowPriorityRate { get; set; } = 0.01;
}
