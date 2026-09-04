using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for an item in the playback queue.
/// </summary>
public class QueueItemDto
{
    /// <summary>
    /// Gets or sets the position in the queue (0-based).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the sound ID.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the sound name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
}

/// <summary>
/// DTO for queue updated event when the playback queue changes.
/// </summary>
public class QueueUpdatedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the queue changed.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the current queue items.
    /// </summary>
    public List<QueueItemDto> Queue { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the queue was updated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
