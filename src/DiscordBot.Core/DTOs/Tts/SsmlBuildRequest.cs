using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Request DTO for building SSML from structured segments.
/// </summary>
public class SsmlBuildRequest
{
    /// <summary>
    /// Gets or sets the language code for the SSML document (e.g., "en-US").
    /// </summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the segments to include in the SSML document.
    /// </summary>
    [Required]
    public List<SsmlSegment> Segments { get; set; } = [];
}

/// <summary>
/// Represents a segment of SSML with voice and style configuration.
/// </summary>
public class SsmlSegment
{
    /// <summary>
    /// Gets or sets the voice name (e.g., "en-US-JennyNeural").
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// Gets or sets the speaking style (e.g., "cheerful", "sad").
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Gets or sets the speech rate multiplier (0.5 to 2.0).
    /// </summary>
    public double? Rate { get; set; }

    /// <summary>
    /// Gets or sets the pitch adjustment (0.5 to 2.0).
    /// </summary>
    public double? Pitch { get; set; }

    /// <summary>
    /// Gets or sets the plain text content for this segment.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets additional SSML elements to include in this segment.
    /// </summary>
    public List<SsmlElement> Elements { get; set; } = [];
}

/// <summary>
/// Represents an SSML element (break, emphasis, say-as, etc.).
/// </summary>
public class SsmlElement
{
    /// <summary>
    /// Gets or sets the element type (e.g., "break", "emphasis", "say-as").
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text content for the element (if applicable).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets additional attributes for the element (e.g., duration for break, level for emphasis).
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];
}
