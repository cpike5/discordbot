namespace DiscordBot.Core.DTOs;

/// <summary>
/// Request DTO for synthesizing speech from SSML markup.
/// </summary>
public class SsmlSynthesisRequest
{
    /// <summary>
    /// Gets or sets the SSML markup to synthesize.
    /// </summary>
    public required string Ssml { get; init; }

    /// <summary>
    /// Gets or sets whether to play the synthesized audio in the user's voice channel.
    /// </summary>
    public bool PlayInVoiceChannel { get; init; } = false;
}
