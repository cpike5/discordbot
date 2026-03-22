using System.Diagnostics;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Constants;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Audio;

/// <summary>
/// Handles streaming audio data to a Discord PCM output stream.
/// Routes between cached audio and FFmpeg transcoding, manages progress
/// notifications, and implements filter fallback on FFmpeg errors.
/// </summary>
public class AudioStreamer : IAudioStreamer
{
    private readonly IFfmpegTranscoder _transcoder;
    private readonly ISoundCacheService _soundCacheService;
    private readonly IAudioNotifier _audioNotifier;
    private readonly ILogger<AudioStreamer> _logger;
    private readonly AudioCacheOptions _cacheOptions;

    /// <summary>
    /// 20ms of audio at 48kHz stereo 16-bit PCM.
    /// </summary>
    private const int BufferSize = 3840;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioStreamer"/> class.
    /// </summary>
    /// <param name="transcoder">The FFmpeg transcoder for starting transcode sessions.</param>
    /// <param name="soundCacheService">The audio cache service for caching FFmpeg-processed audio.</param>
    /// <param name="audioNotifier">The audio notifier for SignalR progress broadcasts.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cacheOptions">Audio cache configuration options.</param>
    public AudioStreamer(
        IFfmpegTranscoder transcoder,
        ISoundCacheService soundCacheService,
        IAudioNotifier audioNotifier,
        ILogger<AudioStreamer> logger,
        IOptions<AudioCacheOptions> cacheOptions)
    {
        _transcoder = transcoder;
        _soundCacheService = soundCacheService;
        _audioNotifier = audioNotifier;
        _logger = logger;
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<AudioStreamResult> StreamAsync(
        ulong guildId,
        Sound sound,
        string filePath,
        AudioFilter filter,
        Stream discord,
        CancellationToken cancellationToken)
    {
        var durationSeconds = sound.DurationSeconds;

        // Try to play with filter, fall back to unfiltered if filter causes error
        var (success, filterFailed, wasCancelled) = await StreamAudioAsync(
            guildId, sound, filePath, filter, discord, durationSeconds, cancellationToken);

        // If filter failed and we were using one, retry without filter
        if (!success && filterFailed && filter != AudioFilter.None)
        {
            _logger.LogWarning("Filter {Filter} failed for sound {SoundName}, retrying without filter",
                filter, sound.Name);

            (success, _, wasCancelled) = await StreamAudioAsync(
                guildId, sound, filePath, AudioFilter.None, discord, durationSeconds, cancellationToken);
        }

        if (!success && !wasCancelled)
        {
            throw new InvalidOperationException($"FFmpeg playback failed for sound {sound.Name}");
        }

        return new AudioStreamResult
        {
            Success = success,
            WasCancelled = wasCancelled
        };
    }

    /// <summary>
    /// Streams audio from cache or FFmpeg to Discord.
    /// </summary>
    /// <returns>A tuple of (success, filterFailed, wasCancelled).</returns>
    private async Task<(bool Success, bool FilterFailed, bool WasCancelled)> StreamAudioAsync(
        ulong guildId,
        Sound sound,
        string filePath,
        AudioFilter filter,
        Stream discord,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        // Check if audio is eligible for caching (based on duration)
        var shouldCache = _cacheOptions.Enabled && durationSeconds <= _cacheOptions.MaxCacheDurationSeconds;

        // Get source file modification time for cache invalidation
        var sourceFileModifiedUtc = File.GetLastWriteTimeUtc(filePath);

        // Try to get cached audio first
        if (shouldCache)
        {
            var cachedStream = await _soundCacheService.TryGetAsync(sound.Id, filter, sourceFileModifiedUtc);
            if (cachedStream != null)
            {
                _logger.LogDebug("Playing sound {SoundName} from cache in guild {GuildId}", sound.Name, guildId);
                return await StreamFromCacheAsync(guildId, sound, cachedStream, discord, durationSeconds, cancellationToken);
            }
        }

        // Cache miss - transcode with FFmpeg
        return await StreamFromFfmpegAsync(guildId, sound, filePath, filter, discord, durationSeconds, sourceFileModifiedUtc, shouldCache, cancellationToken);
    }

    /// <summary>
    /// Streams audio from a cached file to Discord.
    /// </summary>
    private async Task<(bool Success, bool FilterFailed, bool WasCancelled)> StreamFromCacheAsync(
        ulong guildId,
        Sound sound,
        Stream cachedStream,
        Stream discord,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        using var streamScope = BotActivitySource.StartSoundboardStreamActivityWithApm(
            guildId: guildId,
            soundId: sound.Id);
        var streamActivity = streamScope.Activity;

        streamActivity?.SetTag("audio.source", "cache");

        var buffer = new byte[BufferSize];
        int bytesRead;
        long totalBytesRead = 0;
        var wasCancelled = false;

        var playbackStartTime = Stopwatch.GetTimestamp();
        var lastProgressBroadcast = playbackStartTime;
        const long progressBroadcastIntervalTicks = TimeSpan.TicksPerSecond;

        try
        {
            int bufferCount = 0;

            while ((bytesRead = await cachedStream.ReadAsync(buffer, 0, BufferSize, cancellationToken)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Playback cancelled for sound {SoundName} in guild {GuildId}", sound.Name, guildId);
                    wasCancelled = true;
                    break;
                }

                await discord.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;
                bufferCount++;

                BroadcastProgressIfDue(
                    ref lastProgressBroadcast, playbackStartTime, progressBroadcastIntervalTicks,
                    guildId, sound.Id, durationSeconds, cancellationToken);
            }

            await discord.FlushAsync(cancellationToken);

            BotActivitySource.RecordAudioStreamMetrics(
                streamActivity,
                bytesWritten: totalBytesRead,
                bufferCount: bufferCount);

            streamScope.SetSuccess();
            return (true, false, wasCancelled);
        }
        catch (OperationCanceledException)
        {
            streamScope.RecordException(new OperationCanceledException("Playback cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            streamScope.RecordException(ex);
            throw;
        }
        finally
        {
            await cachedStream.DisposeAsync();
        }
    }

    /// <summary>
    /// Streams audio from FFmpeg to Discord, optionally caching the output.
    /// </summary>
    private async Task<(bool Success, bool FilterFailed, bool WasCancelled)> StreamFromFfmpegAsync(
        ulong guildId,
        Sound sound,
        string filePath,
        AudioFilter filter,
        Stream discord,
        double durationSeconds,
        DateTime sourceFileModifiedUtc,
        bool shouldCache,
        CancellationToken cancellationToken)
    {
        // Start activity for FFmpeg transcode
        using var transcodeScope = BotActivitySource.StartFfmpegTranscodeActivityWithApm(
            soundName: sound.Name,
            filePath: Path.GetFileName(filePath), // Relative path only for security
            filter: filter.ToString());
        var transcodeActivity = transcodeScope.Activity;

        transcodeActivity?.SetTag("audio.source", "ffmpeg");

        using var session = _transcoder.StartTranscode(filePath, filter);

        _logger.LogDebug("FFmpeg process started (PID: {ProcessId}) for sound {SoundName} in guild {GuildId} with filter {Filter}",
            session.ProcessId, sound.Name, guildId, filter);

        // Record FFmpeg process ID
        transcodeActivity?.SetTag(TracingConstants.Attributes.FfmpegProcessId, session.ProcessId);

        var buffer = new byte[BufferSize];
        int bytesRead;
        long totalBytesRead = 0;
        var wasCancelled = false;

        // Buffer for caching (only allocate if we're caching)
        MemoryStream? cacheBuffer = shouldCache ? new MemoryStream() : null;

        var playbackStartTime = Stopwatch.GetTimestamp();
        var lastProgressBroadcast = playbackStartTime;
        const long progressBroadcastIntervalTicks = TimeSpan.TicksPerSecond;

        // Start child activity for audio streaming
        using var streamScope = BotActivitySource.StartSoundboardStreamActivityWithApm(
            guildId: guildId,
            soundId: sound.Id);
        var streamActivity = streamScope.Activity;

        try
        {
            int bufferCount = 0;

            while ((bytesRead = await session.OutputStream.ReadAsync(buffer, 0, BufferSize, cancellationToken)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Playback cancelled for sound {SoundName} in guild {GuildId}", sound.Name, guildId);
                    wasCancelled = true;
                    break;
                }

                await discord.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;
                bufferCount++;

                // Capture for cache if enabled
                cacheBuffer?.Write(buffer, 0, bytesRead);

                BroadcastProgressIfDue(
                    ref lastProgressBroadcast, playbackStartTime, progressBroadcastIntervalTicks,
                    guildId, sound.Id, durationSeconds, cancellationToken);
            }

            await discord.FlushAsync(cancellationToken);

            // Record streaming metrics
            BotActivitySource.RecordAudioStreamMetrics(
                streamActivity,
                bytesWritten: totalBytesRead,
                bufferCount: bufferCount);

            streamScope.SetSuccess();
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            cacheBuffer?.Dispose();
            cacheBuffer = null; // Don't cache cancelled playbacks
            streamScope.RecordException(new OperationCanceledException("Playback cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            cacheBuffer?.Dispose();
            cacheBuffer = null; // Don't cache failed playbacks
            streamScope.RecordException(ex);
            throw;
        }

        // Check for FFmpeg errors (only non-zero exit code is a real failure;
        // FFmpeg writes harmless warnings like "Estimating duration from bitrate" to stderr)
        var errorOutput = await session.ReadErrorOutputAsync();
        var hasError = session.ExitCode != 0;

        // Record FFmpeg completion
        BotActivitySource.RecordFfmpegDetails(
            transcodeActivity,
            processId: session.ProcessId,
            exitCode: session.ExitCode,
            arguments: session.Arguments);

        // Log FFmpeg stderr output even on success (as debug) for diagnostics
        if (!string.IsNullOrWhiteSpace(errorOutput) && !hasError)
        {
            _logger.LogDebug("FFmpeg stderr for sound {SoundName} in guild {GuildId} (exit code 0): {ErrorOutput}",
                sound.Name, guildId, errorOutput);
        }

        if (hasError)
        {
            _logger.LogWarning("FFmpeg errors for sound {SoundName} in guild {GuildId} (exit code {ExitCode}): {ErrorOutput}",
                sound.Name, guildId, session.ExitCode, errorOutput);

            // Truncate error output for trace attribute
            transcodeActivity?.SetTag("ffmpeg.error_output", errorOutput.Length > 256 ? errorOutput[..256] : errorOutput);

            // If we got very little data and had a filter, it's likely the filter caused the failure
            var filterFailed = filter != AudioFilter.None && totalBytesRead < BufferSize * 10; // Less than ~200ms of audio

            if (filterFailed)
            {
                transcodeActivity?.SetTag("ffmpeg.filter_failed", true);
            }

            cacheBuffer?.Dispose(); // Don't cache failed transcodes
            transcodeScope.SetSuccess(); // Mark as handled (not an unhandled error)
            return (false, filterFailed, wasCancelled);
        }

        // Cache the successfully transcoded audio (fire and forget)
        if (cacheBuffer != null && !wasCancelled)
        {
            var pcmData = cacheBuffer.ToArray();
            cacheBuffer.Dispose();

            _ = Task.Run(async () =>
            {
                try
                {
                    var cached = await _soundCacheService.StoreAsync(sound.Id, filter, pcmData, sourceFileModifiedUtc);
                    if (cached)
                    {
                        _logger.LogDebug("Cached transcoded audio for sound {SoundName} ({SizeBytes} bytes)", sound.Name, pcmData.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache audio for sound {SoundName}", sound.Name);
                }
            }, CancellationToken.None);
        }
        else
        {
            cacheBuffer?.Dispose();
        }

        transcodeScope.SetSuccess();
        return (true, false, wasCancelled);
    }

    /// <summary>
    /// Broadcasts playback progress via SignalR if enough time has elapsed since the last broadcast.
    /// </summary>
    private void BroadcastProgressIfDue(
        ref long lastProgressBroadcast,
        long playbackStartTime,
        long progressBroadcastIntervalTicks,
        ulong guildId,
        Guid soundId,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var currentTime = Stopwatch.GetTimestamp();
        var elapsedSinceLastBroadcast = currentTime - lastProgressBroadcast;
        if (elapsedSinceLastBroadcast >= progressBroadcastIntervalTicks && durationSeconds > 0)
        {
            var elapsedTotalSeconds = Stopwatch.GetElapsedTime(playbackStartTime).TotalSeconds;
            var positionSeconds = Math.Min(elapsedTotalSeconds, durationSeconds);

            _ = _audioNotifier.NotifyPlaybackProgressAsync(
                guildId, soundId, positionSeconds, durationSeconds, cancellationToken);

            lastProgressBroadcast = currentTime;
        }
    }
}
