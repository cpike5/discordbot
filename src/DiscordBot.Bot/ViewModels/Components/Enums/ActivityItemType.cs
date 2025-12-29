namespace DiscordBot.Bot.ViewModels.Components.Enums;

/// <summary>
/// Represents the type of activity item for visual styling and status indication.
/// </summary>
public enum ActivityItemType
{
    /// <summary>
    /// Successful operation or completion (green indicator).
    /// </summary>
    Success,

    /// <summary>
    /// Informational activity (blue indicator).
    /// </summary>
    Info,

    /// <summary>
    /// Warning or caution (yellow indicator).
    /// </summary>
    Warning,

    /// <summary>
    /// Error or failure (red indicator).
    /// </summary>
    Error
}
