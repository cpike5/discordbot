namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents who initiated a user data purge operation.
/// </summary>
public enum PurgeInitiator
{
    /// <summary>
    /// User requested deletion of their own data via slash command.
    /// </summary>
    User = 1,

    /// <summary>
    /// Administrator initiated purge via admin UI.
    /// </summary>
    Admin = 2,

    /// <summary>
    /// System-initiated purge (e.g., automated compliance process).
    /// </summary>
    System = 3
}
