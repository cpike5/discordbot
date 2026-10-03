namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// What the member portal needs to know about a guild from Discord: whether the bot can see it,
/// its voice channels, and whether a person belongs to it.
/// <para>
/// The portal pages, the portal authorization handler and the portal controllers go through this
/// seam instead of reaching into <c>DiscordSocketClient</c>. That keeps one place to answer
/// "is this guild reachable" and lets a development-only implementation answer it without a
/// gateway connection (see <see cref="Services.Portal.DevelopmentPortal"/>). Production always
/// registers the Discord-backed implementation.
/// </para>
/// </summary>
public interface IPortalGuildDirectory
{
    /// <summary>
    /// Whether the bot is connected to the Discord gateway right now.
    /// </summary>
    bool IsBotOnline { get; }

    /// <summary>
    /// Whether the bot can see the guild at all. False means the portal answers 404 for it.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsGuildAvailableAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The guild's voice channels in Discord's channel order. Empty when the guild is unknown.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    IReadOnlyList<PortalVoiceChannel> GetVoiceChannels(ulong guildId);

    /// <summary>
    /// Finds one voice channel, or null when the guild or the channel is unknown.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="channelId">The voice channel's Discord snowflake ID.</param>
    PortalVoiceChannel? FindVoiceChannel(ulong guildId, ulong channelId);

    /// <summary>
    /// Whether the Discord user belongs to the guild. Never throws: a lookup that fails is "no".
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="discordUserId">The member's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsMemberAsync(ulong guildId, ulong discordUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A voice channel as the portal shows it.
/// </summary>
/// <param name="Id">The channel's Discord snowflake ID.</param>
/// <param name="Name">The channel name.</param>
/// <param name="MemberCount">How many people (not bots) are in the channel.</param>
public sealed record PortalVoiceChannel(ulong Id, string Name, int MemberCount);
