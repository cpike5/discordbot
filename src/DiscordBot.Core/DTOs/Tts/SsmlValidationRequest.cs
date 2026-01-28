using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Request DTO for validating SSML markup.
/// </summary>
public class SsmlValidationRequest
{
    /// <summary>
    /// Gets or sets the SSML markup to validate.
    /// </summary>
    [Required]
    public string Ssml { get; set; } = string.Empty;
}
