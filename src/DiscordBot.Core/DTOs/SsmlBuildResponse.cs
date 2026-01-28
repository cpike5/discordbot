namespace DiscordBot.Core.DTOs;

/// <summary>
/// Response DTO for the SSML build operation.
/// </summary>
public class SsmlBuildResponse
{
    /// <summary>
    /// Gets or sets the generated SSML markup.
    /// </summary>
    public required string Ssml { get; init; }

    /// <summary>
    /// Gets or sets whether the generated SSML is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets validation errors, if any.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Gets or sets validation warnings (non-critical issues).
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}
