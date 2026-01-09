using System.Collections.Concurrent;
using System.Diagnostics;
using Discord.Audio;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for soundboard audio playback using FFmpeg to stream audio to Discord voice channels.
/// Supports both queue mode (sounds queue up) and replace mode (new sound replaces current).
/// Thread-safe using per-guild locks and concurrent dictionaries.
/// </summary>
public class PlaybackService : IPlaybackService
{
    private readonly IAudioService _audioService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PlaybackService> _logger;
    private readonly SoundboardOptions _options;

    // Per-guild playback state
    private readonly ConcurrentDictionary<ulong, PlaybackState> _playbackStates = new();

    // Per-guild locks for thread-safe playback control
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackService"/> class.
    /// </summary>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="serviceScopeFactory">The service scope factory for resolving scoped services.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">Soundboard configuration options.</param>
    public PlaybackService(
        IAudioService audioService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PlaybackService> logger,
        IOptions<SoundboardOptions> options)
    {
        _audioService = audioService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task PlayAsync(ulong guildId, Sound sound, bool queueEnabled, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "playback",
            "play",
            guildId: guildId,
            entityId: sound.Id.ToString());

        try
        {
            // Validate audio client exists
            var audioClient = _audioService.GetAudioClient(guildId);
            if (audioClient == null)
            {
                var ex = new InvalidOperationException($"No audio client available for guild {guildId}. Join a voice channel first.");
                _logger.LogError(ex, "Cannot play sound {SoundName} - not connected to voice channel in guild {GuildId}",
                    sound.Name, guildId);
                BotActivitySource.RecordException(activity, ex);
                throw ex;
            }

            // Validate file exists
            var filePath = Path.Combine(_options.BasePath, guildId.ToString(), sound.FileName);
            if (!File.Exists(filePath))
            {
                var ex = new FileNotFoundException($"Sound file not found: {filePath}", filePath);
                _logger.LogError(ex, "Sound file not found for sound {SoundId} ({SoundName}) in guild {GuildId}",
                    sound.Id, sound.Name, guildId);
                BotActivitySource.RecordException(activity, ex);
                throw ex;
            }

            _logger.LogInformation("Queueing sound {SoundName} ({SoundId}) for playback in guild {GuildId} (QueueMode: {QueueEnabled})",
                sound.Name, sound.Id, guildId, queueEnabled);

            activity?.SetTag("sound.id", sound.Id.ToString());
            activity?.SetTag("sound.name", sound.Name);
            activity?.SetTag("sound.file_size_bytes", sound.FileSizeBytes);
            activity?.SetTag("sound.duration_seconds", sound.DurationSeconds);
            activity?.SetTag("playback.queue_enabled", queueEnabled);

            var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
            await guildLock.WaitAsync(cancellationToken);

            try
            {
                var state = _playbackStates.GetOrAdd(guildId, _ => new PlaybackState());

                if (queueEnabled)
                {
                    // Queue mode: Add to queue
                    state.Queue.Enqueue(sound);
                    _logger.LogDebug("Added sound {SoundName} to queue (position {QueuePosition}) in guild {GuildId}",
                        sound.Name, state.Queue.Count, guildId);
                }
                else
                {
                    // Replace mode: Stop current playback and clear queue
                    if (state.IsPlaying)
                    {
                        _logger.LogInformation("Stopping current playback to replace with {SoundName} in guild {GuildId}",
                            sound.Name, guildId);
                        state.CancellationTokenSource?.Cancel();
                        state.Queue.Clear();
                    }

                    state.Queue.Enqueue(sound);
                }

                // Start playback loop if not already running
                if (!state.IsPlaying)
                {
                    _logger.LogDebug("Starting playback loop for guild {GuildId}", guildId);
                    _ = PlaybackLoopAsync(guildId);
                }
            }
            finally
            {
                guildLock.Release();
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Unexpected error queueing sound {SoundId} for playback in guild {GuildId}",
                sound.Id, guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "playback",
            "stop",
            guildId: guildId);

        try
        {
            var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
            await guildLock.WaitAsync(cancellationToken);

            try
            {
                if (!_playbackStates.TryGetValue(guildId, out var state))
                {
                    _logger.LogDebug("No playback state for guild {GuildId}, nothing to stop", guildId);
                    BotActivitySource.SetSuccess(activity);
                    return;
                }

                if (state.IsPlaying)
                {
                    _logger.LogInformation("Stopping playback and clearing queue in guild {GuildId}", guildId);
                    state.CancellationTokenSource?.Cancel();
                }

                state.Queue.Clear();

                activity?.SetTag("playback.queue_cleared", true);
            }
            finally
            {
                guildLock.Release();
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping playback in guild {GuildId}", guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public bool IsPlaying(ulong guildId)
    {
        return _playbackStates.TryGetValue(guildId, out var state) && state.IsPlaying;
    }

    /// <inheritdoc/>
    public int GetQueueLength(ulong guildId)
    {
        if (!_playbackStates.TryGetValue(guildId, out var state))
        {
            return 0;
        }

        // Don't count the currently playing sound
        var queueLength = state.Queue.Count;
        if (state.IsPlaying && queueLength > 0)
        {
            queueLength--;
        }

        return Math.Max(0, queueLength);
    }

    /// <summary>
    /// Background playback loop that processes the queue for a guild.
    /// Runs until the queue is empty, then stops.
    /// </summary>
    private async Task PlaybackLoopAsync(ulong guildId)
    {
        if (!_playbackStates.TryGetValue(guildId, out var state))
        {
            return;
        }

        var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

        try
        {
            while (true)
            {
                Sound? sound = null;

                // Get next sound from queue
                await guildLock.WaitAsync();

                try
                {
                    if (state.Queue.Count == 0)
                    {
                        // Queue empty, stop playback loop
                        state.IsPlaying = false;
                        state.CancellationTokenSource?.Dispose();
                        state.CancellationTokenSource = null;
                        _logger.LogDebug("Playback queue empty, stopping playback loop for guild {GuildId}", guildId);
                        return;
                    }

                    sound = state.Queue.Dequeue();
                    state.IsPlaying = true;
                    state.CancellationTokenSource?.Dispose();
                    state.CancellationTokenSource = new CancellationTokenSource();
                }
                finally
                {
                    guildLock.Release();
                }

                if (sound == null)
                {
                    continue;
                }

                // Play the sound
                try
                {
                    await PlaySoundAsync(guildId, sound, state.CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Playback cancelled for sound {SoundName} in guild {GuildId}",
                        sound.Name, guildId);
                    // Continue to next sound or exit loop
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error playing sound {SoundName} ({SoundId}) in guild {GuildId}",
                        sound.Name, sound.Id, guildId);
                    // Continue to next sound despite error
                }
            }
        }
        finally
        {
            // Clean up state when loop exits
            await guildLock.WaitAsync();
            try
            {
                state.IsPlaying = false;
                state.CancellationTokenSource?.Dispose();
                state.CancellationTokenSource = null;
            }
            finally
            {
                guildLock.Release();
            }
        }
    }

    /// <summary>
    /// Plays a single sound file using FFmpeg to transcode to Opus PCM.
    /// </summary>
    private async Task PlaySoundAsync(ulong guildId, Sound sound, CancellationToken cancellationToken)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "playback",
            "play_sound",
            guildId: guildId,
            entityId: sound.Id.ToString());

        try
        {
            var filePath = Path.Combine(_options.BasePath, guildId.ToString(), sound.FileName);

            _logger.LogInformation("Starting playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);

            activity?.SetTag("sound.id", sound.Id.ToString());
            activity?.SetTag("sound.name", sound.Name);
            activity?.SetTag("sound.file_path", filePath);

            // Update play count (use scoped service)
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var soundService = scope.ServiceProvider.GetRequiredService<ISoundService>();
                await soundService.IncrementPlayCountAsync(sound.Id, cancellationToken);
            }

            // Update last activity on voice connection
            _audioService.UpdateLastActivity(guildId);

            // Determine FFmpeg executable path - handle null or empty string
            // If not configured, look for ffmpeg in the application's base directory first, then fall back to PATH
            string ffmpegPath;
            if (!string.IsNullOrWhiteSpace(_options.FfmpegPath))
            {
                ffmpegPath = _options.FfmpegPath;
            }
            else
            {
                var localFfmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                ffmpegPath = File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
            }

            _logger.LogDebug("Starting FFmpeg from path '{FfmpegPath}' for file '{FilePath}'", ffmpegPath, filePath);

            // Start FFmpeg process
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -loglevel panic -i \"{filePath}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var ffmpeg = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start FFmpeg process from '{ffmpegPath}'");

            _logger.LogDebug("FFmpeg process started (PID: {ProcessId}) for sound {SoundName} in guild {GuildId}",
                ffmpeg.Id, sound.Name, guildId);

            // Get persistent PCM stream (created once per connection, reused for all playback)
            var discord = _audioService.GetOrCreatePcmStream(guildId);
            if (discord == null)
            {
                throw new InvalidOperationException($"Failed to get PCM stream for guild {guildId}");
            }

            // Stream audio data to Discord
            using (var output = ffmpeg.StandardOutput.BaseStream)
            {
                await output.CopyToAsync(discord, 81920, cancellationToken);
                await discord.FlushAsync(cancellationToken);
            }

            _logger.LogInformation("Completed playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Represents the playback state for a guild.
    /// </summary>
    private class PlaybackState
    {
        /// <summary>
        /// Queue of sounds to play.
        /// </summary>
        public Queue<Sound> Queue { get; } = new();

        /// <summary>
        /// Whether a sound is currently playing.
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Cancellation token source for the current playback.
        /// Used to stop playback when a new sound should replace the current one.
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource { get; set; }
    }
}
