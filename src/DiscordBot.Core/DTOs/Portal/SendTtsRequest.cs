using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.DTOs.Portal;

/// <summary>
/// Request DTO for sending a TTS message through the portal.
/// </summary>
public class SendTtsRequest
{
    /// <summary>
    /// Gets or sets the text message to synthesize.
    /// Maximum length is configurable via AzureSpeechOptions.MaxTextLength (default: 500).
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice identifier to use for synthesis.
    /// </summary>
    [Required]
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the speech rate multiplier (0.5 to 2.0).
    /// </summary>
    [Range(0.5, 2.0)]
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the pitch adjustment (0.5 to 2.0).
    /// </summary>
    [Range(0.5, 2.0)]
    public double Pitch { get; set; } = 1.0;
}
