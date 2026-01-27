using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Provides access to voice capability information with memory caching support.
/// Maintains a registry of known Azure TTS voices with their style support data.
/// </summary>
public class VoiceCapabilityProvider : IVoiceCapabilityProvider
{
    private readonly ILogger<VoiceCapabilityProvider> _logger;
    private readonly IMemoryCache _cache;

    // Cache key for all known voices
    private const string AllVoicesCacheKey = "voice_capabilities_all";

    // Default cache duration: 24 hours
    private const int DefaultCacheDurationMinutes = 24 * 60;

    // Known Azure neural voices with their supported styles
    private static readonly Dictionary<string, VoiceCapabilities> KnownVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US-JennyNeural"] = new()
        {
            VoiceName = "en-US-JennyNeural",
            DisplayName = "Jenny",
            Locale = "en-US",
            Gender = "Female",
            SupportedStyles = new[]
            {
                "angry", "assistant", "chat", "cheerful", "customerservice",
                "empathetic", "excited", "friendly", "hopeful", "newscast",
                "sad", "shouting", "terrified", "unfriendly", "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-US-AriaNeural"] = new()
        {
            VoiceName = "en-US-AriaNeural",
            DisplayName = "Aria",
            Locale = "en-US",
            Gender = "Female",
            SupportedStyles = new[]
            {
                "angry", "chat", "cheerful", "empathetic", "excited",
                "friendly", "hopeful", "sad", "shouting", "terrified",
                "unfriendly", "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-US-GuyNeural"] = new()
        {
            VoiceName = "en-US-GuyNeural",
            DisplayName = "Guy",
            Locale = "en-US",
            Gender = "Male",
            SupportedStyles = new[]
            {
                "angry", "cheerful", "excited", "friendly", "hopeful",
                "newscast", "sad", "shouting", "terrified", "unfriendly",
                "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-US-DavisNeural"] = new()
        {
            VoiceName = "en-US-DavisNeural",
            DisplayName = "Davis",
            Locale = "en-US",
            Gender = "Male",
            SupportedStyles = new[]
            {
                "angry", "chat", "cheerful", "excited", "friendly",
                "hopeful", "sad", "shouting", "terrified", "unfriendly",
                "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        }
    };

    /// <summary>
    /// Creates a new instance of VoiceCapabilityProvider.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic messages.</param>
    /// <param name="cache">Memory cache for storing capability data.</param>
    public VoiceCapabilityProvider(ILogger<VoiceCapabilityProvider> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    /// <inheritdoc/>
    public VoiceCapabilities? GetCapabilities(string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            _logger.LogDebug("GetCapabilities called with null or whitespace voice name");
            return null;
        }

        _logger.LogDebug("Getting capabilities for voice: {VoiceName}", voiceName);

        // Look up in known voices (static dictionary provides O(1) lookup)
        if (!KnownVoices.TryGetValue(voiceName, out var capabilities))
        {
            _logger.LogDebug("Voice not found in known voices registry: {VoiceName}", voiceName);
            return null;
        }

        _logger.LogDebug("Voice capabilities retrieved for: {VoiceName}", voiceName);

        return capabilities;
    }

    /// <inheritdoc/>
    public bool IsStyleSupported(string voiceName, string style)
    {
        if (string.IsNullOrWhiteSpace(voiceName) || string.IsNullOrWhiteSpace(style))
        {
            _logger.LogDebug("IsStyleSupported called with null/whitespace parameters");
            return false;
        }

        var capabilities = GetCapabilities(voiceName);
        if (capabilities == null)
        {
            _logger.LogDebug("Voice not found for style check: {VoiceName}, Style: {Style}", voiceName, style);
            return false;
        }

        var isSupported = capabilities.SupportedStyles.Contains(style, StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug(
            "Style support check: Voice={VoiceName}, Style={Style}, Supported={Supported}",
            voiceName, style, isSupported);

        return isSupported;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSupportedStyles(string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            _logger.LogDebug("GetSupportedStyles called with null or whitespace voice name");
            return Array.Empty<string>();
        }

        var capabilities = GetCapabilities(voiceName);
        if (capabilities == null)
        {
            _logger.LogDebug("Voice not found for style lookup: {VoiceName}", voiceName);
            return Array.Empty<string>();
        }

        _logger.LogDebug("Retrieved {StyleCount} supported styles for voice: {VoiceName}",
            capabilities.SupportedStyles.Count, voiceName);

        return capabilities.SupportedStyles;
    }

    /// <inheritdoc/>
    public IEnumerable<VoiceCapabilities> GetAllKnownVoices()
    {
        _logger.LogDebug("Getting all known voice capabilities");

        // Try to get from cache
        if (_cache.TryGetValue(AllVoicesCacheKey, out IEnumerable<VoiceCapabilities>? cached))
        {
            _logger.LogDebug("All voices found in cache");
            return cached;
        }

        // Get all voices from registry
        var allVoices = KnownVoices.Values.ToList();

        // Cache the result
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheDurationMinutes)
        };

        _cache.Set(AllVoicesCacheKey, (IEnumerable<VoiceCapabilities>)allVoices, cacheOptions);
        _logger.LogDebug("All voices cached: {VoiceCount} voices", allVoices.Count);

        return allVoices;
    }

}
