namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for background services and scheduled tasks.
/// </summary>
public class BackgroundServicesOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "BackgroundServices";

    // Token Refresh Service

    /// <summary>
    /// Gets or sets the interval (in minutes) between Discord token refresh checks.
    /// Default is 30 minutes.
    /// </summary>
    public int TokenRefreshIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the threshold (in hours) before token expiration to trigger a refresh.
    /// Tokens expiring within this window will be refreshed proactively.
    /// Default is 1 hour.
    /// </summary>
    public int TokenExpirationThresholdHours { get; set; } = 1;

    /// <summary>
    /// Gets or sets the delay (in seconds) between individual token refresh operations.
    /// Used to rate-limit API calls when refreshing multiple tokens.
    /// Default is 1 second.
    /// </summary>
    public int TokenRefreshDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the initial delay (in minutes) before the first token refresh check.
    /// Default is 1 minute.
    /// </summary>
    public int TokenRefreshInitialDelayMinutes { get; set; } = 1;

    /// <summary>
    /// Gets or sets the threshold (in minutes) for on-demand token refresh.
    /// Tokens expiring within this window will be refreshed immediately when accessed.
    /// Default is 5 minutes.
    /// </summary>
    public int OnDemandRefreshThresholdMinutes { get; set; } = 5;

    // Verification Cleanup Service

    /// <summary>
    /// Gets or sets the interval (in minutes) between verification code cleanup operations.
    /// Default is 5 minutes.
    /// </summary>
    public int VerificationCleanupIntervalMinutes { get; set; } = 5;

    // Interaction State Cleanup Service

    /// <summary>
    /// Gets or sets the interval (in minutes) between interaction state cleanup operations.
    /// Default is 1 minute.
    /// </summary>
    public int InteractionStateCleanupIntervalMinutes { get; set; } = 1;

    // Metrics Services

    /// <summary>
    /// Gets or sets the interval (in seconds) between real-time metrics updates.
    /// Used for bot status, uptime, and performance metrics.
    /// Default is 30 seconds.
    /// </summary>
    public int MetricsUpdateIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the interval (in minutes) between business metrics calculations.
    /// Used for command usage statistics and aggregate metrics.
    /// Default is 5 minutes.
    /// </summary>
    public int BusinessMetricsUpdateIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial delay (in seconds) before the first business metrics update.
    /// Default is 30 seconds.
    /// </summary>
    public int BusinessMetricsInitialDelaySeconds { get; set; } = 30;

    // Message Log Cleanup Service

    /// <summary>
    /// Gets or sets the initial delay (in minutes) before the first message log cleanup operation.
    /// Default is 5 minutes.
    /// </summary>
    public int MessageLogCleanupInitialDelayMinutes { get; set; } = 5;

    // Audit Log Cleanup Service

    /// <summary>
    /// Gets or sets the initial delay (in minutes) before the first audit log cleanup operation.
    /// Default is 5 minutes.
    /// </summary>
    public int AuditLogCleanupInitialDelayMinutes { get; set; } = 5;
}
