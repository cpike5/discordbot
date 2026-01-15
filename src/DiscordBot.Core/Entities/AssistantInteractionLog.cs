namespace DiscordBot.Core.Entities;

/// <summary>
/// Detailed log of individual assistant interactions for debugging and audit.
/// Stores question/response pairs, token usage, latency, and error information.
/// Subject to retention policy configured in AssistantOptions.
/// </summary>
public class AssistantInteractionLog
{
    /// <summary>
    /// Unique identifier for this interaction.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Timestamp when the question was asked (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Discord user ID who asked the question.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Discord guild ID where question was asked.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel ID where question was asked.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord message ID of the user's question.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// User's original question (truncated to MaxQuestionLength).
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Claude's response (may be truncated to MaxResponseLength).
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Number of input tokens consumed (non-cached).
    /// </summary>
    public int InputTokens { get; set; } = 0;

    /// <summary>
    /// Number of output tokens consumed.
    /// </summary>
    public int OutputTokens { get; set; } = 0;

    /// <summary>
    /// Number of tokens served from cache.
    /// </summary>
    public int CachedTokens { get; set; } = 0;

    /// <summary>
    /// Number of tokens written to cache (on cache miss).
    /// </summary>
    public int CacheCreationTokens { get; set; } = 0;

    /// <summary>
    /// Whether the prompt cache was hit.
    /// </summary>
    public bool CacheHit { get; set; } = false;

    /// <summary>
    /// Number of tool calls executed.
    /// </summary>
    public int ToolCalls { get; set; } = 0;

    /// <summary>
    /// Total response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; } = 0;

    /// <summary>
    /// Whether the request succeeded.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Estimated cost in USD for this interaction.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; } = 0m;

    /// <summary>
    /// Navigation property for the user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
