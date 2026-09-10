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
}
