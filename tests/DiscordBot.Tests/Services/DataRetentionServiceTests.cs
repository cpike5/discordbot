using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for <see cref="DataRetentionService"/>. The startup and inter-batch delays run on a
/// <see cref="FakeTimeProvider"/>, as in <c>AssistantInteractionLogRetentionServiceTests</c>, so a
/// sweep can be driven without waiting on the real clock. Waits poll with
/// <c>ConfigureAwait(false)</c> per CLAUDE.md's background-service test rules.
/// </summary>
public class DataRetentionServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _providerMock = new();
    private readonly Mock<ICommandLogRepository> _commandLogs = new();
    private readonly Mock<IUserActivityEventRepository> _activityEvents = new();
    private readonly Mock<IConnectionEventRepository> _connectionEvents = new();
    private readonly Mock<ITtsMessageRepository> _ttsMessages = new();
    private readonly Mock<IAssistantUsageMetricsRepository> _usageMetrics = new();
    private readonly Mock<IAudioPlaybackLogRepository> _playbackLogs = new();
    private readonly Mock<ILogger<DataRetentionService>> _loggerMock = new();
    private readonly FakeTimeProvider _fakeTime = new();

    public DataRetentionServiceTests()
    {
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_providerMock.Object);
        _providerMock.Setup(x => x.GetService(typeof(ICommandLogRepository))).Returns(_commandLogs.Object);
        _providerMock.Setup(x => x.GetService(typeof(IUserActivityEventRepository))).Returns(_activityEvents.Object);
        _providerMock.Setup(x => x.GetService(typeof(IConnectionEventRepository))).Returns(_connectionEvents.Object);
        _providerMock.Setup(x => x.GetService(typeof(ITtsMessageRepository))).Returns(_ttsMessages.Object);
        _providerMock.Setup(x => x.GetService(typeof(IAssistantUsageMetricsRepository))).Returns(_usageMetrics.Object);
        _providerMock.Setup(x => x.GetService(typeof(IAudioPlaybackLogRepository))).Returns(_playbackLogs.Object);

        _commandLogs.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _activityEvents.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _connectionEvents.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _ttsMessages.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _usageMetrics.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _playbackLogs.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private DataRetentionService CreateService(
        DataRetentionOptions? options = null,
        UserActivityEventRetentionOptions? activityOptions = null,
        int connectionEventRetentionDays = 30) =>
        new(
            _providerMock.Object,
            _scopeFactoryMock.Object,
            Options.Create(options ?? new DataRetentionOptions { InitialDelayMinutes = 1 }),
            Options.Create(activityOptions ?? new UserActivityEventRetentionOptions()),
            Options.Create(new PerformanceMetricsOptions { ConnectionEventRetentionDays = connectionEventRetentionDays }),
            _loggerMock.Object,
            _fakeTime);

    private static bool Called<T>(Mock<T> mock) where T : class =>
        mock.Invocations.Any(i => i.Method.Name == "DeleteOlderThanAsync");

    private static DateTime CutoffOf<T>(Mock<T> mock) where T : class =>
        (DateTime)mock.Invocations.First(i => i.Method.Name == "DeleteOlderThanAsync").Arguments[0];

    private async Task RunOneSweepAsync(DataRetentionService service, Func<bool> sweepFinished)
    {
        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!sweepFinished() && DateTime.UtcNow < deadline)
        {
            _fakeTime.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(20).ConfigureAwait(false);
        }

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Sweep_DeletesFromEveryTable_WithEachTablesOwnCutoff()
    {
        var options = new DataRetentionOptions
        {
            InitialDelayMinutes = 1,
            CommandLogRetentionDays = 11,
            TtsMessageRetentionDays = 12,
            AssistantUsageMetricsRetentionDays = 13,
            AudioPlaybackLogRetentionDays = 14
        };
        var service = CreateService(options, new UserActivityEventRetentionOptions { RetentionDays = 15 }, connectionEventRetentionDays: 16);
        var before = DateTime.UtcNow;

        await RunOneSweepAsync(service, () => Called(_playbackLogs));

        CutoffOf(_commandLogs).Should().BeCloseTo(before.AddDays(-11), TimeSpan.FromMinutes(2));
        CutoffOf(_ttsMessages).Should().BeCloseTo(before.AddDays(-12), TimeSpan.FromMinutes(2));
        CutoffOf(_usageMetrics).Should().BeCloseTo(before.AddDays(-13), TimeSpan.FromMinutes(2));
        CutoffOf(_playbackLogs).Should().BeCloseTo(before.AddDays(-14), TimeSpan.FromMinutes(2));
        CutoffOf(_activityEvents).Should().BeCloseTo(before.AddDays(-15), TimeSpan.FromMinutes(2));
        CutoffOf(_connectionEvents).Should().BeCloseTo(before.AddDays(-16), TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Sweep_SkipsTablesWhoseRetentionIsDisabled()
    {
        var options = new DataRetentionOptions { InitialDelayMinutes = 1, CommandLogRetentionDays = 0, TtsMessageRetentionDays = -1 };
        var service = CreateService(options, new UserActivityEventRetentionOptions { Enabled = false }, connectionEventRetentionDays: 0);

        await RunOneSweepAsync(service, () => Called(_playbackLogs));

        Called(_commandLogs).Should().BeFalse();
        Called(_ttsMessages).Should().BeFalse();
        Called(_activityEvents).Should().BeFalse("UserActivityEventRetention:Enabled=false disables that table");
        Called(_connectionEvents).Should().BeFalse();
        Called(_usageMetrics).Should().BeTrue();
        Called(_playbackLogs).Should().BeTrue();
    }

    [Fact]
    public async Task Sweep_LoopsUntilAPartialBatch_AndUsesActivityEventBatchSize()
    {
        var calls = 0;
        _commandLogs
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++calls < 3 ? 100 : 40);

        var options = new DataRetentionOptions { InitialDelayMinutes = 1, CleanupBatchSize = 100 };
        var service = CreateService(options, new UserActivityEventRetentionOptions { CleanupBatchSize = 250 });

        await RunOneSweepAsync(service, () => Called(_playbackLogs));

        calls.Should().Be(3, "the loop keeps deleting until a batch comes back short");
        _activityEvents.Verify(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 250, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sweep_WhenOneTableThrows_StillSweepsTheOthers_AndRecordsTheError()
    {
        _commandLogs
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService();

        await RunOneSweepAsync(service, () => Called(_playbackLogs) && service.Status == "Error");

        Called(_playbackLogs).Should().BeTrue("a failure on one table does not stop the rest of the sweep");
        service.LastError.Should().Contain("boom");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_NeverSweeps()
    {
        var service = CreateService(new DataRetentionOptions { Enabled = false });
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(() => _loggerMock.Invocations.Any(i =>
            i.Method.Name == "Log" && i.Arguments[2]?.ToString()?.Contains("disabled") == true));
        _fakeTime.Advance(TimeSpan.FromDays(2));
        await service.StopAsync(CancellationToken.None);

        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }
}
