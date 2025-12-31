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

    // Member Sync Service

    /// <summary>
    /// Gets or sets the initial delay (in minutes) before the first member sync.
    /// Default is 2 minutes (allow bot to fully connect).
    /// </summary>
    public int MemberSyncInitialDelayMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets the interval (in hours) between full reconciliation syncs.
    /// Default is 24 hours.
    /// </summary>
    public int MemberSyncReconciliationIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the batch size for database upserts.
    /// Default is 500 members per transaction.
    /// </summary>
    public int MemberSyncBatchSize { get; set; } = 500;

    /// <summary>
    /// Gets or sets the delay (in milliseconds) between Discord API requests.
    /// Used to respect rate limits (10 requests per 10 seconds = 1 per second).
    /// Default is 1100ms (slightly over 1 second for safety).
    /// </summary>
    public int MemberSyncApiDelayMs { get; set; } = 1100;

    /// <summary>
    /// Gets or sets whether member sync is enabled.
    /// Default is true.
    /// </summary>
    public bool MemberSyncEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum retry attempts for failed API calls.
    /// Default is 3.
    /// </summary>
    public int MemberSyncMaxRetries { get; set; } = 3;
}
