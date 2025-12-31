namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the review status of a flagged moderation event.
/// </summary>
public enum FlaggedEventStatus
{
    /// <summary>
    /// Event is pending moderator review.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Event was reviewed and dismissed as a false positive.
    /// </summary>
    Dismissed = 1,

    /// <summary>
    /// Event was acknowledged by a moderator but no action taken.
    /// </summary>
    Acknowledged = 2,

    /// <summary>
    /// Moderation action was taken in response to this event.
    /// </summary>
    Actioned = 3
}
