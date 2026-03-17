namespace DiscordBot.Core.Enums;

/// <summary>
/// Channel types for display purposes in the admin UI.
/// </summary>
public enum ChannelDisplayType
{
    /// <summary>Regular text channel.</summary>
    Text,

    /// <summary>Voice channel (with text chat capability).</summary>
    Voice,

    /// <summary>Announcement/news channel.</summary>
    Announcement,

    /// <summary>Stage channel.</summary>
    Stage,

    /// <summary>Forum channel.</summary>
    Forum
}
