namespace DiscordBot.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for database operations and performance monitoring.
/// </summary>
/// <remarks>
/// These settings control query performance logging behavior including slow query thresholds
/// and parameter logging options. Configured via the "Database" section in appsettings.json.
/// </remarks>
public class DatabaseSettings
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Gets or sets the threshold in milliseconds for identifying slow queries.
    /// Queries exceeding this threshold will be logged at Warning level.
    /// Default: 100ms.
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether query parameters should be logged.
    /// When enabled, parameters are sanitized to mask sensitive values.
    /// Default: true.
    /// </summary>
    public bool LogQueryParameters { get; set; } = true;
}
