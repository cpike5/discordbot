namespace DiscordBot.Core.Entities;

/// <summary>
/// Detailed log of individual DM assistant interactions for debugging and audit.
/// User-scoped (no GuildId/ChannelId) since DMs are direct.
/// </summary>
public class DmAssistantInteractionLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public ulong UserId { get; set; }
    public bool IsOwner { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public int ToolCalls { get; set; }
    public string? ToolNames { get; set; }
    public int LoopCount { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    public User? User { get; set; }
}
