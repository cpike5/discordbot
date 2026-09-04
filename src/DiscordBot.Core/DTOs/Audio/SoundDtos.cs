using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for sound uploaded event when a new sound is added to the soundboard.
/// </summary>
public class SoundUploadedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the sound was uploaded.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the sound name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the play count (always 0 for new sounds).
    /// </summary>
    public int PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the sound was uploaded.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for sound deleted event when a sound is removed from the soundboard.
/// </summary>
public class SoundDeletedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the sound was deleted.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID that was deleted.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the sound was deleted.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
