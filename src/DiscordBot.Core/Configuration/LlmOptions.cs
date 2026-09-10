namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the LLM model catalog (binds under "Llm").
/// </summary>
public class LlmOptions
{
    /// <summary>The configuration section name for binding.</summary>
    public const string SectionName = "Llm";

    /// <summary>
    /// Gets or sets how often <c>LlmCatalogRefreshService</c> refreshes the model catalog from
    /// OpenRouter, in hours. Default is 24. A value of 0 disables the background refresh entirely
    /// (an admin can still refresh manually from the portal in a later PR).
    /// </summary>
    public int CatalogRefreshHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets how long <c>LlmCatalogRefreshService</c> waits after startup before its first
    /// refresh attempt, in minutes. Default is 5 - mirrors the other background services' startup
    /// stagger (see <c>BackgroundServicesOptions</c>) so every scheduled job doesn't hit the database
    /// and OpenRouter in the same instant.
    /// </summary>
    public int CatalogRefreshInitialDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the capacity of the bounded channel <c>LlmUsageRecorder</c> enqueues onto
    /// before <c>LlmUsageRecordProcessor</c> drains it. Default is 10,000, matching
    /// <c>AuditLogQueue</c>. The channel drops the oldest entry (and logs a warning) rather than
    /// blocking the caller when full.
    /// </summary>
    public int UsageQueueCapacity { get; set; } = 10000;

    /// <summary>
    /// Gets or sets how often <c>AssistantInteractionLogRetentionService</c> sweeps assistant
    /// interaction logs (guild and DM) and the LLM usage ledger for retention, in hours. Default
    /// is 24. A value of 0 disables the sweep entirely.
    /// </summary>
    public int RetentionSweepIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the batch size <c>AssistantInteractionLogRetentionService</c> uses when
    /// deleting expired <c>LlmUsageRecord</c> rows. Default is 1000, matching the other
    /// retention services' batch sizes.
    /// </summary>
    public int RetentionBatchSize { get; set; } = 1000;
}
