namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a single message in a DM conversation history (sliding window).
/// Stores both user and assistant messages for multi-turn conversation context.
/// </summary>
public class DmConversationMessage
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}
