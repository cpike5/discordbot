namespace DiscordBot.Core.DTOs.Llm;

/// <summary>
/// One question put to the guild assistant: where it was asked, by whom, and whether it continues
/// a thread the assistant holds.
/// </summary>
/// <param name="GuildId">The guild the question was asked in.</param>
/// <param name="ChannelId">The channel the member wrote in: the thread id for a thread turn.</param>
/// <param name="ParentChannelId">
/// The text channel a thread hangs off, or null outside a thread. The allowed-channel check runs
/// against this when set: the list is about where conversations may start, and a thread inherits
/// its parent.
/// </param>
/// <param name="ThreadId">The assistant thread this turn continues, or null for a single reply.</param>
/// <param name="UserId">The member who asked.</param>
/// <param name="MessageId">The Discord message carrying the question.</param>
/// <param name="Question">The question text with the bot mention removed.</param>
/// <param name="CallerCanMutate">
/// Whether this caller may use tools that create or change data, decided from their Discord
/// permissions by the host. Defaults to false: a caller that was never assessed gets read-only
/// access.
/// </param>
public sealed record GuildAssistantRequest(
    ulong GuildId,
    ulong ChannelId,
    ulong? ParentChannelId,
    ulong? ThreadId,
    ulong UserId,
    ulong MessageId,
    string Question,
    bool CallerCanMutate = false)
{
    /// <summary>The channel the allowed-channel list is checked against.</summary>
    public ulong EffectiveChannelId => ParentChannelId ?? ChannelId;

    /// <summary>A single-reply request, as the pre-thread overloads build it.</summary>
    public static GuildAssistantRequest SingleReply(
        ulong guildId, ulong channelId, ulong userId, ulong messageId, string question, bool callerCanMutate = false)
        => new(guildId, channelId, null, null, userId, messageId, question, callerCanMutate);
}
