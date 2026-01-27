namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing an upcoming reminder.
/// </summary>
public class UpcomingReminderDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this reminder.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID who set the reminder.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the user who set the reminder.
    /// Falls back to UserId as string if username is not available.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reminder message preview (truncated to 50 characters).
    /// </summary>
    public string MessagePreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the reminder should be triggered.
    /// </summary>
    public DateTime TriggerAt { get; set; }

    /// <summary>
    /// Gets the trigger time in ISO format for client-side timezone conversion.
    /// </summary>
    public string TriggerAtUtcIso => DateTime.SpecifyKind(TriggerAt, DateTimeKind.Utc).ToString("o");
}
