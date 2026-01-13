namespace DiscordBot.Core.DTOs.Portal;

/// <summary>
/// Response DTO for TTS bot status information.
/// </summary>
public class TtsStatusResponse
{
    /// <summary>
    /// Gets or sets whether the bot is connected to a voice channel.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the ID of the voice channel the bot is connected to.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the name of the voice channel the bot is connected to.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets whether a TTS message is currently playing.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Gets or sets the current message being played (truncated to 50 characters).
    /// </summary>
    public string? CurrentMessage { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed message length in characters.
    /// </summary>
    public int MaxMessageLength { get; set; }
}
