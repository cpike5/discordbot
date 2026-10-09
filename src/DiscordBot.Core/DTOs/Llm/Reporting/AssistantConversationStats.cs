namespace DiscordBot.Core.DTOs.Llm.Reporting;

/// <summary>
/// How much of a guild's assistant traffic ran in threads over a period: the number of distinct
/// conversations and the turns they took. Counted from the interaction log's <c>ThreadId</c>, so a
/// retained-away thread still counts for the days its turns are logged.
/// </summary>
/// <param name="Conversations">Distinct threads with at least one logged turn in the period.</param>
/// <param name="Turns">Logged turns across those threads.</param>
public sealed record AssistantConversationStats(int Conversations, int Turns)
{
    /// <summary>No threads in the period.</summary>
    public static readonly AssistantConversationStats Empty = new(0, 0);

    /// <summary>Turns per conversation, or 0 when there were none.</summary>
    public double AverageTurns => Conversations == 0 ? 0 : (double)Turns / Conversations;
}
