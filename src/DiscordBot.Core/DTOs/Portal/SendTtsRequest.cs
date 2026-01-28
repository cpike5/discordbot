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

    /// <summary>
    /// Gets or sets the optional voice style (e.g., "cheerful", "angry", "whispering").
    /// If provided, the message will be synthesized with the specified style.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Gets or sets the optional style intensity multiplier (0.01 to 2.0).
    /// Only applies when Style is specified.
    /// </summary>
    [Range(0.01, 2.0)]
    public decimal? StyleIntensity { get; set; }

    /// <summary>
    /// Gets or sets the optional raw SSML markup.
    /// If provided, this takes precedence over Message, Style, and other parameters.
    /// </summary>
    public string? Ssml { get; set; }
}
