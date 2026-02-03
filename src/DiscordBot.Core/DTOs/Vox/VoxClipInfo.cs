using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Information about a single VOX audio clip.
/// </summary>
public record VoxClipInfo
{
    /// <summary>
    /// Clip name (filename without extension, e.g. "warning").
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Which group this clip belongs to.
    /// </summary>
    public VoxClipGroup Group { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Audio duration in seconds (extracted via FFprobe at startup).
    /// </summary>
    public double DurationSeconds { get; init; }
}
