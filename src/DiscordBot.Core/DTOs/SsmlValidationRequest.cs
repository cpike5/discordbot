namespace DiscordBot.Core.DTOs;

/// <summary>
/// Request DTO for validating SSML markup.
/// </summary>
public class SsmlValidationRequest
{
    /// <summary>
    /// Gets or sets the SSML markup to validate.
    /// </summary>
    public required string Ssml { get; init; }
}
