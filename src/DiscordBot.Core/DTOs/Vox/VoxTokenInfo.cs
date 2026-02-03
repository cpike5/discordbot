namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Information about a single word token in a VOX message.
/// </summary>
public record VoxTokenInfo
{
    /// <summary>
    /// The word token.
    /// </summary>
    public string Word { get; init; } = "";

    /// <summary>
    /// Whether a clip exists for this word.
    /// </summary>
    public bool HasClip { get; init; }

    /// <summary>
    /// Duration of the clip in seconds (0 if no clip exists).
    /// </summary>
    public double DurationSeconds { get; init; }
}
