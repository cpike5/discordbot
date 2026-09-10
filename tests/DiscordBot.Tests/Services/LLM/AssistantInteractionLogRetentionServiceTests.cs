using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Configuration.Assistant;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="AssistantInteractionLogRetentionService"/>. Mirrors the pattern in
/// <c>AuditLogRetentionServiceTests</c>: the service has a fixed 5-minute startup delay before its
/// first sweep, so these tests exercise the disabled/enabled startup paths rather than waiting out
/// the delay to observe an actual cleanup pass. See CLAUDE.md's background-service test rules -
/// nothing here blocks a thread-pool thread on another, and log assertions poll via
/// <see cref="LogTestHelper"/> instead of racing a fixed delay.
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

    public AssistantInteractionLogRetentionServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _assistantInteractionLogRepoMock = new Mock<IAssistantInteractionLogRepository>();
        _dmAssistantInteractionLogRepoMock = new Mock<IDmAssistantInteractionLogRepository>();
        _llmUsageRepoMock = new Mock<ILlmUsageRepository>();
        _loggerMock = new Mock<ILogger<AssistantInteractionLogRetentionService>>();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IAssistantInteractionLogRepository)))
            .Returns(_assistantInteractionLogRepoMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IDmAssistantInteractionLogRepository)))
            .Returns(_dmAssistantInteractionLogRepoMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILlmUsageRepository)))
            .Returns(_llmUsageRepoMock.Object);
    }

    private static IOptions<LlmOptions> LlmOptions(int intervalHours, int batchSize = 1000) =>
        Options.Create(new LlmOptions
        {
            RetentionSweepIntervalHours = intervalHours,
            RetentionBatchSize = batchSize
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
        int batchSize = 1000) =>
        new(
            _serviceProviderMock.Object,
            _scopeFactoryMock.Object,
            LlmOptions(intervalHours, batchSize),
            AssistantOptions(guildRetentionDays),
            DmAssistantOptions(dmRetentionDays),
            _loggerMock.Object);

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
            x => x.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "sweep should not run when the interval is 0");

        // Status ends up "Stopped" once StartAsync/StopAsync have completed - the base class's
        // finally block always overwrites it on the way out (see MonitoredBackgroundService).
        // "Disabled" is only observable transiently, via SetStatus, while the service is still
        // running - hence asserting on the log line instead.
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

        // The 5-minute initial delay means no repository call happens during the test.
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
}
