namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for <c>DataRetentionService</c>, the background sweep that deletes old
/// rows from high-volume tables that have no retention job of their own: command logs, TTS
/// messages, assistant usage metrics, and audio playback logs. The same sweep also covers user
/// activity events (configured by <see cref="UserActivityEventRetentionOptions"/>) and connection
/// events (configured by <see cref="PerformanceMetricsOptions.ConnectionEventRetentionDays"/>).
/// A per-table retention of zero or less disables that table's sweep only.
/// </summary>
public class DataRetentionOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "DataRetention";

    /// <summary>
    /// Gets or sets whether the data retention sweep runs at all. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval (in hours) between sweeps. Default is 24 hours.
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the delay (in minutes) after startup before the first sweep. Default is 10 minutes.
    /// </summary>
    public int InitialDelayMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of rows deleted per batch. Repositories clamp this to 1000.
    /// Default is 1000.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how many days of command logs to keep. Zero or less disables the sweep for this
    /// table. Default is 90 days. Daily guild metrics are aggregated from the previous day's command
    /// logs, so the longer-lived summaries do not depend on this window.
    /// </summary>
    public int CommandLogRetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets how many days of TTS message history to keep. Zero or less disables the sweep for
    /// this table. Default is 90 days.
    /// </summary>
    public int TtsMessageRetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets how many days of per-guild daily assistant usage metrics to keep. Zero or less
    /// disables the sweep for this table. Default is 365 days: these rows are already one-per-day
    /// aggregates, so they are small and worth keeping for a year of cost history.
    /// </summary>
    public int AssistantUsageMetricsRetentionDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets how many days of audio playback logs (the audio moderation log) to keep. Zero or
    /// less disables the sweep for this table. Default is 90 days.
    /// </summary>
    public int AudioPlaybackLogRetentionDays { get; set; } = 90;
}
