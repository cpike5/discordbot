namespace DiscordBot.Core.Enums;

/// <summary>
/// Which moderation events a guild's mod-log channel receives. Stored as an int on
/// <see cref="Entities.GuildModerationConfig.ModLogEvents"/>.
/// </summary>
[Flags]
public enum ModLogEventKinds
{
    /// <summary>Nothing is posted, even when a channel is set.</summary>
    None = 0,

    /// <summary>Every moderation case created, whatever its type (warn, kick, ban, unban, mute, note).</summary>
    Cases = 1,

    /// <summary>An auto-moderation rule flagged an event for review and did not act on its own.</summary>
    FlaggedEvents = 2,

    /// <summary>Auto-moderation acted on its own (deleted a message, muted, kicked or banned).</summary>
    AutoActions = 4,

    /// <summary>The default for a newly configured channel: everything.</summary>
    All = Cases | FlaggedEvents | AutoActions
}
