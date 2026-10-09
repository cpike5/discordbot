using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>What a guild message asks of the assistant, if anything.</summary>
public enum AssistantTrigger
{
    /// <summary>Not for the assistant.</summary>
    Ignore,

    /// <summary>A mention answered with one reply in the channel it was asked in.</summary>
    NewQuestion,

    /// <summary>A mention in a text channel of a guild in thread mode: open a thread and answer there.</summary>
    NewThreadQuestion,

    /// <summary>A message in a thread the assistant holds: a turn of that conversation, no mention needed.</summary>
    ThreadTurn
}

/// <summary>
/// The one decision <c>AssistantMessageHandler</c> makes before it does anything: which kind of
/// request a message is. Pure, so every combination is a unit test rather than a socket fixture.
/// </summary>
public static class AssistantTriggerRules
{
    /// <summary>Classifies a message.</summary>
    /// <param name="authorIsBot">The author is a bot (including this one).</param>
    /// <param name="isGuildChannel">The message is in a guild, not a DM.</param>
    /// <param name="mentionsBot">The message mentions this bot.</param>
    /// <param name="isThread">The channel is a thread.</param>
    /// <param name="threadOwnedByBot">The thread was created by this bot.</param>
    /// <param name="threadIsKnownAssistantThread">An <c>AssistantThread</c> row exists for the thread.</param>
    /// <param name="mode">The guild's conversation mode.</param>
    public static AssistantTrigger Classify(
        bool authorIsBot,
        bool isGuildChannel,
        bool mentionsBot,
        bool isThread,
        bool threadOwnedByBot,
        bool threadIsKnownAssistantThread,
        AssistantConversationMode mode)
    {
        if (authorIsBot || !isGuildChannel)
        {
            return AssistantTrigger.Ignore;
        }

        // Both checks: ownership is cheap and filters every other thread before a database read;
        // the row is what proves the thread is a conversation and not some other thread the bot made.
        if (isThread && threadOwnedByBot && threadIsKnownAssistantThread)
        {
            return AssistantTrigger.ThreadTurn;
        }

        if (!mentionsBot)
        {
            return AssistantTrigger.Ignore;
        }

        // Discord cannot nest threads, so a mention inside someone else's thread is a single reply
        // whatever the guild's mode.
        if (!isThread && mode == AssistantConversationMode.Thread)
        {
            return AssistantTrigger.NewThreadQuestion;
        }

        return AssistantTrigger.NewQuestion;
    }
}
