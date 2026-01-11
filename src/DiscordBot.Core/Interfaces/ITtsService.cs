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
    /// Gets available voices for the specified locale.
    /// </summary>
    /// <param name="locale">The locale to filter voices by (e.g., "en-US"). Pass null for all locales.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of available voice information.</returns>
    Task<IEnumerable<VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the TTS service is configured and available.
    /// </summary>
    /// <returns>True if the service is properly configured, false otherwise.</returns>
    bool IsConfigured { get; }
}
