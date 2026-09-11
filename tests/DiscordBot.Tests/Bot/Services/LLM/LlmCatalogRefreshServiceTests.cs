using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Bot.Services.LLM;

/// <summary>
/// Unit tests for <see cref="LlmCatalogRefreshService"/>: disabled-via-config short-circuit, the
/// skip-when-recently-refreshed guard, and that a thrown refresh is caught and logged rather than
/// killing the loop. Uses tiny configured delays and polls via <see cref="LogTestHelper"/> instead of
/// fixed <c>Task.Delay</c> waits, per CLAUDE.md's background-service test guidance.
/// </summary>
public class LlmCatalogRefreshServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILlmModelCatalogService> _catalogServiceMock;
    private readonly Mock<ILogger<LlmCatalogRefreshService>> _loggerMock;
    private readonly Mock<IOptions<LlmOptions>> _optionsMock;

    public LlmCatalogRefreshServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _catalogServiceMock = new Mock<ILlmModelCatalogService>();
        _loggerMock = new Mock<ILogger<LlmCatalogRefreshService>>();
        _optionsMock = new Mock<IOptions<LlmOptions>>();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILlmModelCatalogService)))
            .Returns(_catalogServiceMock.Object);
    }

    private LlmCatalogRefreshService CreateService(LlmOptions options)
    {
        _optionsMock.Setup(x => x.Value).Returns(options);
        return new LlmCatalogRefreshService(
            _serviceProviderMock.Object,
            _scopeFactoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);
    }

    private static async Task RunBrieflyAsync(LlmCatalogRefreshService service, Func<bool> until)
    {
        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(until, TimeSpan.FromSeconds(5));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshHoursIsZero_DisablesAndNeverRefreshes()
    {
        var service = CreateService(new LlmOptions
        {
            CatalogRefreshHours = 0,
            CatalogRefreshInitialDelayMinutes = 0,
        });

        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);
        await LogTestHelper.WaitUntilAsync(() => service.Status == "Disabled" || service.Status == "Stopped");
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        _catalogServiceMock.Verify(
            x => x.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a zero-hour interval must disable the refresh entirely");

        service.Status.Should().BeOneOf("Disabled", "Stopped");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLastRefreshIsRecent_SkipsFetchAndKeepsRunning()
    {
        _catalogServiceMock
            .Setup(x => x.GetLastRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddMinutes(-5));

        var service = CreateService(new LlmOptions
        {
            CatalogRefreshHours = 24,
            CatalogRefreshInitialDelayMinutes = 0,
        });

        await RunBrieflyAsync(
            service,
            () => _catalogServiceMock.Invocations.Any(i => i.Method.Name == nameof(ILlmModelCatalogService.GetLastRefreshAsync)));

        _catalogServiceMock.Verify(
            x => x.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a recent last refresh (within the configured interval) must not trigger another fetch");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPriorRefresh_CallsRefreshAsync()
    {
        _catalogServiceMock
            .Setup(x => x.GetLastRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _catalogServiceMock
            .Setup(x => x.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCatalogRefreshResult { Added = 3, Updated = 0, Removed = 0, FetchedAt = DateTime.UtcNow });

        var service = CreateService(new LlmOptions
        {
            CatalogRefreshHours = 24,
            CatalogRefreshInitialDelayMinutes = 0,
        });

        await RunBrieflyAsync(
            service,
            () => _catalogServiceMock.Invocations.Any(i => i.Method.Name == nameof(ILlmModelCatalogService.RefreshAsync)));

        _catalogServiceMock.Verify(
            x => x.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshThrows_LogsErrorAndLoopContinues()
    {
        _catalogServiceMock
            .Setup(x => x.GetLastRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var callCount = 0;
        _catalogServiceMock
            .Setup(x => x.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("OpenRouter is unavailable");
                }

                return new LlmCatalogRefreshResult { Added = 0, Updated = 0, Removed = 0, FetchedAt = DateTime.UtcNow };
            });

        var service = CreateService(new LlmOptions
        {
            CatalogRefreshHours = 24,
            CatalogRefreshInitialDelayMinutes = 0,
        });

        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        var loggedError = await LogTestHelper.WaitForLogAsync(
            _loggerMock,
            LogLevel.Error,
            msg => msg.Contains("Scheduled LLM catalog refresh failed"),
            TimeSpan.FromSeconds(5));

        // Captured before cancellation: proves the exception was swallowed in-loop (status "Error")
        // rather than propagating out of ExecuteMonitoredAsync and being marked "Stopped"/fatal by
        // MonitoredBackgroundService's own catch block.
        var statusAfterFailure = service.Status;

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        loggedError.Should().BeTrue("a thrown refresh must be caught and logged, not left to crash the service");
        callCount.Should().Be(1, "the loop should not have reattempted yet within the 24h interval");
        statusAfterFailure.Should().Be("Error");
    }

    [Fact]
    public void ServiceName_ShouldReturnCorrectName()
    {
        var service = CreateService(new LlmOptions());
        service.ServiceName.Should().Be("LLM Catalog Refresh Service");
    }
}
