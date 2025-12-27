namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for scheduled message execution and background processing.
/// </summary>
public class ScheduledMessagesOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "ScheduledMessages";

    /// <summary>
    /// Gets or sets the interval (in seconds) between scheduled message checks.
    /// The background service will check for due messages at this interval.
    /// Default is 60 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of scheduled messages to execute concurrently.
    /// Used to prevent resource exhaustion when many messages are due simultaneously.
    /// Default is 5.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout (in seconds) for individual message execution.
    /// If a message execution takes longer than this, it will be cancelled.
    /// Default is 30 seconds.
    /// </summary>
    public int ExecutionTimeoutSeconds { get; set; } = 30;
}
