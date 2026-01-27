using DiscordBot.Core.Enums;
using DiscordBot.Core.Models;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for text-to-speech synthesis.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Synthesizes speech from text and returns the audio as a stream.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="options">TTS options (voice, speed, pitch, volume).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the synthesized audio in PCM format (48kHz, 16-bit, stereo).</returns>
    /// <exception cref="ArgumentException">Thrown when text is null, empty, or exceeds max length.</exception>
    /// <exception cref="InvalidOperationException">Thrown when TTS service is not configured.</exception>
    Task<Stream> SynthesizeSpeechAsync(string text, TtsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes speech from text or SSML and returns the audio as a stream.
    /// </summary>
    /// <param name="input">The text or SSML to synthesize.</param>
    /// <param name="options">TTS options (voice, speed, pitch, volume). Only used for PlainText mode.</param>
    /// <param name="mode">The synthesis mode (PlainText, Ssml, or Auto).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the synthesized audio in PCM format (48kHz, 16-bit, stereo).</returns>
    /// <exception cref="ArgumentException">Thrown when input is null, empty, or exceeds max length.</exception>
    /// <exception cref="InvalidOperationException">Thrown when TTS service is not configured.</exception>
    /// <exception cref="Exceptions.SsmlValidationException">Thrown when SSML validation fails.</exception>
    Task<Stream> SynthesizeSpeechAsync(
        string input,
        TtsOptions? options,
        SynthesisMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available voices for the specified locale.
    /// </summary>
    /// <param name="locale">The locale to filter voices by (e.g., "en-US"). Pass null for all locales.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of available voice information.</returns>
    Task<IEnumerable<VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities for a specific voice.
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural").</param>
    /// <returns>Voice capabilities if known, null if voice is unknown.</returns>
    Task<VoiceCapabilities?> GetVoiceCapabilitiesAsync(string voiceName);

    /// <summary>
    /// Gets all available style presets.
    /// </summary>
    /// <returns>Collection of style presets for voice configuration.</returns>
    IEnumerable<StylePreset> GetStylePresets();

    /// <summary>
    /// Validates SSML markup.
    /// </summary>
    /// <param name="ssml">SSML markup to validate.</param>
    /// <returns>Validation result with details about errors, warnings, and detected voices.</returns>
    SsmlValidationResult ValidateSsml(string ssml);

    /// <summary>
    /// Checks if the TTS service is configured and available.
    /// </summary>
    /// <returns>True if the service is properly configured, false otherwise.</returns>
    bool IsConfigured { get; }

    /// <summary>
    /// Gets the curated list of popular TTS voices across multiple languages.
    /// This list is consistent between the Discord /tts command and the web TTS Portal.
    /// </summary>
    /// <returns>Collection of curated voice information ordered by locale and name.</returns>
    IEnumerable<VoiceInfo> GetCuratedVoices();
}
