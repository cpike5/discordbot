namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the Voice Channel Control Panel component.
/// Displays voice connection status, playback state, and queue management.
/// </summary>
public record VoiceChannelPanelViewModel
{
    /// <summary>
    /// The Discord guild ID this panel is for.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <summary>
    /// Whether the bot is currently connected to a voice channel.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// The name of the currently connected channel, if any.
    /// </summary>
    public string? ConnectedChannelName { get; init; }

    /// <summary>
    /// The ID of the currently connected channel, if any.
    /// </summary>
    public ulong? ConnectedChannelId { get; init; }

    /// <summary>
    /// Number of members in the currently connected channel.
    /// </summary>
    public int? ChannelMemberCount { get; init; }

    /// <summary>
    /// List of available voice channels in the guild.
    /// </summary>
    public IReadOnlyList<VoiceChannelInfo> AvailableChannels { get; init; } = [];

    /// <summary>
    /// Information about the currently playing audio, if any.
    /// </summary>
    public NowPlayingInfo? NowPlaying { get; init; }

    /// <summary>
    /// List of queued audio items.
    /// </summary>
    public IReadOnlyList<QueueItemInfo> Queue { get; init; } = [];

    /// <summary>
    /// Total number of items in the queue.
    /// </summary>
    public int QueueCount => Queue.Count;
}

/// <summary>
/// Information about a voice channel available for the bot to join.
/// </summary>
public record VoiceChannelInfo
{
    /// <summary>
    /// The Discord voice channel ID.
    /// </summary>
    public required ulong Id { get; init; }

    /// <summary>
    /// The display name of the voice channel.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Number of members currently in the channel.
    /// </summary>
    public int MemberCount { get; init; }
}

/// <summary>
/// Information about the currently playing audio.
/// </summary>
public record NowPlayingInfo
{
    /// <summary>
    /// The ID of the sound or message being played.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The display name of what's playing.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Total duration in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Current playback position in seconds.
    /// </summary>
    public double PositionSeconds { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent => DurationSeconds > 0
        ? (int)Math.Round(PositionSeconds / DurationSeconds * 100)
        : 0;
}

/// <summary>
/// Information about an item in the playback queue.
/// </summary>
public record QueueItemInfo
{
    /// <summary>
    /// Position in the queue (1-based).
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// The ID of the queued item.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The display name of the queued item.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Formatted duration string (e.g., "0:30").
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
