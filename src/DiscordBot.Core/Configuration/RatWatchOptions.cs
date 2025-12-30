namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the Rat Watch background service and feature settings.
/// </summary>
public class RatWatchOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "RatWatch";

    /// <summary>
    /// Gets or sets the interval (in seconds) between checks for due watches and expired voting.
    /// Default is 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of concurrent watch executions.
    /// Default is 5.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout (in seconds) for executing a single watch notification or voting finalization.
    /// Default is 30 seconds.
    /// </summary>
    public int ExecutionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default voting duration (in minutes) for guilds that haven't configured their own.
    /// Default is 5 minutes.
    /// </summary>
    public int DefaultVotingDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default maximum advance hours for scheduling watches.
    /// Default is 24 hours.
    /// </summary>
    public int DefaultMaxAdvanceHours { get; set; } = 24;
}
