namespace DiscordBot.Core.Entities;

/// <summary>
/// One turn of an assistant thread's history: a member's message or the assistant's reply. The
/// sliding window the next turn is seeded with is the most recent rows of one thread.
/// </summary>
public class AssistantThreadMessage
{
    public long Id { get; set; }

    /// <summary>The thread this turn belongs to.</summary>
    public ulong ThreadId { get; set; }

    /// <summary>
    /// The member who wrote a user turn, or the member whose question the assistant turn answers.
    /// Indexed so a purge can find a person's rows.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    /// <summary>Navigation property for the thread.</summary>
    public AssistantThread? Thread { get; set; }
}
