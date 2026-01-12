namespace DiscordBot.Core.DTOs.Portal;

/// <summary>
/// DTO for voice channel information.
/// </summary>
public class VoiceChannelInfo
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID of the voice channel.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the voice channel.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
