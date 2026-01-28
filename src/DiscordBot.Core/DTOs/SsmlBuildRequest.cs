namespace DiscordBot.Core.DTOs;

/// <summary>
/// Request DTO for building SSML from structured segments.
/// </summary>
public class SsmlBuildRequest
{
    /// <summary>
    /// Gets or sets the language code for the SSML document.
    /// </summary>
    public string Language { get; init; } = "en-US";

    /// <summary>
    /// Gets or sets the segments that make up the SSML document.
    /// </summary>
    public required List<SsmlSegment> Segments { get; init; }
}

/// <summary>
/// Represents a segment of SSML content with voice and style settings.
/// </summary>
public class SsmlSegment
{
    /// <summary>
    /// Gets or sets the voice name for this segment.
    /// </summary>
    public required string VoiceName { get; init; }

    /// <summary>
    /// Gets or sets the speaking style (e.g., "cheerful", "sad", "angry").
    /// </summary>
    public string? Style { get; init; }

    /// <summary>
    /// Gets or sets the style intensity (0.01-2.0).
    /// </summary>
    public double? StyleDegree { get; init; }

    /// <summary>
    /// Gets or sets the speaking rate (0.5-2.0).
    /// </summary>
    public double? Rate { get; init; }

    /// <summary>
    /// Gets or sets the pitch adjustment (0.5-1.5).
    /// </summary>
    public double? Pitch { get; init; }

    /// <summary>
    /// Gets or sets the volume level (0.0-1.0).
    /// </summary>
    public double? Volume { get; init; }

    /// <summary>
    /// Gets or sets the plain text content for this segment.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets optional inline SSML elements within this segment.
    /// </summary>
    public List<SsmlElement>? Elements { get; init; }
}

/// <summary>
/// Represents an inline SSML element such as a break, emphasis, or say-as.
/// </summary>
public class SsmlElement
{
    /// <summary>
    /// Gets or sets the element type (e.g., "break", "emphasis", "say-as", "phoneme").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the element attributes (e.g., "time" for break, "level" for emphasis).
    /// </summary>
    public Dictionary<string, string>? Attributes { get; init; }

    /// <summary>
    /// Gets or sets the text content for the element.
    /// </summary>
    public string? Content { get; init; }
}
