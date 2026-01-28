namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Response DTO for SSML build operations.
/// </summary>
public class SsmlBuildResponse
{
    /// <summary>
    /// Gets or sets the built SSML markup.
    /// </summary>
    public string Ssml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the generated SSML is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation errors if the SSML is invalid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets validation warnings for the generated SSML.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
