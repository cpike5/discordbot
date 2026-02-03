namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Result of a VOX message playback operation.
/// </summary>
public record VoxPlaybackResult
{
    /// <summary>
    /// Whether the playback was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if playback failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of words that were matched to clips and played.
    /// </summary>
    public List<string> MatchedWords { get; init; } = new();

    /// <summary>
    /// List of words that had no matching clips and were skipped.
    /// </summary>
    public List<string> SkippedWords { get; init; } = new();

    /// <summary>
    /// Estimated total duration of the playback in seconds.
    /// </summary>
    public double EstimatedDurationSeconds { get; init; }
}
