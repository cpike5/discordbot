namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Preview of a tokenized VOX message showing which words match clips.
/// </summary>
public record VoxTokenPreview
{
    /// <summary>
    /// List of tokens with their match status.
    /// </summary>
    public List<VoxTokenInfo> Tokens { get; init; } = new();

    /// <summary>
    /// Count of tokens that have matching clips.
    /// </summary>
    public int MatchedCount { get; init; }

    /// <summary>
    /// Count of tokens that have no matching clips.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Estimated total duration of the message in seconds.
    /// </summary>
    public double EstimatedDurationSeconds { get; init; }
}
