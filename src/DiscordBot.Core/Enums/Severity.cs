namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the severity level of a flagged moderation event.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Low severity - minor flag, informational only.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - notable pattern worth reviewing.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - significant concern requiring attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - immediate attention required.
    /// </summary>
    Critical = 3
}
