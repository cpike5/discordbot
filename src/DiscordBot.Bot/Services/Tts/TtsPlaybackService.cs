using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs.Tts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Service for TTS playback orchestration.
/// Consolidates PCM streaming, duration calculation, history logging, and activity tracking.
/// </summary>
public class TtsPlaybackService : ITtsPlaybackService
{
    private readonly IAudioService _audioService;
    private readonly ITtsHistoryService _ttsHistoryService;
    private readonly ILogger<TtsPlaybackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TtsPlaybackService"/> class.
    /// </summary>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="ttsHistoryService">The TTS history service for logging messages.</param>
    /// <param name="logger">The logger.</param>
    public TtsPlaybackService(
        IAudioService audioService,
        ITtsHistoryService ttsHistoryService,
        ILogger<TtsPlaybackService> logger)
    {
        _audioService = audioService;
        _ttsHistoryService = ttsHistoryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TtsPlaybackResult> PlayAsync(
        ulong guildId,
        ulong userId,
        string username,
        string message,
        string voice,
        Stream audioStream,
        CancellationToken cancellationToken = default)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(audioStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);

        // Calculate duration from audio stream
        var durationSeconds = CalculateAudioDuration(audioStream);

        // Reset stream position to ensure we read from the beginning
        audioStream.Position = 0;

        // Get the PCM stream for playback
        var pcmStream = _audioService.GetOrCreatePcmStream(guildId);
        if (pcmStream == null)
        {
            _logger.LogError("Failed to get PCM stream for guild {GuildId}", guildId);
            return new TtsPlaybackResult
            {
                Success = false,
                ErrorMessage = "Failed to get audio stream. Please try reconnecting to the voice channel.",
                DurationSeconds = durationSeconds
            };
        }

        // Stream the audio to Discord
        try
        {
            // Start activity for Discord audio streaming
            using var streamActivity = BotActivitySource.StartDiscordAudioStreamActivity(
                guildId: guildId,
                durationSeconds: durationSeconds);

            try
            {
                var bytesWritten = 0L;
                var buffer = new byte[3840]; // 20ms at 48kHz stereo 16-bit
                int bytesRead;

                while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await pcmStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    bytesWritten += bytesRead;
                }

                await pcmStream.FlushAsync(cancellationToken);

                // Record streaming metrics
                BotActivitySource.RecordAudioStreamMetrics(
                    streamActivity,
                    bytesWritten: bytesWritten,
                    bufferCount: (int)(bytesWritten / 3840));

                // Update activity to prevent auto-leave
                _audioService.UpdateLastActivity(guildId);

                _logger.LogInformation("Successfully played TTS message for guild {GuildId}. Bytes written: {BytesWritten}",
                    guildId, bytesWritten);

                BotActivitySource.SetSuccess(streamActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
                BotActivitySource.RecordException(streamActivity, ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
            return new TtsPlaybackResult
            {
                Success = false,
                ErrorMessage = "An error occurred while streaming audio to Discord.",
                DurationSeconds = durationSeconds
            };
        }

        // Record in history
        var ttsMessage = new TtsMessage
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            Username = username,
            Message = message,
            Voice = voice,
            DurationSeconds = durationSeconds,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _ttsHistoryService.LogMessageAsync(ttsMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log TTS message to history for guild {GuildId}", guildId);
            // Don't fail the playback if history logging fails
        }

        return new TtsPlaybackResult
        {
            Success = true,
            DurationSeconds = durationSeconds,
            LoggedMessage = ttsMessage
        };
    }

    /// <summary>
    /// Calculates the duration of PCM audio stream in seconds.
    /// </summary>
    /// <param name="audioStream">The audio stream (48kHz, 16-bit, stereo PCM).</param>
    /// <returns>Duration in seconds.</returns>
    /// <remarks>
    /// PCM format: 48kHz sample rate, 16-bit (2 bytes per sample), stereo (2 channels).
    /// Bytes per second = 48000 samples/sec * 2 bytes/sample * 2 channels = 192000 bytes/sec.
    /// </remarks>
    private static double CalculateAudioDuration(Stream audioStream)
    {
        if (!audioStream.CanSeek)
        {
            throw new ArgumentException("Audio stream must support seeking", nameof(audioStream));
        }

        const int sampleRate = 48000;
        const int bytesPerSample = 2; // 16-bit
        const int channels = 2; // stereo
        const int bytesPerSecond = sampleRate * bytesPerSample * channels; // 192000

        return audioStream.Length / (double)bytesPerSecond;
    }
}
