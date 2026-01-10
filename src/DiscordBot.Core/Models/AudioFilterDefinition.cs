namespace DiscordBot.Core.Models;

/// <summary>
/// Defines an audio filter with its display properties and FFmpeg filter string.
/// </summary>
public class AudioFilterDefinition
{
    /// <summary>
    /// Gets the display name of the filter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the description of what the filter does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the FFmpeg audio filter argument string.
    /// </summary>
    /// <remarks>
    /// This value is passed to FFmpeg's -af parameter.
    /// For example: "aecho=0.8:0.9:40:0.4" for reverb effect.
    /// </remarks>
    public required string FfmpegFilter { get; init; }
}
