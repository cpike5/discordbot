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

    /// <summary>
    /// Gets or sets the database provider to use. Valid values: "Sqlite", "PostgreSql".
    /// When null, the provider is auto-detected from the connection string.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Determines whether the configured provider is PostgreSQL, using the explicit
    /// <see cref="Provider"/> setting first, then falling back to connection string heuristics.
    /// </summary>
    /// <param name="connectionString">The connection string to inspect when Provider is not set.</param>
    /// <returns><c>true</c> if the provider is PostgreSQL; <c>false</c> for SQLite.</returns>
    public bool IsPostgreSql(string connectionString)
    {
        if (!string.IsNullOrEmpty(Provider))
            return Provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase);

        return connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the display name of the active database provider ("PostgreSQL" or "SQLite").
    /// </summary>
    /// <param name="connectionString">The connection string to inspect when Provider is not set.</param>
    public string GetProviderDisplayName(string connectionString)
        => IsPostgreSql(connectionString) ? "PostgreSQL" : "SQLite";
}
