namespace DiscordBot.Core.DTOs.Vox;

/// <summary>
/// Options for VOX message playback.
/// </summary>
public record VoxPlaybackOptions
{
    /// <summary>
    /// Word gap in milliseconds (20-200).
    /// </summary>
    public int WordGapMs { get; init; } = 50;
}
