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

    // Voice capability reference: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts#voice-styles-and-roles
    // Last verified: 2026-02
    // Known Azure neural voices with their supported styles
    private static readonly Dictionary<string, VoiceCapabilities> KnownVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── en-US voices ──────────────────────────────────────────────────────
        ["en-US-JennyNeural"] = new()
        {
            VoiceName = "en-US-JennyNeural",
            DisplayName = "Jenny",
            Locale = "en-US",
            Gender = "Female",
            SupportedStyles = new[]
            {
                "angry", "assistant", "chat", "cheerful", "customerservice",
                "excited", "friendly", "hopeful", "newscast",
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
                "angry", "chat", "cheerful", "customerservice", "empathetic",
                "excited", "friendly", "hopeful", "narration-professional",
                "newscast-casual", "newscast-formal", "sad", "shouting",
                "terrified", "unfriendly", "whispering"
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
            SupportsEmphasis = true,
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
            SupportsEmphasis = true,
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-US-JaneNeural"] = new()
        {
            VoiceName = "en-US-JaneNeural",
            DisplayName = "Jane",
            Locale = "en-US",
            Gender = "Female",
            SupportedStyles = new[]
            {
                "angry", "cheerful", "excited", "friendly", "hopeful",
                "sad", "shouting", "terrified", "unfriendly", "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsEmphasis = true,
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-US-JasonNeural"] = new()
        {
            VoiceName = "en-US-JasonNeural",
            DisplayName = "Jason",
            Locale = "en-US",
            Gender = "Male",
            SupportedStyles = new[]
            {
                "angry", "cheerful", "excited", "friendly", "hopeful",
                "sad", "shouting", "terrified", "unfriendly", "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── en-GB voices ──────────────────────────────────────────────────────
        ["en-GB-RyanNeural"] = new()
        {
            VoiceName = "en-GB-RyanNeural",
            DisplayName = "Ryan",
            Locale = "en-GB",
            Gender = "Male",
            SupportedStyles = new[] { "chat", "cheerful", "sad", "whispering" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-GB-SoniaNeural"] = new()
        {
            VoiceName = "en-GB-SoniaNeural",
            DisplayName = "Sonia",
            Locale = "en-GB",
            Gender = "Female",
            SupportedStyles = new[] { "cheerful", "sad" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["en-GB-LibbyNeural"] = new()
        {
            VoiceName = "en-GB-LibbyNeural",
            DisplayName = "Libby",
            Locale = "en-GB",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── ja-JP voices ──────────────────────────────────────────────────────
        ["ja-JP-NanamiNeural"] = new()
        {
            VoiceName = "ja-JP-NanamiNeural",
            DisplayName = "Nanami",
            Locale = "ja-JP",
            Gender = "Female",
            SupportedStyles = new[] { "chat", "cheerful", "customerservice" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["ja-JP-KeitaNeural"] = new()
        {
            VoiceName = "ja-JP-KeitaNeural",
            DisplayName = "Keita",
            Locale = "ja-JP",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["ja-JP-MayuNeural"] = new()
        {
            VoiceName = "ja-JP-MayuNeural",
            DisplayName = "Mayu",
            Locale = "ja-JP",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["ja-JP-NaokiNeural"] = new()
        {
            VoiceName = "ja-JP-NaokiNeural",
            DisplayName = "Naoki",
            Locale = "ja-JP",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── fr-FR voices ──────────────────────────────────────────────────────
        ["fr-FR-DeniseNeural"] = new()
        {
            VoiceName = "fr-FR-DeniseNeural",
            DisplayName = "Denise",
            Locale = "fr-FR",
            Gender = "Female",
            SupportedStyles = new[] { "cheerful", "excited", "sad", "whispering" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["fr-FR-HenriNeural"] = new()
        {
            VoiceName = "fr-FR-HenriNeural",
            DisplayName = "Henri",
            Locale = "fr-FR",
            Gender = "Male",
            SupportedStyles = new[] { "cheerful", "excited", "sad", "whispering" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["fr-FR-BrigitteNeural"] = new()
        {
            VoiceName = "fr-FR-BrigitteNeural",
            DisplayName = "Brigitte",
            Locale = "fr-FR",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── de-DE voices ──────────────────────────────────────────────────────
        ["de-DE-ConradNeural"] = new()
        {
            VoiceName = "de-DE-ConradNeural",
            DisplayName = "Conrad",
            Locale = "de-DE",
            Gender = "Male",
            SupportedStyles = new[] { "cheerful", "sad" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["de-DE-KatjaNeural"] = new()
        {
            VoiceName = "de-DE-KatjaNeural",
            DisplayName = "Katja",
            Locale = "de-DE",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── it-IT voices ──────────────────────────────────────────────────────
        ["it-IT-DiegoNeural"] = new()
        {
            VoiceName = "it-IT-DiegoNeural",
            DisplayName = "Diego",
            Locale = "it-IT",
            Gender = "Male",
            SupportedStyles = new[] { "cheerful", "excited", "sad" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["it-IT-ElsaNeural"] = new()
        {
            VoiceName = "it-IT-ElsaNeural",
            DisplayName = "Elsa",
            Locale = "it-IT",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── es-ES voices ──────────────────────────────────────────────────────
        ["es-ES-AlvaroNeural"] = new()
        {
            VoiceName = "es-ES-AlvaroNeural",
            DisplayName = "Alvaro",
            Locale = "es-ES",
            Gender = "Male",
            SupportedStyles = new[] { "cheerful", "sad" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["es-ES-ElviraNeural"] = new()
        {
            VoiceName = "es-ES-ElviraNeural",
            DisplayName = "Elvira",
            Locale = "es-ES",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── es-MX voices ──────────────────────────────────────────────────────
        ["es-MX-DaliaNeural"] = new()
        {
            VoiceName = "es-MX-DaliaNeural",
            DisplayName = "Dalia",
            Locale = "es-MX",
            Gender = "Female",
            SupportedStyles = new[] { "cheerful", "sad", "whispering" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── hi-IN voices ──────────────────────────────────────────────────────
        ["hi-IN-SwaraNeural"] = new()
        {
            VoiceName = "hi-IN-SwaraNeural",
            DisplayName = "Swara",
            Locale = "hi-IN",
            Gender = "Female",
            SupportedStyles = new[] { "cheerful", "empathetic", "newscast" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["hi-IN-MadhurNeural"] = new()
        {
            VoiceName = "hi-IN-MadhurNeural",
            DisplayName = "Madhur",
            Locale = "hi-IN",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── zh-CN voices ──────────────────────────────────────────────────────
        ["zh-CN-XiaoxiaoNeural"] = new()
        {
            VoiceName = "zh-CN-XiaoxiaoNeural",
            DisplayName = "Xiaoxiao",
            Locale = "zh-CN",
            Gender = "Female",
            SupportedStyles = new[]
            {
                "affectionate", "angry", "assistant", "calm", "chat",
                "chat-casual", "cheerful", "customerservice", "disgruntled",
                "excited", "fearful", "friendly", "gentle", "lyrical",
                "newscast", "poetry-reading", "sad", "serious", "sorry",
                "whispering"
            },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["zh-CN-YunxiNeural"] = new()
        {
            VoiceName = "zh-CN-YunxiNeural",
            DisplayName = "Yunxi",
            Locale = "zh-CN",
            Gender = "Male",
            SupportedStyles = new[]
            {
                "angry", "assistant", "chat", "cheerful", "depressed",
                "disgruntled", "embarrassed", "fearful", "narration-relaxed",
                "newscast", "sad", "serious"
            },
            SupportedRoles = new[] { "Boy", "Narrator", "YoungAdultMale" },
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["zh-CN-YunyangNeural"] = new()
        {
            VoiceName = "zh-CN-YunyangNeural",
            DisplayName = "Yunyang",
            Locale = "zh-CN",
            Gender = "Male",
            SupportedStyles = new[] { "customerservice", "narration-professional", "newscast-casual" },
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── sv-SE voices ──────────────────────────────────────────────────────
        ["sv-SE-SofieNeural"] = new()
        {
            VoiceName = "sv-SE-SofieNeural",
            DisplayName = "Sofie",
            Locale = "sv-SE",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["sv-SE-MattiasNeural"] = new()
        {
            VoiceName = "sv-SE-MattiasNeural",
            DisplayName = "Mattias",
            Locale = "sv-SE",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── ru-RU voices ──────────────────────────────────────────────────────
        ["ru-RU-SvetlanaNeural"] = new()
        {
            VoiceName = "ru-RU-SvetlanaNeural",
            DisplayName = "Svetlana",
            Locale = "ru-RU",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["ru-RU-DmitryNeural"] = new()
        {
            VoiceName = "ru-RU-DmitryNeural",
            DisplayName = "Dmitry",
            Locale = "ru-RU",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },

        // ── ar-SA voices ──────────────────────────────────────────────────────
        ["ar-SA-ZariyahNeural"] = new()
        {
            VoiceName = "ar-SA-ZariyahNeural",
            DisplayName = "Zariyah",
            Locale = "ar-SA",
            Gender = "Female",
            SupportedStyles = Array.Empty<string>(),
            SupportedRoles = Array.Empty<string>(),
            SupportsMultilingual = false,
            IsPremium = false,
            VoiceType = "Neural",
            SampleRate = 48000
        },
        ["ar-SA-HamedNeural"] = new()
        {
            VoiceName = "ar-SA-HamedNeural",
            DisplayName = "Hamed",
            Locale = "ar-SA",
            Gender = "Male",
            SupportedStyles = Array.Empty<string>(),
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
