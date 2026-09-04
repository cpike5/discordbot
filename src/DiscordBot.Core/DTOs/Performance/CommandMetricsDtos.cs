namespace DiscordBot.Core.DTOs;

/// <summary>
/// Aggregated performance metrics for a command.
/// </summary>
public record CommandPerformanceAggregateDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of times this command was executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AvgMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time in milliseconds.
    /// </summary>
    public double MinMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxMs { get; set; }

    /// <summary>
    /// Gets or sets the 50th percentile execution time in milliseconds.
    /// </summary>
    public double P50Ms { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile execution time in milliseconds.
    /// </summary>
    public double P95Ms { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile execution time in milliseconds.
    /// </summary>
    public double P99Ms { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// Details about a slow command execution.
/// </summary>
public record SlowestCommandDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the command was executed (UTC).
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord username who executed the command (resolved from cache).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID where the command was executed.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild name where the command was executed (resolved from cache).
    /// </summary>
    public string? GuildName { get; set; }
}

/// <summary>
/// Command execution throughput over time.
/// </summary>
public record CommandThroughputDto
{
    /// <summary>
    /// Gets or sets the timestamp for this throughput measurement (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of commands executed in this time bucket.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the time granularity (hour, day).
    /// </summary>
    public string Granularity { get; set; } = string.Empty;
}

/// <summary>
/// Error breakdown for a command.
/// </summary>
public record CommandErrorBreakdownDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of errors for this command.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the collection of error messages and their frequency.
    /// </summary>
    public IReadOnlyDictionary<string, int> ErrorMessages { get; set; } = new Dictionary<string, int>();
}

// ============================================================================
// API Tracking DTOs
// ============================================================================

/// <summary>
/// Command error summary with error rate, breakdown, and recent errors.
/// </summary>
public record CommandErrorsDto
{
    /// <summary>
    /// Gets or sets the overall error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets or sets the error breakdown by command.
    /// </summary>
    public IReadOnlyList<CommandErrorBreakdownDto> ByType { get; init; } = Array.Empty<CommandErrorBreakdownDto>();

    /// <summary>
    /// Gets or sets the most recent command errors.
    /// </summary>
    public IReadOnlyList<RecentCommandErrorDto> RecentErrors { get; init; } = Array.Empty<RecentCommandErrorDto>();
}

/// <summary>
/// Details about a recent command error.
/// </summary>
public record RecentCommandErrorDto
{
    /// <summary>
    /// Gets or sets when the error occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the command name that failed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the guild ID where the error occurred.
    /// </summary>
    public ulong? GuildId { get; init; }
}
