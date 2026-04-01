namespace DiscordBot.Core.Entities;

/// <summary>
/// Standalone audit log entry recording an input-validation rejection of a feature request submission.
/// No navigation properties — intentionally decoupled from the Guild entity to survive guild deletion.
/// </summary>
public class FeatureRequestRejection
{
    /// <summary>
    /// Unique identifier for this rejection record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where the rejected submission originated.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the member whose submission was rejected.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Human-readable reason explaining why the submission was rejected.
    /// </summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this rejection was recorded (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
