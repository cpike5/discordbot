namespace DiscordBot.Core.DTOs;

/// <summary>
/// What the mod-log feed shows about a flagged event beyond the event row itself: the message that
/// tripped the rule and, for a join, how new the account is. Carried as plain values so the
/// notifier contract in Core never sees a Discord type.
/// </summary>
/// <param name="MessageContent">The flagged message's text, or null for a join event or when the message is gone.</param>
/// <param name="AccountCreatedAt">When the member's account was created, for a join event.</param>
/// <param name="JoinedAt">When the member joined the server, for a join event.</param>
public sealed record ModLogFlaggedContext(
    string? MessageContent = null,
    DateTimeOffset? AccountCreatedAt = null,
    DateTimeOffset? JoinedAt = null)
{
    /// <summary>No extra context.</summary>
    public static readonly ModLogFlaggedContext None = new();
}
