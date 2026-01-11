namespace DiscordBot.Core.Models;

/// <summary>
/// Options for text-to-speech synthesis.
/// </summary>
public class TtsOptions
{
    /// <summary>
    /// The voice to use for synthesis (e.g., "en-US-JennyNeural").
    /// </summary>
    public string Voice { get; set; } = "en-US-JennyNeural";

    /// <summary>
    /// Speech rate multiplier. Range: 0.5 to 2.0. Default is 1.0 (normal speed).
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// Pitch adjustment. Range: 0.5 to 1.5 (relative). Default is 1.0 (no adjustment).
    /// </summary>
    /// <remarks>
    /// This value is converted to a percentage for SSML: (Pitch - 1.0) * 100%.
    /// Example: 0.5 = -50%, 1.0 = 0%, 1.5 = +50%.
    /// </remarks>
    public double Pitch { get; set; } = 1.0;

    /// <summary>
    /// Volume level. Range: 0.0 to 1.0. Default is 1.0.
    /// </summary>
    public double Volume { get; set; } = 1.0;
}
