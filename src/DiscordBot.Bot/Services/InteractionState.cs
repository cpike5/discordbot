namespace DiscordBot.Bot.Services;

/// <summary>
/// Represents a stored state entry for a Discord component interaction.
/// </summary>
/// <typeparam name="T">The type of state data stored</typeparam>
public class InteractionState<T>
{
    /// <summary>
    /// The unique correlation ID for this state entry.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The user ID associated with this state.
    /// </summary>
    public required ulong UserId { get; init; }

    /// <summary>
    /// The state data.
    /// </summary>
    public required T Data { get; init; }

    /// <summary>
    /// The timestamp when this state was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The timestamp when this state expires.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Gets whether this state entry has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
