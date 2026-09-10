using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="LlmUsageRecorder"/> — the bounded-channel non-blocking write path,
/// matching <c>AuditLogQueueTests</c>'s coverage of <c>AuditLogQueue</c>.
/// </summary>
public class LlmUsageRecorderTests
{
    private static LlmUsageRecorder CreateRecorder(int capacity = 10000)
    {
        return new LlmUsageRecorder(
            Options.Create(new LlmOptions { UsageQueueCapacity = capacity }),
            Mock.Of<ILogger<LlmUsageRecorder>>());
    }

    private static LlmUsageRecord CreateRecord(ulong userId = 1) => new()
    {
        Timestamp = DateTime.UtcNow,
        Mode = LlmMode.GuildAssistant,
        UserId = userId,
        Model = "test/model"
    };

    [Fact]
    public void Record_EnqueuesRecord()
    {
        var recorder = CreateRecorder();

        recorder.Record(CreateRecord());

        recorder.Count.Should().Be(1);
    }

    [Fact]
    public async Task Record_ThenDequeueAsync_ReturnsTheSameRecord()
    {
        var recorder = CreateRecorder();
        var record = CreateRecord(userId: 42);

        recorder.Record(record);
        var dequeued = await recorder.DequeueAsync();

        dequeued.UserId.Should().Be(42);
    }

    [Fact]
    public void Record_NullRecord_Throws()
    {
        var recorder = CreateRecorder();

        var act = () => recorder.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Record_BeyondCapacity_DropsOldest_AndKeepsCountAtCapacity()
    {
        var recorder = CreateRecorder(capacity: 3);

        recorder.Record(CreateRecord(userId: 1));
        recorder.Record(CreateRecord(userId: 2));
        recorder.Record(CreateRecord(userId: 3));
        recorder.Record(CreateRecord(userId: 4)); // Should drop userId=1

        recorder.Count.Should().Be(3);
    }

    [Fact]
    public async Task Record_BeyondCapacity_OldestRecordIsGone()
    {
        var recorder = CreateRecorder(capacity: 2);

        recorder.Record(CreateRecord(userId: 1));
        recorder.Record(CreateRecord(userId: 2));
        recorder.Record(CreateRecord(userId: 3)); // Drops userId=1

        var first = await recorder.DequeueAsync();
        var second = await recorder.DequeueAsync();

        first.UserId.Should().Be(2);
        second.UserId.Should().Be(3);
    }

    [Fact]
    public void Record_WithinCapacity_DoesNotCountAsDropped()
    {
        var recorder = CreateRecorder(capacity: 3);

        recorder.Record(CreateRecord(userId: 1));
        recorder.Record(CreateRecord(userId: 2));

        recorder.TotalDropped.Should().Be(0);
    }

    [Fact]
    public void Record_BeyondCapacity_IncrementsTotalDropped()
    {
        var recorder = CreateRecorder(capacity: 2);

        recorder.Record(CreateRecord(userId: 1));
        recorder.Record(CreateRecord(userId: 2));
        recorder.Record(CreateRecord(userId: 3)); // 1st drop
        recorder.Record(CreateRecord(userId: 4)); // 2nd drop

        recorder.TotalDropped.Should().Be(2);
    }

    [Fact]
    public void Record_BeyondCapacity_LogsWarningOnFirstDrop()
    {
        var mockLogger = new Mock<ILogger<LlmUsageRecorder>>();
        var recorder = new LlmUsageRecorder(
            Options.Create(new LlmOptions { UsageQueueCapacity = 1 }),
            mockLogger.Object);

        recorder.Record(CreateRecord(userId: 1));
        recorder.Record(CreateRecord(userId: 2)); // Drops userId=1, should log

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("queue is full")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Record_ManyDropsBetweenLoggedOnes_OnlyLogsAtRateLimitedIntervals()
    {
        var mockLogger = new Mock<ILogger<LlmUsageRecorder>>();
        var recorder = new LlmUsageRecorder(
            Options.Create(new LlmOptions { UsageQueueCapacity = 1 }),
            mockLogger.Object);

        // First record fills capacity; the next 150 each cause one drop (150 total drops).
        // Expected log lines: drop #1 and drop #100 - two total, not 150.
        recorder.Record(CreateRecord(userId: 0));
        for (var i = 1; i <= 150; i++)
        {
            recorder.Record(CreateRecord(userId: (ulong)i));
        }

        recorder.TotalDropped.Should().Be(150);

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("queue is full")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }
}
