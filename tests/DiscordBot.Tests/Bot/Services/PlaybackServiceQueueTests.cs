using System.Collections.Concurrent;
using System.Reflection;
using Discord.Audio;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Tests for <see cref="PlaybackService"/>'s playback loop ownership and queue numbering.
/// The audio streamer is replaced by a fake whose streams stay open until the test ends them
/// (or the playback token is cancelled), so the test decides when each sound finishes.
/// </summary>
public sealed class PlaybackServiceQueueTests : IDisposable
{
    private const ulong GuildId = 424242UL;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _basePath;
    private readonly FakeStreamer _streamer = new();
    private readonly ConcurrentQueue<QueueUpdatedDto> _queueBroadcasts = new();
    private readonly PlaybackService _service;

    public PlaybackServiceQueueTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "playback-queue-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_basePath, GuildId.ToString()));

        var audioService = new Mock<IAudioService>();
        audioService.Setup(a => a.GetAudioClient(GuildId)).Returns(new Mock<IAudioClient>().Object);
        audioService.Setup(a => a.GetOrCreatePcmStream(GuildId)).Returns(new Mock<AudioOutStream>().Object);

        var notifier = new Mock<IAudioNotifier>();
        notifier
            .Setup(n => n.NotifyQueueUpdatedAsync(GuildId, It.IsAny<QueueUpdatedDto>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, QueueUpdatedDto, CancellationToken>((_, dto, _) => _queueBroadcasts.Enqueue(dto))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => new Mock<ISoundService>().Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _service = new PlaybackService(
            audioService.Object,
            _streamer,
            notifier.Object,
            scopeFactory,
            NullLogger<PlaybackService>.Instance,
            Options.Create(new SoundboardOptions { BasePath = _basePath }));
    }

    public void Dispose()
    {
        _streamer.ReleaseAll();
        try
        {
            Directory.Delete(_basePath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the directory is under the temp path.
        }
    }

    [Fact]
    public async Task PlayAsync_TwoCallsWaitingBeforeTheLoopStarts_StartOnlyOneLoop()
    {
        // Two PlayAsync calls both waiting on the guild lock before the first loop has taken it.
        // Both used to see IsPlaying == false and start a loop each: the second loop dequeued B
        // while A was still streaming, disposed A's token and played B over it, so Skip stopped
        // only one of the two sounds.
        var soundA = CreateSound("a");
        var soundB = CreateSound("b");
        var guildLock = GetGuildLock();

        await guildLock.WaitAsync().ConfigureAwait(false);
        var playA = _service.PlayAsync(GuildId, soundA, queueEnabled: true);
        var playB = _service.PlayAsync(GuildId, soundB, queueEnabled: true);
        guildLock.Release();
        await Task.WhenAll(playA, playB).WaitAsync(Timeout).ConfigureAwait(false);

        // Every loop started above queued on the lock before PlayAsync(B) released it, so once
        // the test holds the lock again each of them has taken its first sound.
        // Each dequeue broadcasts the queue under the lock, so the last broadcast is current.
        await guildLock.WaitAsync().ConfigureAwait(false);
        List<QueueItemDto> waiting;
        try
        {
            waiting = _queueBroadcasts.Last().Queue;
        }
        finally
        {
            guildLock.Release();
        }

        waiting.Should().ContainSingle("only one loop should be running, so B still waits behind A")
            .Which.SoundId.Should().Be(soundB.Id);

        var streamA = await _streamer.WaitForStartAsync(0).ConfigureAwait(false);
        streamA.Sound.Should().BeSameAs(soundA);

        // Skip the playing sound: A's own token is cancelled and B plays next, alone.
        (await _service.RemoveFromQueueAsync(GuildId, 0).ConfigureAwait(false)).Should().BeTrue();
        streamA.Token.IsCancellationRequested.Should().BeTrue("Skip must cancel the token the playing sound was given");

        var streamB = await _streamer.WaitForStartAsync(1).ConfigureAwait(false);
        streamB.Sound.Should().BeSameAs(soundB);
        streamB.Token.IsCancellationRequested.Should().BeFalse();
        _streamer.MaxConcurrent.Should().Be(1, "two sounds must never stream at once");

        streamB.Finish();
        await WaitUntilAsync(() => !_service.IsPlaying(GuildId)).ConfigureAwait(false);
    }

    [Fact]
    public async Task PlayAsync_AfterTheLoopDrainsTheQueue_StartsANewLoopThatSkipCanStop()
    {
        // A sound played after the previous loop has emptied the queue gets a fresh loop whose
        // token is the one Skip cancels.
        var soundA = CreateSound("a");
        var soundB = CreateSound("b");

        await _service.PlayAsync(GuildId, soundA, queueEnabled: true).ConfigureAwait(false);
        (await _streamer.WaitForStartAsync(0).ConfigureAwait(false)).Finish();
        await WaitUntilAsync(() => !_service.IsPlaying(GuildId)).ConfigureAwait(false);

        await _service.PlayAsync(GuildId, soundB, queueEnabled: true).ConfigureAwait(false);
        var streamB = await _streamer.WaitForStartAsync(1).ConfigureAwait(false);

        (await _service.RemoveFromQueueAsync(GuildId, 0).ConfigureAwait(false)).Should().BeTrue();
        streamB.Token.IsCancellationRequested.Should().BeTrue();
        await WaitUntilAsync(() => !_service.IsPlaying(GuildId)).ConfigureAwait(false);
    }

    [Fact]
    public async Task RemoveFromQueueAsync_FirstWaitingPosition_RemovesItAndKeepsTheCurrentSoundPlaying()
    {
        var playing = CreateSound("playing");
        var waiting = CreateSound("waiting");

        await _service.PlayAsync(GuildId, playing, queueEnabled: true).ConfigureAwait(false);
        var current = await _streamer.WaitForStartAsync(0).ConfigureAwait(false);
        await _service.PlayAsync(GuildId, waiting, queueEnabled: true).ConfigureAwait(false);

        // The panel numbers waiting sounds from the broadcast and sends that number back.
        var lastBroadcast = _queueBroadcasts.Last();
        lastBroadcast.Queue.Should().ContainSingle();
        var broadcastPosition = lastBroadcast.Queue[0].Position;
        broadcastPosition.Should().Be(1, "position 0 means the sound that is playing");

        var removed = await _service.RemoveFromQueueAsync(GuildId, broadcastPosition).ConfigureAwait(false);

        removed.Should().BeTrue();
        current.Token.IsCancellationRequested.Should().BeFalse("removing a waiting sound must not skip the playing one");
        _service.GetQueueLength(GuildId).Should().Be(0);
        _queueBroadcasts.Last().Queue.Should().BeEmpty();

        current.Finish();
        await WaitUntilAsync(() => !_service.IsPlaying(GuildId)).ConfigureAwait(false);
        _streamer.StartedCount.Should().Be(1, "the removed sound must never play");
    }

    [Fact]
    public async Task RemoveFromQueueAsync_PositionPastTheLastWaitingSound_ReturnsFalse()
    {
        await _service.PlayAsync(GuildId, CreateSound("playing"), queueEnabled: true).ConfigureAwait(false);
        var current = await _streamer.WaitForStartAsync(0).ConfigureAwait(false);
        await _service.PlayAsync(GuildId, CreateSound("waiting"), queueEnabled: true).ConfigureAwait(false);

        (await _service.RemoveFromQueueAsync(GuildId, 2).ConfigureAwait(false)).Should().BeFalse();
        _service.GetQueueLength(GuildId).Should().Be(1, "the playing sound is not in the queue; one sound waits");
        current.Token.IsCancellationRequested.Should().BeFalse();
    }

    private Sound CreateSound(string name)
    {
        var fileName = name + "-" + Guid.NewGuid().ToString("N") + ".mp3";
        File.WriteAllBytes(Path.Combine(_basePath, GuildId.ToString(), fileName), new byte[] { 0 });
        return new Sound { Id = Guid.NewGuid(), GuildId = GuildId, Name = name, FileName = fileName, DurationSeconds = 1 };
    }

    private SemaphoreSlim GetGuildLock()
    {
        var field = typeof(PlaybackService).GetField("_guildLocks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var locks = (ConcurrentDictionary<ulong, SemaphoreSlim>)field.GetValue(_service)!;
        return locks.GetOrAdd(GuildId, _ => new SemaphoreSlim(1, 1));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    /// <summary>One call to the fake streamer: the sound, the token it was given, and a way to end it.</summary>
    private sealed class StreamCall
    {
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StreamCall(Sound sound, CancellationToken token)
        {
            Sound = sound;
            Token = token;
        }

        public Sound Sound { get; }

        public CancellationToken Token { get; }

        public Task Finished => _finished.Task;

        public void Finish() => _finished.TrySetResult();
    }

    /// <summary>
    /// Stands in for FFmpeg: each stream stays open until the test finishes it or its token is
    /// cancelled, and the fake records how many streams were open at the same time.
    /// </summary>
    private sealed class FakeStreamer : IAudioStreamer
    {
        private readonly object _gate = new();
        private readonly List<StreamCall> _calls = new();
        private int _active;
        private int _maxConcurrent;

        public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

        public int StartedCount
        {
            get
            {
                lock (_gate)
                {
                    return _calls.Count;
                }
            }
        }

        public async Task<AudioStreamResult> StreamAsync(
            ulong guildId, Sound sound, string filePath, AudioFilter filter, Stream discord, CancellationToken cancellationToken)
        {
            var call = new StreamCall(sound, cancellationToken);
            var active = Interlocked.Increment(ref _active);
            int max;
            while (active > (max = Volatile.Read(ref _maxConcurrent)))
            {
                Interlocked.CompareExchange(ref _maxConcurrent, active, max);
            }

            lock (_gate)
            {
                _calls.Add(call);
            }

            try
            {
                var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => cancelled.TrySetResult()))
                {
                    await Task.WhenAny(call.Finished, cancelled.Task).ConfigureAwait(false);
                }

                return new AudioStreamResult { Success = true, WasCancelled = cancellationToken.IsCancellationRequested };
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public async Task<StreamCall> WaitForStartAsync(int index)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (true)
            {
                lock (_gate)
                {
                    if (_calls.Count > index)
                    {
                        return _calls[index];
                    }
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"Stream {index} never started.");
                }

                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        public void ReleaseAll()
        {
            lock (_gate)
            {
                foreach (var call in _calls)
                {
                    call.Finish();
                }
            }
        }
    }
}
