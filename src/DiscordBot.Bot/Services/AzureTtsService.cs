using System.Collections.Concurrent;
using System.Text;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for text-to-speech synthesis using Azure Cognitive Services Speech.
/// Converts text to PCM audio compatible with Discord voice channels (48kHz, 16-bit, stereo).
/// </summary>
public class AzureTtsService : ITtsService
{
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<AzureTtsService> _logger;
    private readonly SpeechConfig? _speechConfig;
    private readonly ConcurrentDictionary<string, List<Core.Models.VoiceInfo>> _voiceCache = new();
    private readonly SemaphoreSlim _voiceCacheLock = new(1, 1);
    private readonly IVoiceCapabilityProvider _voiceCapabilityProvider;
    private readonly IStylePresetProvider _stylePresetProvider;
    private readonly ISsmlValidator _ssmlValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTtsService"/> class.
    /// </summary>
    /// <param name="options">Azure Speech configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="voiceCapabilityProvider">The voice capability provider.</param>
    /// <param name="stylePresetProvider">The style preset provider.</param>
    /// <param name="ssmlValidator">The SSML validator.</param>
    public AzureTtsService(
        IOptions<AzureSpeechOptions> options,
        ILogger<AzureTtsService> logger,
        IVoiceCapabilityProvider voiceCapabilityProvider,
        IStylePresetProvider stylePresetProvider,
        ISsmlValidator ssmlValidator)
    {
        _options = options.Value;
        _logger = logger;
        _voiceCapabilityProvider = voiceCapabilityProvider;
        _stylePresetProvider = stylePresetProvider;
        _ssmlValidator = ssmlValidator;

        // Initialize SpeechConfig if subscription key is provided
        if (!string.IsNullOrWhiteSpace(_options.SubscriptionKey))
        {
            try
            {
                _speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
                _logger.LogInformation("Azure Speech service configured for region {Region}", _options.Region);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Speech service with region {Region}", _options.Region);
                _speechConfig = null;
            }
        }
        else
        {
            _logger.LogWarning("Azure Speech service not configured - SubscriptionKey is missing. TTS features will be disabled.");
        }
    }

    /// <inheritdoc/>
    public bool IsConfigured => _speechConfig != null;

    /// <inheritdoc/>
    public async Task<Stream> SynthesizeSpeechAsync(string text, Core.Models.TtsOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate parameters here to preserve backward-compatible error messages
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        if (text.Length > _options.MaxTextLength)
        {
            throw new ArgumentException($"Text length ({text.Length}) exceeds maximum allowed length ({_options.MaxTextLength}).", nameof(text));
        }

        return await SynthesizeSpeechAsync(text, options, Core.Enums.SynthesisMode.PlainText, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> SynthesizeSpeechAsync(
        string input,
        Core.Models.TtsOptions? options,
        Core.Enums.SynthesisMode mode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty.", nameof(input));
        }

        if (input.Length > _options.MaxTextLength)
        {
            throw new ArgumentException($"Input length ({input.Length}) exceeds maximum allowed length ({_options.MaxTextLength}).", nameof(input));
        }

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Azure Speech service is not configured. Configure SubscriptionKey in user secrets.");
        }

        // Determine actual mode if Auto
        var actualMode = mode;
        if (mode == Core.Enums.SynthesisMode.Auto)
        {
            var trimmedInput = input.TrimStart();
            actualMode = trimmedInput.StartsWith("<speak", StringComparison.OrdinalIgnoreCase)
                ? Core.Enums.SynthesisMode.Ssml
                : Core.Enums.SynthesisMode.PlainText;

            _logger.LogDebug("Auto-detected synthesis mode as {Mode}", actualMode);
        }

        string ssml;

        if (actualMode == Core.Enums.SynthesisMode.Ssml)
        {
            // Validate SSML
            var validationResult = _ssmlValidator.Validate(input);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("SSML validation failed with {ErrorCount} errors", validationResult.Errors.Count);
                throw new Core.Exceptions.SsmlValidationException(
                    "SSML validation failed. See Errors property for details.",
                    validationResult.Errors,
                    input);
            }

            ssml = input;
            _logger.LogInformation("Using provided SSML directly ({Length} characters)", input.Length);
        }
        else
        {
            // PlainText mode - use existing BuildSsml with options
            var ttsOptions = options ?? new Core.Models.TtsOptions
            {
                Voice = _options.DefaultVoice,
                Speed = _options.DefaultSpeed,
                Pitch = _options.DefaultPitch,
                Volume = _options.DefaultVolume
            };

            // Defensive validation: ensure voice is never empty
            if (string.IsNullOrWhiteSpace(ttsOptions.Voice))
            {
                _logger.LogWarning("Voice was null or empty, falling back to default: {DefaultVoice}",
                    _options.DefaultVoice);
                ttsOptions.Voice = string.IsNullOrWhiteSpace(_options.DefaultVoice)
                    ? "en-US-JennyNeural"
                    : _options.DefaultVoice;
            }

            ssml = BuildSsml(input, ttsOptions);
            _logger.LogInformation("Built SSML from plain text with voice {Voice} (speed: {Speed}, pitch: {Pitch}, volume: {Volume})",
                ttsOptions.Voice, ttsOptions.Speed, ttsOptions.Pitch, ttsOptions.Volume);
        }

        _logger.LogDebug("Final SSML: {Ssml}", ssml);

        // Start tracing activity for Azure Speech synthesis
        // For SSML mode, extract voice from SSML if possible, otherwise use "multiple" or "unknown"
        var voiceForTracing = actualMode == Core.Enums.SynthesisMode.Ssml
            ? ExtractVoiceFromSsml(ssml)
            : options?.Voice ?? _options.DefaultVoice;

        using var activity = BotActivitySource.StartAzureSpeechActivity(
            textLength: input.Length,
            voice: voiceForTracing,
            region: _options.Region);

        try
        {
            // Add synthesis mode to activity
            activity?.SetTag("tts.synthesis_mode", actualMode.ToString());

            // Create synthesizer with raw PCM output format (mono - we'll convert to stereo)
            // Azure Speech SDK outputs Raw48Khz16BitMonoPcm
            _speechConfig!.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);

            using var synthesizer = new SpeechSynthesizer(_speechConfig, null); // null = no audio output, we'll handle the stream

            // Synthesize speech from SSML
            var result = await synthesizer.SpeakSsmlAsync(ssml);

            // Record synthesis result
            activity?.SetTag(TracingConstants.Attributes.TtsSynthesisResult, result.Reason.ToString());

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("Speech synthesis completed successfully. Audio data size: {SizeBytes} bytes", result.AudioData.Length);

                // Record audio size
                activity?.SetTag(TracingConstants.Attributes.TtsAudioSizeBytes, result.AudioData.Length);

                // Convert mono PCM to stereo PCM for Discord
                var stereoData = ConvertMonoToStereo(result.AudioData);

                BotActivitySource.SetSuccess(activity);
                return new MemoryStream(stereoData);
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                _logger.LogError("Speech synthesis cancelled: {Reason} - {ErrorDetails}", cancellation.Reason, cancellation.ErrorDetails);

                // Record cancellation details
                activity?.SetTag(TracingConstants.Attributes.TtsCancellationReason, cancellation.Reason.ToString());

                var ex = new InvalidOperationException($"Speech synthesis failed: {cancellation.ErrorDetails}");
                BotActivitySource.RecordException(activity, ex);
                throw ex;
            }
            else
            {
                _logger.LogError("Speech synthesis failed with reason: {Reason}", result.Reason);
                var ex = new InvalidOperationException($"Speech synthesis failed: {result.Reason}");
                BotActivitySource.RecordException(activity, ex);
                throw ex;
            }
        }
        catch (Core.Exceptions.SsmlValidationException)
        {
            // Re-throw SSML validation exceptions without wrapping
            throw;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error during speech synthesis");
            BotActivitySource.RecordException(activity, ex);
            throw new InvalidOperationException("Speech synthesis failed. See inner exception for details.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<Core.Models.VoiceCapabilities?> GetVoiceCapabilitiesAsync(string voiceName)
    {
        return Task.FromResult(_voiceCapabilityProvider.GetCapabilities(voiceName));
    }

    /// <inheritdoc/>
    public IEnumerable<Core.Models.StylePreset> GetStylePresets()
    {
        return _stylePresetProvider.GetAllPresets();
    }

    /// <inheritdoc/>
    public Core.Models.SsmlValidationResult ValidateSsml(string ssml)
    {
        return _ssmlValidator.Validate(ssml);
    }

    /// <inheritdoc/>
    public IEnumerable<Core.Models.VoiceInfo> GetCuratedVoices()
    {
        return new List<Core.Models.VoiceInfo>
        {
            // English (US) - most popular only
            new() { ShortName = "en-US-JennyNeural", DisplayName = "Jenny", Locale = "en-US", Gender = "Female" },
            new() { ShortName = "en-US-GuyNeural", DisplayName = "Guy", Locale = "en-US", Gender = "Male" },
            new() { ShortName = "en-US-AriaNeural", DisplayName = "Aria", Locale = "en-US", Gender = "Female" },
            new() { ShortName = "en-US-DavisNeural", DisplayName = "Davis", Locale = "en-US", Gender = "Male" },
            new() { ShortName = "en-US-JaneNeural", DisplayName = "Jane", Locale = "en-US", Gender = "Female" },
            new() { ShortName = "en-US-JasonNeural", DisplayName = "Jason", Locale = "en-US", Gender = "Male" },

            // English (UK)
            new() { ShortName = "en-GB-SoniaNeural", DisplayName = "Sonia", Locale = "en-GB", Gender = "Female" },
            new() { ShortName = "en-GB-RyanNeural", DisplayName = "Ryan", Locale = "en-GB", Gender = "Male" },
            new() { ShortName = "en-GB-LibbyNeural", DisplayName = "Libby", Locale = "en-GB", Gender = "Female" },

            // Japanese
            new() { ShortName = "ja-JP-NanamiNeural", DisplayName = "Nanami", Locale = "ja-JP", Gender = "Female" },
            new() { ShortName = "ja-JP-KeitaNeural", DisplayName = "Keita", Locale = "ja-JP", Gender = "Male" },
            new() { ShortName = "ja-JP-MayuNeural", DisplayName = "Mayu", Locale = "ja-JP", Gender = "Female" },
            new() { ShortName = "ja-JP-NaokiNeural", DisplayName = "Naoki", Locale = "ja-JP", Gender = "Male" },

            // French
            new() { ShortName = "fr-FR-DeniseNeural", DisplayName = "Denise", Locale = "fr-FR", Gender = "Female" },
            new() { ShortName = "fr-FR-HenriNeural", DisplayName = "Henri", Locale = "fr-FR", Gender = "Male" },
            new() { ShortName = "fr-FR-BrigitteNeural", DisplayName = "Brigitte", Locale = "fr-FR", Gender = "Female" },

            // German
            new() { ShortName = "de-DE-KatjaNeural", DisplayName = "Katja", Locale = "de-DE", Gender = "Female" },
            new() { ShortName = "de-DE-ConradNeural", DisplayName = "Conrad", Locale = "de-DE", Gender = "Male" },

            // Italian
            new() { ShortName = "it-IT-ElsaNeural", DisplayName = "Elsa", Locale = "it-IT", Gender = "Female" },
            new() { ShortName = "it-IT-DiegoNeural", DisplayName = "Diego", Locale = "it-IT", Gender = "Male" },

            // Spanish
            new() { ShortName = "es-ES-ElviraNeural", DisplayName = "Elvira", Locale = "es-ES", Gender = "Female" },
            new() { ShortName = "es-ES-AlvaroNeural", DisplayName = "Alvaro", Locale = "es-ES", Gender = "Male" },
            new() { ShortName = "es-MX-DaliaNeural", DisplayName = "Dalia", Locale = "es-MX", Gender = "Female" },

            // Hindi (Indian)
            new() { ShortName = "hi-IN-SwaraNeural", DisplayName = "Swara", Locale = "hi-IN", Gender = "Female" },
            new() { ShortName = "hi-IN-MadhurNeural", DisplayName = "Madhur", Locale = "hi-IN", Gender = "Male" },

            // Chinese (Mandarin)
            new() { ShortName = "zh-CN-XiaoxiaoNeural", DisplayName = "Xiaoxiao", Locale = "zh-CN", Gender = "Female" },
            new() { ShortName = "zh-CN-YunxiNeural", DisplayName = "Yunxi", Locale = "zh-CN", Gender = "Male" },
            new() { ShortName = "zh-CN-YunyangNeural", DisplayName = "Yunyang", Locale = "zh-CN", Gender = "Male" },

            // Swedish
            new() { ShortName = "sv-SE-SofieNeural", DisplayName = "Sofie", Locale = "sv-SE", Gender = "Female" },
            new() { ShortName = "sv-SE-MattiasNeural", DisplayName = "Mattias", Locale = "sv-SE", Gender = "Male" },

            // Russian
            new() { ShortName = "ru-RU-SvetlanaNeural", DisplayName = "Svetlana", Locale = "ru-RU", Gender = "Female" },
            new() { ShortName = "ru-RU-DmitryNeural", DisplayName = "Dmitry", Locale = "ru-RU", Gender = "Male" },

            // Arabic
            new() { ShortName = "ar-SA-ZariyahNeural", DisplayName = "Zariyah", Locale = "ar-SA", Gender = "Female" },
            new() { ShortName = "ar-SA-HamedNeural", DisplayName = "Hamed", Locale = "ar-SA", Gender = "Male" },
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Core.Models.VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot retrieve voices - Azure Speech service is not configured");
            return Enumerable.Empty<Core.Models.VoiceInfo>();
        }

        var cacheKey = locale ?? "all";

        // Check cache first
        if (_voiceCache.TryGetValue(cacheKey, out var cachedVoices))
        {
            _logger.LogDebug("Returning {Count} voices from cache for locale '{Locale}'", cachedVoices.Count, cacheKey);
            return cachedVoices;
        }

        // Not in cache, fetch from Azure
        await _voiceCacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_voiceCache.TryGetValue(cacheKey, out cachedVoices))
            {
                return cachedVoices;
            }

            _logger.LogInformation("Fetching available voices from Azure Speech service for locale '{Locale}'", cacheKey);

            // Start tracing activity for voice retrieval
            using var activity = BotActivitySource.StartGetVoicesActivity(locale);

            try
            {
                using var synthesizer = new SpeechSynthesizer(_speechConfig!, null);
                var result = await synthesizer.GetVoicesAsync(locale);

                if (result.Reason == ResultReason.VoicesListRetrieved)
                {
                    var voices = result.Voices
                        .Select(v => new Core.Models.VoiceInfo
                        {
                            ShortName = v.ShortName,
                            DisplayName = v.LocalName,
                            Locale = v.Locale,
                            Gender = v.Gender.ToString()
                        })
                        .ToList();

                    _logger.LogInformation("Retrieved {Count} voices for locale '{Locale}'", voices.Count, cacheKey);

                    // Record voice count
                    activity?.SetTag("tts.voices_retrieved", voices.Count);

                    // Cache the results
                    _voiceCache[cacheKey] = voices;

                    BotActivitySource.SetSuccess(activity);
                    return voices;
                }
                else
                {
                    _logger.LogError("Failed to retrieve voices. Reason: {Reason}", result.Reason);
                    activity?.SetTag("tts.retrieval_failed", result.Reason.ToString());
                    BotActivitySource.SetSuccess(activity); // Not an error, just no voices
                    return Enumerable.Empty<Core.Models.VoiceInfo>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available voices for locale '{Locale}'", locale);
                BotActivitySource.RecordException(activity, ex);
                return Enumerable.Empty<Core.Models.VoiceInfo>();
            }
        }
        finally
        {
            _voiceCacheLock.Release();
        }
    }

    /// <summary>
    /// Extracts the voice name from SSML for tracing purposes.
    /// </summary>
    /// <param name="ssml">SSML markup.</param>
    /// <returns>Voice name if found, "multiple" if multiple voices, or "unknown" if not found.</returns>
    private static string ExtractVoiceFromSsml(string ssml)
    {
        try
        {
            // Simple regex to find voice name attribute
            var matches = System.Text.RegularExpressions.Regex.Matches(ssml, @"<voice\s+name=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (matches.Count == 0)
            {
                return "unknown";
            }
            else if (matches.Count == 1)
            {
                return matches[0].Groups[1].Value;
            }
            else
            {
                return "multiple";
            }
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Builds SSML markup for speech synthesis with the specified options.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="options">TTS options for voice, speed, pitch, and volume.</param>
    /// <returns>SSML string.</returns>
    private static string BuildSsml(string text, Core.Models.TtsOptions options)
    {
        // Escape XML special characters in the text
        var escapedText = System.Security.SecurityElement.Escape(text);

        // Convert pitch from multiplier (0.5-1.5) to percentage (-50% to +50%)
        var pitchPercent = (options.Pitch - 1.0) * 100.0;
        var pitchStr = pitchPercent >= 0 ? $"+{pitchPercent:F0}%" : $"{pitchPercent:F0}%";

        // Convert speed multiplier to rate string
        var speedStr = options.Speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        // Convert volume (0.0-1.0) to percentage (0-100)
        var volumePercent = (int)(options.Volume * 100);

        var ssml = new StringBuilder();
        ssml.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">");
        ssml.AppendLine($"  <voice name=\"{options.Voice}\">");
        ssml.AppendLine($"    <prosody rate=\"{speedStr}\" pitch=\"{pitchStr}\" volume=\"{volumePercent}\">");
        ssml.AppendLine($"      {escapedText}");
        ssml.AppendLine("    </prosody>");
        ssml.AppendLine("  </voice>");
        ssml.AppendLine("</speak>");

        return ssml.ToString();
    }

    /// <summary>
    /// Converts mono PCM audio to stereo by duplicating each sample (interleaving left and right channels).
    /// </summary>
    /// <param name="monoData">Mono PCM data (16-bit samples).</param>
    /// <returns>Stereo PCM data (16-bit samples, interleaved).</returns>
    /// <remarks>
    /// Input: Mono 48kHz 16-bit PCM (2 bytes per sample)
    /// Output: Stereo 48kHz 16-bit PCM (4 bytes per frame - 2 bytes left, 2 bytes right)
    /// Discord expects stereo, so we duplicate each mono sample for both channels.
    /// </remarks>
    private byte[] ConvertMonoToStereo(byte[] monoData)
    {
        // Start activity for audio conversion
        using var activity = BotActivitySource.StartAudioConversionActivity(
            fromFormat: "mono_48khz_16bit",
            toFormat: "stereo_48khz_16bit",
            bytesIn: monoData.Length);

        try
        {
            // Each sample is 2 bytes (16-bit). For stereo, we need to duplicate each sample.
            var stereoData = new byte[monoData.Length * 2];

            for (int i = 0; i < monoData.Length; i += 2)
            {
                // Get the mono sample (2 bytes)
                var sampleLow = monoData[i];
                var sampleHigh = monoData[i + 1];

                // Write to left channel
                stereoData[i * 2] = sampleLow;
                stereoData[i * 2 + 1] = sampleHigh;

                // Write to right channel (duplicate)
                stereoData[i * 2 + 2] = sampleLow;
                stereoData[i * 2 + 3] = sampleHigh;
            }

            _logger.LogDebug("Converted mono audio ({MonoBytes} bytes) to stereo ({StereoBytes} bytes)",
                monoData.Length, stereoData.Length);

            // Record output size
            activity?.SetTag("audio.bytes_out", stereoData.Length);
            BotActivitySource.SetSuccess(activity);

            return stereoData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting mono to stereo");
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
