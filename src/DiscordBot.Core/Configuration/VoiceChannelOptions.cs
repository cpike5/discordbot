namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the voice channel system.
/// </summary>
public class VoiceChannelOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "VoiceChannel";

    /// <summary>
    /// Gets or sets the timeout (in seconds) for automatically leaving a voice channel when the bot is alone.
    /// Default is 300 seconds (5 minutes). Set to 0 to stay indefinitely.
    /// </summary>
    public int AutoLeaveTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the interval (in seconds) between checking for auto-leave conditions.
    /// Default is 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;
}
