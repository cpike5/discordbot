namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for Azure Cognitive Services Speech integration.
/// </summary>
public class AzureSpeechOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "AzureSpeech";

    /// <summary>
    /// Gets or sets the Azure Speech subscription key.
    /// This should be configured via user secrets, never in appsettings.json.
    /// </summary>
    /// <remarks>
    /// Required for speech synthesis. If not configured, the TTS service will be disabled.
    /// Set via user secrets: dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-key-here"
    /// </remarks>
    public string? SubscriptionKey { get; set; }

    /// <summary>
    /// Gets or sets the Azure region for the Speech service.
    /// Default is "eastus".
    /// </summary>
    /// <remarks>
    /// Common regions: eastus, westus, westus2, eastus2, westeurope, southeastasia.
    /// Must match the region of your Azure Speech resource.
    /// </remarks>
    public string Region { get; set; } = "eastus";

    /// <summary>
    /// Gets or sets the default voice to use for speech synthesis.
    /// Default is "en-US-JennyNeural" (female, US English).
    /// </summary>
    /// <remarks>
    /// Full list of voices: https://learn.microsoft.com/azure/ai-services/speech-service/language-support
    /// Examples: en-US-JennyNeural, en-US-GuyNeural, en-GB-SoniaNeural
    /// </remarks>
    public string DefaultVoice { get; set; } = "en-US-JennyNeural";

    /// <summary>
    /// Gets or sets the maximum allowed text length for synthesis in characters.
    /// Default is 500 characters.
    /// </summary>
    /// <remarks>
    /// Azure Speech service limits single requests to around 400KB of text.
    /// Keeping this limit lower prevents abuse and excessive API usage.
    /// </remarks>
    public int MaxTextLength { get; set; } = 500;

    /// <summary>
    /// Gets or sets the default speech rate multiplier.
    /// Range: 0.5 to 2.0. Default is 1.0 (normal speed).
    /// </summary>
    /// <remarks>
    /// 0.5 = 50% speed (slow), 1.0 = normal speed, 2.0 = 200% speed (fast).
    /// </remarks>
    public double DefaultSpeed { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the default pitch adjustment.
    /// Range: 0.5 to 1.5. Default is 1.0 (no adjustment).
    /// </summary>
    /// <remarks>
    /// 0.5 = -50% pitch (lower), 1.0 = normal pitch, 1.5 = +50% pitch (higher).
    /// This is converted to a percentage offset for SSML.
    /// </remarks>
    public double DefaultPitch { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the default volume level.
    /// Range: 0.0 to 1.0. Default is 0.8 (80% volume).
    /// </summary>
    /// <remarks>
    /// 0.0 = silent, 0.5 = 50% volume, 1.0 = 100% volume.
    /// </remarks>
    public double DefaultVolume { get; set; } = 0.8;
}
