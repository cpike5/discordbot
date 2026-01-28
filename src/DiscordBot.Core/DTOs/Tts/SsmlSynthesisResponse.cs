namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Response DTO for SSML synthesis operations.
/// </summary>
public class SsmlSynthesisResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the synthesized audio.
    /// </summary>
    public Guid AudioId { get; set; }

    /// <summary>
    /// Gets or sets the duration of the synthesized audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the list of voice names used in the SSML.
    /// </summary>
    public IReadOnlyList<string> VoicesUsed { get; set; } = [];
}
