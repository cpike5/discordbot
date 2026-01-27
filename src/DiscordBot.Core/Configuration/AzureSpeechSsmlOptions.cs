namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for SSML support in Azure Speech service.
/// </summary>
public class AzureSpeechSsmlOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "AzureSpeech:Ssml";

    /// <summary>
    /// Gets or sets whether to validate SSML before sending to Azure.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, SSML documents are validated against Azure Speech service requirements
    /// before being sent. This helps catch errors early and provides better error messages.
    /// </remarks>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to reject invalid SSML (true) or fallback to plain text (false).
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// When strict mode is disabled and SSML validation fails, the system will attempt
    /// to extract plain text and synthesize that instead. When enabled, validation
    /// failures will throw an exception.
    /// </remarks>
    public bool StrictMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum allowed SSML complexity score (based on nested elements).
    /// Default is 50.
    /// </summary>
    /// <remarks>
    /// Complexity is calculated based on the depth and number of nested SSML elements.
    /// This prevents overly complex documents that may cause performance issues or
    /// exceed Azure service limits.
    /// </remarks>
    public int MaxComplexityScore { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum SSML document length in characters.
    /// Default is 5000.
    /// </summary>
    /// <remarks>
    /// Azure Speech service has limits on request size. This setting prevents
    /// excessively long SSML documents from being sent.
    /// </remarks>
    public int MaxDocumentLength { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to attempt automatic sanitization of invalid SSML.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, the system will attempt to fix common SSML issues such as
    /// unclosed tags, invalid characters, and unsupported elements before validation.
    /// </remarks>
    public bool EnableSanitization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the style presets feature.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Style presets provide quick access to common voice+style combinations
    /// for enhanced TTS output without requiring manual SSML construction.
    /// </remarks>
    public bool EnableStylePresets { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to cache voice capabilities metadata.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Caching voice capabilities reduces API calls when checking which
    /// styles and features are supported by specific voices.
    /// </remarks>
    public bool CacheVoiceCapabilities { get; set; } = true;

    /// <summary>
    /// Gets or sets the duration to cache voice capabilities in minutes.
    /// Default is 1440 (24 hours).
    /// </summary>
    /// <remarks>
    /// Voice capability metadata rarely changes, so a long cache duration
    /// is recommended to minimize API calls.
    /// </remarks>
    public int CacheDurationMinutes { get; set; } = 1440;
}
