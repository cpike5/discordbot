namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the moderation system.
/// </summary>
public class ModerationOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Moderation";

    /// <summary>
    /// Default duration in days for temporary bans.
    /// Default: 7 days
    /// </summary>
    public int DefaultTempBanDurationDays { get; set; } = 7;

    /// <summary>
    /// Maximum number of messages that can be purged in a single operation.
    /// Default: 100
    /// </summary>
    public int MaxPurgeMessages { get; set; } = 100;

    /// <summary>
    /// Number of moderation cases to display per page in case history.
    /// Default: 10
    /// </summary>
    public int CaseHistoryPageSize { get; set; } = 10;

    /// <summary>
    /// Whether to log moderation actions to the audit log system.
    /// Default: true
    /// </summary>
    public bool LogActionsToAudit { get; set; } = true;
}
