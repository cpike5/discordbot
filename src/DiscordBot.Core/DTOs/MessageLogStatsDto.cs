namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for message log statistics and analytics.
/// </summary>
public class MessageLogStatsDto
{
    /// <summary>
    /// Gets or sets the total number of messages logged.
    /// </summary>
    public long TotalMessages { get; set; }

    /// <summary>
    /// Gets or sets the number of direct messages logged.
    /// </summary>
    public long DmMessages { get; set; }

    /// <summary>
    /// Gets or sets the number of server channel messages logged.
    /// </summary>
    public long ServerMessages { get; set; }

    /// <summary>
    /// Gets or sets the count of unique message authors.
    /// </summary>
    public long UniqueAuthors { get; set; }

    /// <summary>
    /// Gets or sets the message count breakdown by day for the last 7 days.
    /// </summary>
    public List<DailyMessageCount> MessagesByDay { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the oldest logged message. Null if no messages exist.
    /// </summary>
    public DateTime? OldestMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the newest logged message. Null if no messages exist.
    /// </summary>
    public DateTime? NewestMessage { get; set; }
}

/// <summary>
/// Represents the message count for a specific day.
/// </summary>
/// <param name="Date">The date for this count.</param>
/// <param name="Count">The number of messages on this date.</param>
public record DailyMessageCount(DateOnly Date, long Count);
