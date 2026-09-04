using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for audio connected event when bot joins a voice channel.
/// </summary>
public class AudioConnectedDto
{
    /// <summary>
    /// Gets or sets the guild ID where the bot connected.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel ID the bot connected to.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
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
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the voice channel ID.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
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
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
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
/// DTO for the current audio status of a guild.
/// </summary>
public class AudioStatusDto
{
    /// <summary>
    /// Gets or sets the guild ID.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets whether the bot is connected to a voice channel.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the connected voice channel ID (null if not connected).
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
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
