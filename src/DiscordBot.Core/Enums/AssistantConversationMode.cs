namespace DiscordBot.Core.Enums;

/// <summary>
/// How the guild assistant answers a mention. Stored as an int on
/// <see cref="Entities.AssistantGuildSettings.ConversationMode"/>.
/// </summary>
public enum AssistantConversationMode
{
    /// <summary>One reply in the channel, no memory of earlier questions. The default.</summary>
    SingleReply = 0,

    /// <summary>
    /// A mention opens a public thread off the member's message and the answer goes there; later
    /// messages in that thread continue the conversation without a mention.
    /// </summary>
    Thread = 1
}
