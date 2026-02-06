namespace DiscordBot.Core.Models;

/// <summary>
/// Describes the capabilities of a specific TTS voice.
/// </summary>
public class VoiceCapabilities
{
    /// <summary>
    /// Voice short name (e.g., "en-US-JennyNeural").
    /// </summary>
    public required string VoiceName { get; init; }

    /// <summary>
    /// Display name for the voice.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Locale code (e.g., "en-US").
    /// </summary>
    public required string Locale { get; init; }

    /// <summary>
    /// Gender (Female, Male, Neutral).
    /// </summary>
    public required string Gender { get; init; }

    /// <summary>
    /// Supported speaking styles (cheerful, sad, angry, etc.).
    /// Empty if voice doesn't support styles.
    /// </summary>
    public IReadOnlyList<string> SupportedStyles { get; init; } = [];

    /// <summary>
    /// Supported roles (YoungAdultFemale, OlderAdultMale, etc.).
    /// Empty if voice doesn't support roles.
    /// </summary>
    public IReadOnlyList<string> SupportedRoles { get; init; } = [];

    /// <summary>
    /// Whether voice supports the SSML &lt;emphasis&gt; element.
    /// Only en-US-GuyNeural, en-US-DavisNeural, and en-US-JaneNeural support this.
    /// </summary>
    public bool SupportsEmphasis { get; init; }

    /// <summary>
    /// Whether voice supports multilingual content.
    /// </summary>
    public bool SupportsMultilingual { get; init; }

    /// <summary>
    /// Whether voice is premium/paid tier.
    /// </summary>
    public bool IsPremium { get; init; }

    /// <summary>
    /// Voice type classification (neural, standard, wavenet, etc.).
    /// </summary>
    public string VoiceType { get; init; } = "Neural";

    /// <summary>
    /// Sample rate in Hz (typically 48000 for neural voices).
    /// </summary>
    public int SampleRate { get; init; } = 48000;
}
