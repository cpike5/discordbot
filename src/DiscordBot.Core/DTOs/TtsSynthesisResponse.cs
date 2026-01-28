namespace DiscordBot.Core.DTOs;

/// <summary>
/// Response DTO for TTS synthesis operations.
/// </summary>
public class TtsSynthesisResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the synthesized audio.
    /// </summary>
    public required string AudioId { get; init; }

    /// <summary>
    /// Gets or sets the estimated duration of the audio in seconds.
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Gets or sets the list of voices used in the synthesis (for multi-voice SSML).
    /// </summary>
    public List<string>? VoicesUsed { get; init; }
}
