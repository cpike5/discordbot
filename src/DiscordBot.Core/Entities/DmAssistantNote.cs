namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a personal note saved by the DM assistant for a user.
/// Used to persist user preferences, facts, and context across conversations.
/// </summary>
public class DmAssistantNote
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public string? Tag { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
