using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Configuration.Assistant;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="AssistantInteractionLogRetentionService"/>. The service's startup and
/// inter-batch delays are driven by an injected <see cref="FakeTimeProvider"/> rather than real
/// wall-clock waits, so these tests can deterministically drive <c>PerformCleanupAsync</c> instead
/// of only exercising the disabled/enabled startup paths. See CLAUDE.md's background-service test
/// rules - nothing here blocks a thread-pool thread on another, and both log/mock assertions and
/// the fake-clock advance loop poll via <see cref="LogTestHelper"/> with
/// <c>ConfigureAwait(false)</c> so a busy full test run can't starve them.
/// </summary>
public class AssistantInteractionLogRetentionServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAssistantInteractionLogRepository> _assistantInteractionLogRepoMock;
    private readonly Mock<IDmAssistantInteractionLogRepository> _dmAssistantInteractionLogRepoMock;
    private readonly Mock<ILlmUsageRepository> _llmUsageRepoMock;
    private readonly Mock<ILogger<AssistantInteractionLogRetentionService>> _loggerMock;
    private readonly FakeTimeProvider _fakeTime;

    public AssistantInteractionLogRetentionServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _assistantInteractionLogRepoMock = new Mock<IAssistantInteractionLogRepository>();
        _dmAssistantInteractionLogRepoMock = new Mock<IDmAssistantInteractionLogRepository>();
        _llmUsageRepoMock = new Mock<ILlmUsageRepository>();
        _loggerMock = new Mock<ILogger<AssistantInteractionLogRetentionService>>();
        _fakeTime = new FakeTimeProvider();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IAssistantInteractionLogRepository)))
            .Returns(_assistantInteractionLogRepoMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IDmAssistantInteractionLogRepository)))
            .Returns(_dmAssistantInteractionLogRepoMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILlmUsageRepository)))
            .Returns(_llmUsageRepoMock.Object);

        // Default: every table's delete returns 0 (nothing to delete), so a batch loop stops
        // after one call unless a test overrides it.
        _assistantInteractionLogRepoMock
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _dmAssistantInteractionLogRepoMock
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _llmUsageRepoMock
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static IOptions<LlmOptions> LlmOptions(
        int intervalHours = 24, int batchSize = 1000, int initialDelayMinutes = 5) =>
        Options.Create(new LlmOptions
        {
            RetentionSweepIntervalHours = intervalHours,
            RetentionBatchSize = batchSize,
            RetentionSweepInitialDelayMinutes = initialDelayMinutes
        });

    private static IOptions<AssistantOptions> AssistantOptions(int guildRetentionDays) =>
        Options.Create(new AssistantOptions
        {
            Privacy = new AssistantPrivacyOptions { InteractionLogRetentionDays = guildRetentionDays }
        });

    private static IOptions<DmAssistantOptions> DmAssistantOptions(int dmRetentionDays) =>
        Options.Create(new DmAssistantOptions { InteractionLogRetentionDays = dmRetentionDays });

    private AssistantInteractionLogRetentionService CreateService(
        int intervalHours = 24,
        int guildRetentionDays = 90,
        int dmRetentionDays = 90,
        int batchSize = 1000,
        int initialDelayMinutes = 5) =>
        new(
            _serviceProviderMock.Object,
            _scopeFactoryMock.Object,
            LlmOptions(intervalHours, batchSize, initialDelayMinutes),
            AssistantOptions(guildRetentionDays),
            DmAssistantOptions(dmRetentionDays),
            _loggerMock.Object,
            _fakeTime);

    /// <summary>
    /// Advances the fake clock in small steps until <paramref name="condition"/> is satisfied (or a
    /// generous real-time ceiling is hit, which only matters if the service is genuinely broken -
    /// it never gates correctness on how much real time elapsed). Mirrors
    /// <c>CpuSamplingServiceTests.AdvanceUntilAsync</c>.
    /// </summary>
    private static async Task<bool> AdvanceUntilAsync(
        FakeTimeProvider timeProvider,
        Func<bool> condition,
        TimeSpan? step = null,
        TimeSpan? ceiling = null)
    {
        var stepSize = step ?? TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + (ceiling ?? TimeSpan.FromSeconds(30));

        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            timeProvider.Advance(stepSize);
            await Task.Delay(20).ConfigureAwait(false);
        }

        return true;
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalIsZero_SkipsSweepAndReportsDisabled()
    {
        // Arrange
        var service = CreateService(intervalHours: 0);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(() => _loggerMock.Invocations.Count(i => i.Method.Name == "Log") >= 2);
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _assistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "sweep should not run when the interval is 0");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log that the sweep is disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabled_LogsStartupConfiguration()
    {
        // Arrange
        var service = CreateService(intervalHours: 12, batchSize: 500);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(() => _loggerMock.Invocations.Count(i => i.Method.Name == "Log") >= 1);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("starting") &&
                    v.ToString()!.Contains("12") &&
                    v.ToString()!.Contains("500")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log the configured interval and batch size on startup");

        // The fake clock is never advanced, so the (fake-clock-driven) initial delay never
        // elapses and no repository call happens during the test.
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Act
        var act = () => CreateService();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefullyOnCancellation()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(() => _loggerMock.Invocations.Count(i => i.Method.Name == "Log") >= 1);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - completed without throwing, and the service registered/unregistered cleanly.
        service.Status.Should().Be("Stopped");
    }

    [Fact]
    public async Task PerformCleanupAsync_UsesEachTablesOwnRetentionOption_ForCutoffMath()
    {
        // Arrange - distinct retention windows so a mixed-up cutoff shows up immediately.
        var service = CreateService(guildRetentionDays: 30, dmRetentionDays: 10, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();
        // Cutoff math in the service uses the real wall clock (DateTime.UtcNow), not the injected
        // TimeProvider - only the startup/inter-batch delays are driven by the fake clock.
        var before = DateTime.UtcNow;

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await AdvanceUntilAsync(_fakeTime, () => _scopeFactoryMock.Invocations.Count > 0);
        await LogTestHelper.WaitUntilAsync(() =>
            _assistantInteractionLogRepoMock.Invocations.Any(i => i.Method.Name == nameof(IAssistantInteractionLogRepository.DeleteOlderThanAsync)));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        var guildCutoff = (DateTime)_assistantInteractionLogRepoMock.Invocations
            .First(i => i.Method.Name == nameof(IAssistantInteractionLogRepository.DeleteOlderThanAsync)).Arguments[0];
        var dmCutoff = (DateTime)_dmAssistantInteractionLogRepoMock.Invocations
            .First(i => i.Method.Name == nameof(IDmAssistantInteractionLogRepository.DeleteOlderThanAsync)).Arguments[0];
        var llmCutoff = (DateTime)_llmUsageRepoMock.Invocations
            .First(i => i.Method.Name == nameof(ILlmUsageRepository.DeleteOlderThanAsync)).Arguments[0];

        // Guild retention (30d) also governs the ledger sweep per the plan.
        guildCutoff.Should().BeCloseTo(before.AddDays(-30), TimeSpan.FromMinutes(2));
        llmCutoff.Should().BeCloseTo(before.AddDays(-30), TimeSpan.FromMinutes(2));
        dmCutoff.Should().BeCloseTo(before.AddDays(-10), TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task PerformCleanupAsync_WhenGuildRetentionIsZeroOrLess_SkipsGuildAndLedgerButRunsDm()
    {
        // Arrange - guild retention disabled skips both the guild table and the ledger (which
        // shares the guild retention window), while DM retention stays enabled independently.
        var service = CreateService(guildRetentionDays: 0, dmRetentionDays: 90, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await AdvanceUntilAsync(_fakeTime, () =>
            _dmAssistantInteractionLogRepoMock.Invocations.Any(i => i.Method.Name == nameof(IDmAssistantInteractionLogRepository.DeleteOlderThanAsync)));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _assistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "guild retention <= 0 should skip the guild table");
        _llmUsageRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "guild retention <= 0 should also skip the ledger, which shares the guild retention window");
        _dmAssistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "DM retention is enabled independently of guild retention");
    }

    [Fact]
    public async Task PerformCleanupAsync_WhenDmRetentionIsZeroOrLess_SkipsOnlyDmTable()
    {
        // Arrange
        var service = CreateService(guildRetentionDays: 90, dmRetentionDays: 0, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await AdvanceUntilAsync(_fakeTime, () =>
            _llmUsageRepoMock.Invocations.Any(i => i.Method.Name == nameof(ILlmUsageRepository.DeleteOlderThanAsync)));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _dmAssistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DM retention <= 0 should skip only the DM table");
        _assistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _llmUsageRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PerformCleanupAsync_PassesConfiguredBatchSizeToEachRepository()
    {
        // Arrange
        var service = CreateService(batchSize: 250, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await AdvanceUntilAsync(_fakeTime, () =>
            _llmUsageRepoMock.Invocations.Any(i => i.Method.Name == nameof(ILlmUsageRepository.DeleteOlderThanAsync)));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _assistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 250, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _dmAssistantInteractionLogRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 250, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _llmUsageRepoMock.Verify(
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 250, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PerformCleanupAsync_LoopsOverMultipleBatchesUntilAPartialBatchIsReturned()
    {
        // Arrange - two full batches then a partial (smaller than batchSize) one signals "done".
        var callCount = 0;
        _llmUsageRepoMock
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => 100,
                    2 => 100,
                    _ => 40
                };
            });

        var service = CreateService(batchSize: 100, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        // Advance past the initial delay and then the inter-batch delay repeatedly until all
        // three calls have landed.
        await AdvanceUntilAsync(_fakeTime, () => callCount >= 3, step: TimeSpan.FromMilliseconds(200));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        callCount.Should().Be(3, "the batch loop should keep calling until a partial batch signals nothing remains");
    }

    [Fact]
    public async Task PerformCleanupAsync_WhenARepositoryThrows_RecordsErrorAndTheLoopContinuesNextCycle()
    {
        // Arrange - the first sweep's guild-table call throws; the next scheduled sweep succeeds.
        var callCount = 0;
        _assistantInteractionLogRepoMock
            .Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromException<int>(new InvalidOperationException("boom"))
                    : Task.FromResult(0);
            });

        // Short interval so the second cycle fires promptly once we advance past it.
        var service = CreateService(intervalHours: 1, initialDelayMinutes: 1);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        var reachedError = await AdvanceUntilAsync(
            _fakeTime,
            () => service.Status == "Error",
            step: TimeSpan.FromMinutes(1),
            ceiling: TimeSpan.FromSeconds(30));

        reachedError.Should().BeTrue("the first cleanup pass should record the repository's exception");
        service.LastError.Should().Contain("boom");

        // Advance past the retry interval so the second (successful) cycle runs and clears the error.
        var recovered = await AdvanceUntilAsync(
            _fakeTime,
            () => callCount >= 2 && service.Status == "Running",
            step: TimeSpan.FromMinutes(5),
            ceiling: TimeSpan.FromSeconds(30));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        recovered.Should().BeTrue("the loop should keep running and clear the error once the next cycle succeeds");
    }
}
