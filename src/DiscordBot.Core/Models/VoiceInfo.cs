namespace DiscordBot.Core.Models;

/// <summary>
/// Information about an available text-to-speech voice.
/// </summary>
public class VoiceInfo
{
    /// <summary>
    /// Short name identifier for the voice (e.g., "en-US-JennyNeural").
    /// </summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Human-readable display name for the voice (e.g., "Jenny").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Locale/language code for the voice (e.g., "en-US").
    /// </summary>
    public required string Locale { get; init; }

    /// <summary>
    /// Gender of the voice ("Female" or "Male").
    /// </summary>
    public required string Gender { get; init; }
}
