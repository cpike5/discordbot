namespace DiscordBot.Core.Models;

/// <summary>
/// Result of SSML validation.
/// </summary>
public class SsmlValidationResult
{
    /// <summary>
    /// Whether the SSML is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors if invalid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings (non-critical issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Detected voices used in the SSML.
    /// </summary>
    public IReadOnlyList<string> DetectedVoices { get; init; } = [];

    /// <summary>
    /// Estimated audio duration in seconds (if calculable).
    /// </summary>
    public double? EstimatedDurationSeconds { get; init; }

    /// <summary>
    /// Character count of plain text content (excludes markup).
    /// </summary>
    public int PlainTextLength { get; init; }
}
