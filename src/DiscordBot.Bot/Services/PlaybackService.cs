using System.Collections.Concurrent;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Orchestrates soundboard audio playback: queue management, playback loop,
/// and coordination between the audio service and the audio streamer.
/// Thread-safe using per-guild locks and concurrent dictionaries.
/// </summary>
public class PlaybackService : IPlaybackService
{
    private readonly IAudioService _audioService;
    private readonly IAudioStreamer _audioStreamer;
    private readonly IAudioNotifier _audioNotifier;
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
    /// <param name="audioStreamer">The audio streamer for cache/FFmpeg routing and streaming.</param>
    /// <param name="audioNotifier">The audio notifier for SignalR broadcasts.</param>
    /// <param name="serviceScopeFactory">The service scope factory for resolving scoped services.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">Soundboard configuration options.</param>
    public PlaybackService(
        IAudioService audioService,
        IAudioStreamer audioStreamer,
        IAudioNotifier audioNotifier,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PlaybackService> logger,
        IOptions<SoundboardOptions> options)
    {
        _audioService = audioService;
        _audioStreamer = audioStreamer;
        _audioNotifier = audioNotifier;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task PlayAsync(ulong guildId, Sound sound, bool queueEnabled, AudioFilter filter = AudioFilter.None, string? requestedByDisplayName = null, CancellationToken cancellationToken = default)
    {
        using var scope = BotActivitySource.StartServiceActivityWithApm(
            "playback",
            "play",
            guildId: guildId,
            entityId: sound.Id.ToString());
        var activity = scope.Activity;

        try
        {
            // Validate audio client exists
            var audioClient = _audioService.GetAudioClient(guildId);
            if (audioClient == null)
            {
                var ex = new InvalidOperationException($"No audio client available for guild {guildId}. Join a voice channel first.");
                _logger.LogError(ex, "Cannot play sound {SoundName} - not connected to voice channel in guild {GuildId}",
                    sound.Name, guildId);
                scope.RecordException(ex);
                throw ex;
            }

            // Validate file exists
            var filePath = Path.Combine(_options.BasePath, guildId.ToString(), sound.FileName);
            if (!File.Exists(filePath))
            {
                var ex = new FileNotFoundException($"Sound file not found: {filePath}", filePath);
                _logger.LogError(ex, "Sound file not found for sound {SoundId} ({SoundName}) in guild {GuildId}",
                    sound.Id, sound.Name, guildId);
                scope.RecordException(ex);
                throw ex;
            }

            _logger.LogInformation("Queueing sound {SoundName} ({SoundId}) for playback in guild {GuildId} (QueueMode: {QueueEnabled}, Filter: {Filter})",
                sound.Name, sound.Id, guildId, queueEnabled, filter);

            activity?.SetTag("sound.id", sound.Id.ToString());
            activity?.SetTag("sound.name", sound.Name);
            activity?.SetTag("sound.file_size_bytes", sound.FileSizeBytes);
            activity?.SetTag("sound.duration_seconds", sound.DurationSeconds);
            activity?.SetTag("playback.queue_enabled", queueEnabled);
            activity?.SetTag("playback.filter", filter.ToString());

            var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
            await guildLock.WaitAsync(cancellationToken);

            try
            {
                var state = _playbackStates.GetOrAdd(guildId, _ => new PlaybackState());

                if (queueEnabled)
                {
                    // Queue mode: Add to queue
                    state.Queue.Enqueue(new QueuedSound(sound, filter, requestedByDisplayName));
                    _logger.LogDebug("Added sound {SoundName} to queue (position {QueuePosition}) in guild {GuildId} with filter {Filter}",
                        sound.Name, state.Queue.Count, guildId, filter);

                    // Broadcast queue update
                    BroadcastQueueUpdate(guildId, state);
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

                    state.Queue.Enqueue(new QueuedSound(sound, filter, requestedByDisplayName));

                    // Broadcast queue update
                    BroadcastQueueUpdate(guildId, state);
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

            scope.SetSuccess();
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Unexpected error queueing sound {SoundId} for playback in guild {GuildId}",
                sound.Id, guildId);
            scope.RecordException(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        using var scope = BotActivitySource.StartServiceActivityWithApm(
            "playback",
            "stop",
            guildId: guildId);
        var activity = scope.Activity;

        try
        {
            var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
            await guildLock.WaitAsync(cancellationToken);

            try
            {
                if (!_playbackStates.TryGetValue(guildId, out var state))
                {
                    _logger.LogDebug("No playback state for guild {GuildId}, nothing to stop", guildId);
                    scope.SetSuccess();
                    return;
                }

                if (state.IsPlaying)
                {
                    _logger.LogInformation("Stopping playback and clearing queue in guild {GuildId}", guildId);
                    state.CancellationTokenSource?.Cancel();

                    // Notify clients that playback has finished so now-playing UI clears.
                    // Natural completion notifications are handled in PlaySoundAsync;
                    // StopAsync owns the cancellation-path notification.
                    if (state.CurrentSound != null)
                    {
                        _ = _audioNotifier.NotifyPlaybackFinishedAsync(
                            guildId, state.CurrentSound.Id, wasCancelled: true, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogWarning("StopAsync: IsPlaying=true but CurrentSound is null for guild {GuildId}; PlaybackFinished not sent", guildId);
                    }
                }

                state.Queue.Clear();

                // Broadcast queue update (empty queue)
                BroadcastQueueUpdate(guildId, state);

                activity?.SetTag("playback.queue_cleared", true);
            }
            finally
            {
                guildLock.Release();
            }

            scope.SetSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping playback in guild {GuildId}", guildId);
            scope.RecordException(ex);
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

    /// <inheritdoc/>
    public async Task<bool> RemoveFromQueueAsync(ulong guildId, int position, CancellationToken cancellationToken = default)
    {
        using var scope = BotActivitySource.StartServiceActivityWithApm(
            "playback",
            "remove_from_queue",
            guildId: guildId);
        var activity = scope.Activity;

        try
        {
            if (position < 0)
            {
                _logger.LogWarning("Invalid queue position {Position} for guild {GuildId}", position, guildId);
                scope.SetSuccess();
                return false;
            }

            var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
            await guildLock.WaitAsync(cancellationToken);

            try
            {
                if (!_playbackStates.TryGetValue(guildId, out var state))
                {
                    _logger.LogDebug("No playback state for guild {GuildId}, nothing to remove", guildId);
                    return false;
                }

                // Position 0 with active playback means skip current sound
                if (position == 0 && state.IsPlaying)
                {
                    _logger.LogInformation("Skipping current sound in guild {GuildId}", guildId);
                    state.CancellationTokenSource?.Cancel();
                    activity?.SetTag("playback.skipped_current", true);
                    scope.SetSuccess();
                    return true;
                }

                // Convert to queue array for position-based removal
                var queueList = state.Queue.ToList();
                if (position >= queueList.Count)
                {
                    _logger.LogWarning("Queue position {Position} out of range (queue size: {QueueSize}) for guild {GuildId}",
                        position, queueList.Count, guildId);
                    return false;
                }

                var removedItem = queueList[position];
                queueList.RemoveAt(position);

                // Rebuild the queue
                state.Queue.Clear();
                foreach (var queuedSound in queueList)
                {
                    state.Queue.Enqueue(queuedSound);
                }

                _logger.LogInformation("Removed sound {SoundName} from queue position {Position} in guild {GuildId}",
                    removedItem.Sound.Name, position, guildId);

                activity?.SetTag("playback.removed_position", position);
                activity?.SetTag("sound.name", removedItem.Sound.Name);

                // Broadcast queue update
                BroadcastQueueUpdate(guildId, state);
            }
            finally
            {
                guildLock.Release();
            }

            scope.SetSuccess();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from queue at position {Position} in guild {GuildId}", position, guildId);
            scope.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Builds FFmpeg command line arguments for audio playback with optional filter.
    /// Delegates to <see cref="Audio.FfmpegTranscoder"/> but kept here for backward compatibility with tests.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="filter">The audio filter to apply.</param>
    /// <returns>FFmpeg arguments string.</returns>
    internal static string BuildFfmpegArguments(string filePath, AudioFilter filter)
    {
        var filterString = AudioFilters.GetFfmpegFilter(filter);

        if (string.IsNullOrEmpty(filterString))
        {
            // No filter - standard transcoding
            return $"-hide_banner -loglevel warning -i \"{filePath}\" -ac 2 -f s16le -ar 48000 pipe:1";
        }

        // With filter - insert -af between input and output format
        return $"-hide_banner -loglevel warning -i \"{filePath}\" -af \"{filterString}\" -ac 2 -f s16le -ar 48000 pipe:1";
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

        using var loopScope = BotActivitySource.StartBackgroundServiceActivityWithApm(
            "PlaybackLoop",
            executionCycle: 0,
            correlationId: null,
            asRootSpan: true);
        loopScope.ApmTransaction?.SetLabel("guild_id", guildId.ToString());

        var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        var loopFailed = false;

        try
        {
            while (true)
            {
                QueuedSound? queuedSound = null;

                // Get next sound from queue
                await guildLock.WaitAsync();

                try
                {
                    if (state.Queue.Count == 0)
                    {
                        // Queue empty, stop playback loop
                        state.IsPlaying = false;
                        state.CurrentSound = null;
                        state.CurrentRequestedByDisplayName = null;
                        state.CancellationTokenSource?.Dispose();
                        state.CancellationTokenSource = null;
                        _logger.LogDebug("Playback queue empty, stopping playback loop for guild {GuildId}", guildId);
                        loopScope.SetSuccess();
                        return;
                    }

                    queuedSound = state.Queue.Dequeue();
                    state.IsPlaying = true;
                    state.CurrentSound = queuedSound.Sound;
                    state.CurrentRequestedByDisplayName = queuedSound.RequestedByDisplayName;
                    state.CancellationTokenSource?.Dispose();
                    state.CancellationTokenSource = new CancellationTokenSource();

                    // Broadcast queue update (sound dequeued)
                    BroadcastQueueUpdate(guildId, state);
                }
                finally
                {
                    guildLock.Release();
                }

                if (queuedSound == null)
                {
                    continue;
                }

                // Play the sound
                try
                {
                    await PlaySoundAsync(guildId, queuedSound.Sound, queuedSound.Filter, queuedSound.RequestedByDisplayName, state.CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Playback cancelled for sound {SoundName} in guild {GuildId}",
                        queuedSound.Sound.Name, guildId);
                    // Continue to next sound or exit loop
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error playing sound {SoundName} ({SoundId}) in guild {GuildId}",
                        queuedSound.Sound.Name, queuedSound.Sound.Id, guildId);
                    // Continue to next sound despite error
                }
            }
        }
        catch (Exception ex)
        {
            loopFailed = true;
            loopScope.RecordException(ex);
            throw;
        }
        finally
        {
            if (!loopFailed)
                loopScope.SetSuccess();

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
    /// Plays a single sound file by delegating to the audio streamer.
    /// </summary>
    private async Task PlaySoundAsync(ulong guildId, Sound sound, AudioFilter filter, string? requestedByDisplayName, CancellationToken cancellationToken)
    {
        using var scope = BotActivitySource.StartServiceActivityWithApm(
            "playback",
            "play_sound",
            guildId: guildId,
            entityId: sound.Id.ToString());
        var activity = scope.Activity;

        var wasCancelled = false;

        try
        {
            var filePath = Path.Combine(_options.BasePath, guildId.ToString(), sound.FileName);

            if (filter != AudioFilter.None)
            {
                _logger.LogInformation("Starting playback of sound {SoundName} ({SoundId}) in guild {GuildId} with filter {Filter}",
                    sound.Name, sound.Id, guildId, filter);
            }
            else
            {
                _logger.LogInformation("Starting playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                    sound.Name, sound.Id, guildId);
            }

            activity?.SetTag("sound.id", sound.Id.ToString());
            activity?.SetTag("sound.name", sound.Name);
            activity?.SetTag("sound.file_path", filePath);
            activity?.SetTag("playback.filter", filter.ToString());

            // Update play count (use scoped service)
            using (var serviceScope = _serviceScopeFactory.CreateScope())
            {
                var soundService = serviceScope.ServiceProvider.GetRequiredService<ISoundService>();
                await soundService.IncrementPlayCountAsync(sound.Id, cancellationToken);
            }

            // Update last activity on voice connection
            _audioService.UpdateLastActivity(guildId);

            // Broadcast PlaybackStarted event
            _ = _audioNotifier.NotifyPlaybackStartedAsync(guildId, sound.Id, sound.Name, sound.DurationSeconds, requestedByDisplayName, cancellationToken: cancellationToken);

            // Get persistent PCM stream (created once per connection, reused for all playback)
            var discord = _audioService.GetOrCreatePcmStream(guildId);
            if (discord == null)
            {
                throw new InvalidOperationException($"Failed to get PCM stream for guild {guildId}");
            }

            // Delegate streaming to the audio streamer
            var result = await _audioStreamer.StreamAsync(guildId, sound, filePath, filter, discord, cancellationToken);
            wasCancelled = result.WasCancelled;

            // Broadcast PlaybackFinished event for natural completion only.
            // Cancellation-path notifications are handled by StopAsync to avoid double-fire.
            if (!wasCancelled)
            {
                _ = _audioNotifier.NotifyPlaybackFinishedAsync(guildId, sound.Id, wasCancelled: false, CancellationToken.None);
            }

            _logger.LogInformation("Completed playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);

            scope.SetSuccess();
        }
        catch (OperationCanceledException ex)
        {
            scope.RecordException(ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);
            scope.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Broadcasts a queue update notification to subscribed clients.
    /// </summary>
    private void BroadcastQueueUpdate(ulong guildId, PlaybackState state)
    {
        var queueItems = state.Queue
            .Select((queuedSound, index) => new QueueItemDto
            {
                Position = index,
                SoundId = queuedSound.Sound.Id,
                Name = queuedSound.Sound.Name,
                DurationSeconds = queuedSound.Sound.DurationSeconds
            })
            .ToList();

        var queueDto = new QueueUpdatedDto
        {
            GuildId = guildId,
            Queue = queueItems
        };

        _ = _audioNotifier.NotifyQueueUpdatedAsync(guildId, queueDto);
    }

    /// <summary>
    /// Represents a queued sound with its optional audio filter.
    /// </summary>
    private record QueuedSound(Sound Sound, AudioFilter Filter, string? RequestedByDisplayName = null);

    /// <summary>
    /// Represents the playback state for a guild.
    /// </summary>
    private class PlaybackState
    {
        /// <summary>
        /// Queue of sounds to play with their filters.
        /// </summary>
        public Queue<QueuedSound> Queue { get; } = new();

        /// <summary>
        /// Whether a sound is currently playing.
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Cancellation token source for the current playback.
        /// Used to stop playback when a new sound should replace the current one.
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        /// <summary>
        /// The currently playing sound (used for notifications).
        /// </summary>
        public Sound? CurrentSound { get; set; }

        /// <summary>
        /// The display name of the user who requested the currently playing sound.
        /// </summary>
        public string? CurrentRequestedByDisplayName { get; set; }
    }
}
