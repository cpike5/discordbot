using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotStatusBroadcaster"/>.
/// DiscordSocketClient is a sealed/concrete Discord.Net type, so tests use a real
/// (unconnected) instance for construction; all other collaborators are mocked.
/// </summary>
public class BotStatusBroadcasterTests : IAsyncLifetime
{
    private DiscordSocketClient _client = null!;
    private Mock<IDashboardUpdateService> _mockDashboardUpdateService = null!;
    private Mock<ILogger<BotStatusBroadcaster>> _mockLogger = null!;
    private Mock<ISettingsService> _mockSettingsService = null!;
    private Mock<IRatWatchStatusService> _mockRatWatchStatusService = null!;
    private Mock<IBotStatusService> _mockBotStatusService = null!;
    private Mock<IBackgroundTaskRunner> _mockBackgroundTaskRunner = null!;
    private IBotUptimeProvider _uptimeProvider = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private BotStatusBroadcaster _broadcaster = null!;

    public Task InitializeAsync()
    {
        _client = new DiscordSocketClient();
        _mockDashboardUpdateService = new Mock<IDashboardUpdateService>();
        _mockLogger = new Mock<ILogger<BotStatusBroadcaster>>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockRatWatchStatusService = new Mock<IRatWatchStatusService>();
        _mockBotStatusService = new Mock<IBotStatusService>();
        _mockBackgroundTaskRunner = new Mock<IBackgroundTaskRunner>();
        _uptimeProvider = new BotUptimeProvider();

        var services = new ServiceCollection();
        services.AddSingleton(_mockSettingsService.Object);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _broadcaster = new BotStatusBroadcaster(
            _client,
            _mockDashboardUpdateService.Object,
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockRatWatchStatusService.Object,
            _mockBotStatusService.Object,
            _scopeFactory,
            _mockBackgroundTaskRunner.Object,
            _uptimeProvider);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Constructor_ShouldCreateInstance_ImplementingIBotStatusBroadcaster()
    {
        _broadcaster.Should().NotBeNull();
        _broadcaster.Should().BeAssignableTo<IBotStatusBroadcaster>();
    }

    [Fact]
    public void Initialize_ShouldRegisterCustomStatusSource()
    {
        _broadcaster.Initialize();

        _mockBotStatusService.Verify(
            s => s.RegisterStatusSource(
                "CustomStatus",
                StatusSourcePriority.CustomStatus,
                It.IsAny<Func<Task<string?>>>()),
            Times.Once);
    }

    [Fact]
    public void Initialize_ShouldSubscribeToSettingsChangedAndRatWatchEvents()
    {
        _broadcaster.Initialize();

        // Raising each event must not throw — proves the handlers were wired up.
        var act1 = () => _mockSettingsService.Raise(
            s => s.SettingsChanged += null,
            _mockSettingsService.Object,
            new SettingsChangedEventArgs { UpdatedKeys = new[] { "General:StatusMessage" } });
        act1.Should().NotThrow();

        var act2 = () => _mockRatWatchStatusService.Raise(
            s => s.StatusUpdateRequested += null,
            _mockRatWatchStatusService.Object,
            EventArgs.Empty);
        act2.Should().NotThrow();
    }

    [Fact]
    public void SettingsChanged_WhenStatusMessageKeyUpdated_ShouldQueueRefreshViaBackgroundTaskRunner()
    {
        _broadcaster.Initialize();

        _mockSettingsService.Raise(
            s => s.SettingsChanged += null,
            _mockSettingsService.Object,
            new SettingsChangedEventArgs { UpdatedKeys = new[] { "General:StatusMessage" } });

        _mockBackgroundTaskRunner.Verify(
            r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), "RefreshBotStatus.SettingsChanged"),
            Times.Once);
    }

    [Fact]
    public void SettingsChanged_WhenUnrelatedKeyUpdated_ShouldNotQueueRefresh()
    {
        _broadcaster.Initialize();

        _mockSettingsService.Raise(
            s => s.SettingsChanged += null,
            _mockSettingsService.Object,
            new SettingsChangedEventArgs { UpdatedKeys = new[] { "Some:OtherSetting" } });

        _mockBackgroundTaskRunner.Verify(
            r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), "RefreshBotStatus.SettingsChanged"),
            Times.Never);
    }

    [Fact]
    public void RatWatchStatusUpdateRequested_ShouldQueueRefreshViaBackgroundTaskRunner()
    {
        _broadcaster.Initialize();

        _mockRatWatchStatusService.Raise(
            s => s.StatusUpdateRequested += null,
            _mockRatWatchStatusService.Object,
            EventArgs.Empty);

        _mockBackgroundTaskRunner.Verify(
            r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), "RefreshBotStatus.RatWatchUpdate"),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastStatusAsync_ShouldPublishDtoWithConnectionStateLatencyAndGuildCount()
    {
        BotStatusUpdateDto? captured = null;
        _mockDashboardUpdateService
            .Setup(s => s.BroadcastBotStatusAsync(It.IsAny<BotStatusUpdateDto>(), It.IsAny<CancellationToken>()))
            .Callback<BotStatusUpdateDto, CancellationToken>((dto, _) => captured = dto)
            .Returns(Task.CompletedTask);

        await _broadcaster.BroadcastStatusAsync();

        captured.Should().NotBeNull();
        captured!.ConnectionState.Should().Be(_client.ConnectionState.ToString());
        captured.Latency.Should().Be(_client.Latency);
        captured.GuildCount.Should().Be(_client.Guilds.Count);
    }

    [Fact]
    public async Task BroadcastStatusAsync_WhenDashboardServiceThrows_ShouldNotThrow()
    {
        _mockDashboardUpdateService
            .Setup(s => s.BroadcastBotStatusAsync(It.IsAny<BotStatusUpdateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await _broadcaster.BroadcastStatusAsync();

        await act.Should().NotThrowAsync("status broadcast failures must not break bot operation");
    }

    [Fact]
    public async Task ApplyStartupStatusAsync_ShouldRefreshStatusAndLogCurrentStatus()
    {
        _mockBotStatusService.Setup(s => s.RefreshStatusAsync()).Returns(Task.CompletedTask);
        _mockBotStatusService.Setup(s => s.GetCurrentStatus()).Returns(("RatWatch", "Watching for rats"));

        await _broadcaster.ApplyStartupStatusAsync();

        _mockBotStatusService.Verify(s => s.RefreshStatusAsync(), Times.Once);
        _mockBotStatusService.Verify(s => s.GetCurrentStatus(), Times.Once);
    }

    [Fact]
    public async Task ApplyStartupStatusAsync_WhenRefreshThrows_ShouldNotThrow()
    {
        _mockBotStatusService.Setup(s => s.RefreshStatusAsync()).ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await _broadcaster.ApplyStartupStatusAsync();

        await act.Should().NotThrowAsync("startup status failures must not break bot startup");
    }

    [Fact]
    public void Initialize_WhenCalledTwice_ShouldNotDoubleSubscribe()
    {
        _broadcaster.Initialize();
        _broadcaster.Initialize();

        _mockBotStatusService.Verify(
            s => s.RegisterStatusSource(
                "CustomStatus",
                StatusSourcePriority.CustomStatus,
                It.IsAny<Func<Task<string?>>>()),
            Times.Once);

        _mockSettingsService.Raise(
            s => s.SettingsChanged += null,
            _mockSettingsService.Object,
            new SettingsChangedEventArgs { UpdatedKeys = new[] { "General:StatusMessage" } });

        // If Initialize double-subscribed, the handler would fire twice for one event.
        _mockBackgroundTaskRunner.Verify(
            r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), "RefreshBotStatus.SettingsChanged"),
            Times.Once);
    }

    [Fact]
    public void Shutdown_WhenNotInitialized_ShouldBeNoOp()
    {
        var act = () => _broadcaster.Shutdown();

        act.Should().NotThrow();
        _mockBotStatusService.Verify(
            s => s.UnregisterStatusSource(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Shutdown_ShouldUnsubscribeFromEvents_SoLaterRaisesDoNotQueueRefresh()
    {
        _broadcaster.Initialize();
        _broadcaster.Shutdown();

        _mockSettingsService.Raise(
            s => s.SettingsChanged += null,
            _mockSettingsService.Object,
            new SettingsChangedEventArgs { UpdatedKeys = new[] { "General:StatusMessage" } });

        _mockRatWatchStatusService.Raise(
            s => s.StatusUpdateRequested += null,
            _mockRatWatchStatusService.Object,
            EventArgs.Empty);

        _mockBackgroundTaskRunner.Verify(
            r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<string>()),
            Times.Never);
    }
}
