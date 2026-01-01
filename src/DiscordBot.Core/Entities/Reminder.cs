using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a scheduled reminder that will be delivered to a Discord user at a specified time.
/// </summary>
public class Reminder
{
    /// <summary>
    /// Unique identifier for this reminder.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where the reminder was created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where the reminder was created.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord user snowflake ID who set the reminder.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The reminder message content (max 500 characters).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should be triggered and delivered.
    /// </summary>
    public DateTime TriggerAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was delivered to the user.
    /// Null if the reminder is still pending or failed.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Current status of the reminder.
    /// </summary>
    public ReminderStatus Status { get; set; }

    /// <summary>
    /// Number of delivery attempts made for this reminder.
    /// </summary>
    public int DeliveryAttempts { get; set; }

    /// <summary>
    /// Last error message if delivery failed.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Navigation property for the guild this reminder belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
