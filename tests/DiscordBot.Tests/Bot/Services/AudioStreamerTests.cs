using System.Diagnostics;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Audio;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Tests for <see cref="AudioStreamer"/>'s handling of the FFmpeg process when playback is cancelled.
/// </summary>
public class AudioStreamerTests
{
    [Fact]
    public async Task StreamAsync_CancelledWhileFfmpegStillWriting_CompletesWithoutWaitingForFfmpeg()
    {
        // A real process stands in for FFmpeg: it writes to stdout forever and never closes
        // stderr, so it blocks as soon as the stdout pipe is full. When playback was cancelled
        // between two reads, the streamer stopped reading stdout and then waited for stderr to
        // close, which never happens: the play hung and froze the guild's queue.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            // xUnit v2 has no runtime skip; this needs /bin/sh and /dev/zero.
            return;
        }

        using var playback = new CancellationTokenSource();
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList = { "-c", "exec cat /dev/zero" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        })!;

        try
        {
            var session = new FfmpegTranscodeSession
            {
                Process = process,
                // Cancel playback right after the second read has returned data, which is the
                // window where the loop sees the cancellation and breaks out instead of throwing.
                OutputStream = new CancelAfterReadsStream(process.StandardOutput.BaseStream, playback, cancelAfterRead: 2),
                Arguments = "test"
            };

            var transcoder = new Mock<IFfmpegTranscoder>();
            transcoder.Setup(t => t.StartTranscode(It.IsAny<string>(), It.IsAny<AudioFilter>())).Returns(session);

            var streamer = new AudioStreamer(
                transcoder.Object,
                new Mock<ISoundCacheService>().Object,
                new Mock<IAudioNotifier>().Object,
                NullLogger<AudioStreamer>.Instance,
                Options.Create(new AudioCacheOptions { Enabled = false }));

            var sound = new Sound { Id = Guid.NewGuid(), Name = "endless", FileName = "endless.mp3", DurationSeconds = 10 };
            var stream = streamer.StreamAsync(1UL, sound, "/nonexistent/endless.mp3", AudioFilter.BassBoost, new SinkStream(), playback.Token);

            var finished = await Task.WhenAny(stream, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

            finished.Should().BeSameAs(stream, "a cancelled play must not wait on an FFmpeg that is blocked writing");
            var result = await stream.ConfigureAwait(false);
            result.WasCancelled.Should().BeTrue();
            result.Success.Should().BeTrue();
            transcoder.Verify(
                t => t.StartTranscode(It.IsAny<string>(), It.IsAny<AudioFilter>()),
                Times.Once,
                "a cancelled play must not be retried without the filter");
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already disposed by the streamer.
            }
        }
    }

    [Fact]
    public async Task StreamAsync_FfmpegEndsNormally_ReportsSuccess()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList = { "-c", "head -c 38400 /dev/zero; echo 'a warning' >&2" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        })!;

        var transcoder = new Mock<IFfmpegTranscoder>();
        transcoder
            .Setup(t => t.StartTranscode(It.IsAny<string>(), It.IsAny<AudioFilter>()))
            .Returns(new FfmpegTranscodeSession { Process = process, OutputStream = process.StandardOutput.BaseStream, Arguments = "test" });

        var streamer = new AudioStreamer(
            transcoder.Object,
            new Mock<ISoundCacheService>().Object,
            new Mock<IAudioNotifier>().Object,
            NullLogger<AudioStreamer>.Instance,
            Options.Create(new AudioCacheOptions { Enabled = false }));

        var sound = new Sound { Id = Guid.NewGuid(), Name = "short", FileName = "short.mp3", DurationSeconds = 1 };
        var result = await streamer
            .StreamAsync(1UL, sound, "/nonexistent/short.mp3", AudioFilter.None, new SinkStream(), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Success.Should().BeTrue();
        result.WasCancelled.Should().BeFalse();
    }

    /// <summary>
    /// Discards what is written. Unlike <see cref="Stream.Null"/>, it ignores the token on flush,
    /// so the streamer cannot depend on its output stream throwing to get out of a cancelled play.
    /// </summary>
    private sealed class SinkStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }

    /// <summary>
    /// Reads from an inner stream and cancels a token once a given number of reads have returned.
    /// </summary>
    private sealed class CancelAfterReadsStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationTokenSource _toCancel;
        private readonly int _cancelAfterRead;
        private int _reads;

        public CancelAfterReadsStream(Stream inner, CancellationTokenSource toCancel, int cancelAfterRead)
        {
            _inner = inner;
            _toCancel = toCancel;
            _cancelAfterRead = cancelAfterRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).ConfigureAwait(false);
            if (++_reads == _cancelAfterRead)
            {
                _toCancel.Cancel();
            }

            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
