namespace DiscordBot.Core.Entities;

/// <summary>
/// Audit log entry for a command execution.
/// </summary>
public class CommandLog
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the guild where the command was executed.
    /// Null if executed in DMs.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// ID of the user who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Name of the command that was executed.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized command parameters.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Command execution duration in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Whether the command completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Correlation ID for tracking the request across logs.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Navigation property for the guild (nullable for DM commands).
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the user.
    /// </summary>
    public User User { get; set; } = null!;
}
