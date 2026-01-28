using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Request DTO for synthesizing SSML markup.
/// </summary>
public class SsmlSynthesisRequest
{
    /// <summary>
    /// Gets or sets the SSML markup to synthesize.
    /// </summary>
    [Required]
    public string Ssml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to play the synthesized audio in the bot's current voice channel.
    /// If false, only synthesizes and returns metadata without playing.
    /// </summary>
    public bool PlayInVoiceChannel { get; set; }
}
