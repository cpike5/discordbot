using System.Collections.Concurrent;
using System.Text;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTtsService"/> class.
    /// </summary>
    /// <param name="options">Azure Speech configuration options.</param>
    /// <param name="logger">The logger.</param>
    public AzureTtsService(
        IOptions<AzureSpeechOptions> options,
        ILogger<AzureTtsService> logger)
    {
        _options = options.Value;
        _logger = logger;

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
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        if (text.Length > _options.MaxTextLength)
        {
            throw new ArgumentException($"Text length ({text.Length}) exceeds maximum allowed length ({_options.MaxTextLength}).", nameof(text));
        }

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Azure Speech service is not configured. Configure SubscriptionKey in user secrets.");
        }

        // Use provided options or create defaults
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

        _logger.LogInformation("Synthesizing speech: {TextLength} characters with voice {Voice} (speed: {Speed}, pitch: {Pitch}, volume: {Volume})",
            text.Length, ttsOptions.Voice, ttsOptions.Speed, ttsOptions.Pitch, ttsOptions.Volume);

        try
        {
            // Build SSML for synthesis
            var ssml = BuildSsml(text, ttsOptions);
            _logger.LogDebug("SSML: {Ssml}", ssml);

            // Create synthesizer with raw PCM output format (mono - we'll convert to stereo)
            // Azure Speech SDK outputs Raw48Khz16BitMonoPcm
            _speechConfig!.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);

            using var synthesizer = new SpeechSynthesizer(_speechConfig, null); // null = no audio output, we'll handle the stream

            // Synthesize speech from SSML
            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("Speech synthesis completed successfully. Audio data size: {SizeBytes} bytes", result.AudioData.Length);

                // Convert mono PCM to stereo PCM for Discord
                var stereoData = ConvertMonoToStereo(result.AudioData);

                return new MemoryStream(stereoData);
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                _logger.LogError("Speech synthesis cancelled: {Reason} - {ErrorDetails}", cancellation.Reason, cancellation.ErrorDetails);

                throw new InvalidOperationException($"Speech synthesis failed: {cancellation.ErrorDetails}");
            }
            else
            {
                _logger.LogError("Speech synthesis failed with reason: {Reason}", result.Reason);
                throw new InvalidOperationException($"Speech synthesis failed: {result.Reason}");
            }
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error during speech synthesis");
            throw new InvalidOperationException("Speech synthesis failed. See inner exception for details.", ex);
        }
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

                // Cache the results
                _voiceCache[cacheKey] = voices;

                return voices;
            }
            else
            {
                _logger.LogError("Failed to retrieve voices. Reason: {Reason}", result.Reason);
                return Enumerable.Empty<Core.Models.VoiceInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available voices for locale '{Locale}'", locale);
            return Enumerable.Empty<Core.Models.VoiceInfo>();
        }
        finally
        {
            _voiceCacheLock.Release();
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

        return stereoData;
    }
}
