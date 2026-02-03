using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Information about a single VOX audio clip.
/// </summary>
public record VoxClipInfo
{
    /// <summary>
    /// Normalized clip name used for lookups (punctuation stripped, lowercase).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Original filename without extension (may contain punctuation like "request!").
    /// Used to locate the actual file on disk.
    /// </summary>
    public string FileName { get; init; } = "";

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
