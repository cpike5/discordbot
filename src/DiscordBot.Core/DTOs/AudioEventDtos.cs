namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for audio connected event when bot joins a voice channel.
/// </summary>
public class AudioConnectedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the bot connected.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel ID the bot connected to.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel name.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of members in the voice channel (excluding bots).
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the connection was established.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for voice channel member count updated event when users join/leave the channel.
/// </summary>
public class VoiceChannelMemberCountUpdatedDto
{
    /// <summary>
    /// Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel name.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the updated member count (excluding bots).
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for audio disconnected event when bot leaves a voice channel.
/// </summary>
public class AudioDisconnectedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the bot disconnected.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the reason for disconnection.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the disconnection occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for playback started event when a sound begins playing.
/// </summary>
public class PlaybackStartedDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback started.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID being played.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the sound name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the sound in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when playback started.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for playback progress updates during sound playback.
/// </summary>
public class PlaybackProgressDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback is occurring.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID being played.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the current position in seconds.
    /// </summary>
    public double PositionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for playback finished event when a sound completes.
/// </summary>
public class PlaybackFinishedDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback finished.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID that finished playing.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets whether playback was cancelled (vs. completed naturally).
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when playback finished.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

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

/// <summary>
/// DTO for sound uploaded event when a new sound is added to the soundboard.
/// </summary>
public class SoundUploadedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the sound was uploaded.
    /// </summary>
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

/// <summary>
/// DTO for the current audio status of a guild.
/// </summary>
public class AudioStatusDto
{
    /// <summary>
    /// Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets whether the bot is connected to a voice channel.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the connected voice channel ID (null if not connected).
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the connected voice channel name (null if not connected).
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets whether audio is currently playing.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Gets or sets the currently playing sound (null if not playing).
    /// </summary>
    public PlaybackStartedDto? CurrentSound { get; set; }

    /// <summary>
    /// Gets or sets the current playback position in seconds (null if not playing).
    /// </summary>
    public double? CurrentPositionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of sounds in the queue.
    /// </summary>
    public int QueueLength { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of this status snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
