using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotStatusService"/>.
/// </summary>
public class BotStatusServiceTests
{
    private readonly Mock<DiscordSocketClient> _mockClient;
    private readonly Mock<ILogger<BotStatusService>> _mockLogger;
    private readonly BotStatusService _service;

    public BotStatusServiceTests()
    {
        _mockClient = new Mock<DiscordSocketClient>();
        _mockLogger = new Mock<ILogger<BotStatusService>>();

        // Set up client to return Connected by default
        _mockClient.Setup(c => c.ConnectionState).Returns(ConnectionState.Connected);

        _service = new BotStatusService(_mockClient.Object, _mockLogger.Object);
    }

    #region SetStatusAsync Tests

    [Fact]
    public async Task SetStatusAsync_ShouldCallSetGameAsync()
    {
        // Arrange
        var message = "Test Status";

        // Act
        await _service.SetStatusAsync(message);

        // Assert
        _mockClient.Verify(
            c => c.SetGameAsync(message, It.IsAny<string>(), It.IsAny<ActivityType>()),
            Times.Once,
            "SetGameAsync should be called once with the message");
    }

    [Fact]
    public async Task SetStatusAsync_ShouldSetCurrentSourceToDirect()
    {
        // Arrange
        var message = "Direct status message";

        // Act
        await _service.SetStatusAsync(message);
        var (sourceName, currentMessage) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("Direct", "SetStatusAsync should set source to 'Direct'");
        currentMessage.Should().Be(message);
    }

    [Fact]
    public async Task SetStatusAsync_WithNullMessage_ShouldClearStatus()
    {
        // Arrange
        await _service.SetStatusAsync("First status");

        // Act
        await _service.SetStatusAsync(null);
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        _mockClient.Verify(
            c => c.SetGameAsync(null, It.IsAny<string>(), It.IsAny<ActivityType>()),
            Times.Once,
            "SetGameAsync should be called with null to clear status");
        sourceName.Should().Be("Direct");
        message.Should().BeNull();
    }

    [Fact]
    public async Task SetStatusAsync_ShouldLogInformation()
    {
        // Arrange
        var message = "Test status message";

        // Act
        await _service.SetStatusAsync(message);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Bot status set directly")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information when status is set directly");
    }

    #endregion

    #region RegisterStatusSource Tests

    [Fact]
    public void RegisterStatusSource_ShouldAddSourceToRegistry()
    {
        // Arrange
        var sourceName = "TestSource";
        var priority = 50;
        Func<Task<string?>> provider = () => Task.FromResult<string?>("Test message");

        // Act
        _service.RegisterStatusSource(sourceName, priority, provider);

        // Assert - verify through refresh behavior
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Registered status source") && v.ToString()!.Contains(sourceName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log registration of status source");
    }

    [Fact]
    public void RegisterStatusSource_WithExistingPriority_ShouldReplaceAndLogWarning()
    {
        // Arrange
        var priority = 100;
        Func<Task<string?>> firstProvider = () => Task.FromResult<string?>("First");
        Func<Task<string?>> secondProvider = () => Task.FromResult<string?>("Second");

        _service.RegisterStatusSource("FirstSource", priority, firstProvider);

        // Act
        _service.RegisterStatusSource("SecondSource", priority, secondProvider);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Overwriting existing status source")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when overwriting existing source at same priority");
    }

    #endregion

    #region UnregisterStatusSource Tests

    [Fact]
    public void UnregisterStatusSource_ShouldRemoveSourceFromRegistry()
    {
        // Arrange
        var sourceName = "TestSource";
        Func<Task<string?>> provider = () => Task.FromResult<string?>("Test message");

        _service.RegisterStatusSource(sourceName, 50, provider);

        // Act
        _service.UnregisterStatusSource(sourceName);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unregistered status source") && v.ToString()!.Contains(sourceName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log unregistration of status source");
    }

    [Fact]
    public async Task UnregisterStatusSource_ShouldAffectNextRefresh()
    {
        // Arrange
        var highPrioritySource = "HighPriority";
        var lowPrioritySource = "LowPriority";

        Func<Task<string?>> highProvider = () => Task.FromResult<string?>("High priority message");
        Func<Task<string?>> lowProvider = () => Task.FromResult<string?>("Low priority message");

        _service.RegisterStatusSource(highPrioritySource, 10, highProvider);
        _service.RegisterStatusSource(lowPrioritySource, 100, lowProvider);

        // Verify high priority wins initially
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();
        sourceName.Should().Be(highPrioritySource);

        // Act - unregister high priority source
        _service.UnregisterStatusSource(highPrioritySource);
        await _service.RefreshStatusAsync();
        var (newSourceName, newMessage) = _service.GetCurrentStatus();

        // Assert - low priority source should now be active
        newSourceName.Should().Be(lowPrioritySource, "low priority source should become active after high priority is unregistered");
        newMessage.Should().Be("Low priority message");
    }

    [Fact]
    public void UnregisterStatusSource_WithNonExistentSource_ShouldLogWarning()
    {
        // Arrange
        var nonExistentSource = "DoesNotExist";

        // Act
        _service.UnregisterStatusSource(nonExistentSource);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to unregister non-existent status source")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when attempting to unregister non-existent source");
    }

    #endregion

    #region RefreshStatusAsync Tests

    [Fact]
    public async Task RefreshStatusAsync_WithHighestPriorityActiveSource_ShouldUseIt()
    {
        // Arrange
        var highPriorityMessage = "High priority status";
        var lowPriorityMessage = "Low priority status";

        _service.RegisterStatusSource("HighPriority", 10, () => Task.FromResult<string?>(highPriorityMessage));
        _service.RegisterStatusSource("LowPriority", 100, () => Task.FromResult<string?>(lowPriorityMessage));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("HighPriority", "lower priority number should win");
        message.Should().Be(highPriorityMessage);
        _mockClient.Verify(c => c.SetGameAsync(highPriorityMessage, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once);
    }

    [Fact]
    public async Task RefreshStatusAsync_WithInactiveHighPrioritySource_ShouldSkipToNextActive()
    {
        // Arrange
        var activeLowPriorityMessage = "Active low priority";

        _service.RegisterStatusSource("InactiveHigh", 10, () => Task.FromResult<string?>(null)); // Inactive (returns null)
        _service.RegisterStatusSource("ActiveLow", 100, () => Task.FromResult<string?>(activeLowPriorityMessage));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("ActiveLow", "should skip inactive source and use next active source");
        message.Should().Be(activeLowPriorityMessage);
        _mockClient.Verify(c => c.SetGameAsync(activeLowPriorityMessage, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once);
    }

    [Fact]
    public async Task RefreshStatusAsync_WithNoActiveSources_ShouldClearStatus()
    {
        // Arrange
        // First set an active status, then make all sources inactive
        _service.RegisterStatusSource("InitialActive", 50, () => Task.FromResult<string?>("Active message"));
        await _service.RefreshStatusAsync(); // Set initial status

        // Now make the source inactive
        _service.RegisterStatusSource("InitialActive", 50, () => Task.FromResult<string?>(null));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("None", "source should be 'None' when no active sources");
        message.Should().BeNull();
        _mockClient.Verify(c => c.SetGameAsync(null, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once, "status should be cleared");
    }

    [Fact]
    public async Task RefreshStatusAsync_WithProviderException_ShouldContinueToNextSource()
    {
        // Arrange
        var goodMessage = "Good source message";

        _service.RegisterStatusSource("FailingSource", 10, () => throw new InvalidOperationException("Simulated failure"));
        _service.RegisterStatusSource("GoodSource", 100, () => Task.FromResult<string?>(goodMessage));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("GoodSource", "should skip failing source and use next working source");
        message.Should().Be(goodMessage);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to evaluate status source")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when provider throws exception");
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenStatusUnchanged_ShouldNotCallDiscord()
    {
        // Arrange
        var message = "Unchanging status";
        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>(message));

        // First refresh
        await _service.RefreshStatusAsync();
        _mockClient.Verify(c => c.SetGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once);

        // Act - second refresh with same status
        await _service.RefreshStatusAsync();

        // Assert - should still only be called once (optimization to avoid unnecessary Discord API calls)
        _mockClient.Verify(c => c.SetGameAsync(message, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once,
            "should not call Discord API when status hasn't changed");
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenStatusChanges_ShouldCallDiscord()
    {
        // Arrange
        var firstMessage = "First status";
        var secondMessage = "Second status";
        var messageToReturn = firstMessage;

        _service.RegisterStatusSource("DynamicSource", 50, () => Task.FromResult<string?>(messageToReturn));

        // First refresh
        await _service.RefreshStatusAsync();

        // Act - change the message and refresh again
        messageToReturn = secondMessage;
        await _service.RefreshStatusAsync();

        // Assert
        _mockClient.Verify(c => c.SetGameAsync(firstMessage, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once);
        _mockClient.Verify(c => c.SetGameAsync(secondMessage, It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Once);
    }

    [Fact]
    public async Task RefreshStatusAsync_WithNoSources_ShouldNotCallDiscord()
    {
        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("None", "source should remain 'None' when no sources registered");
        message.Should().BeNull();
        // Should not call Discord when status is already None and no sources are registered
        _mockClient.Verify(c => c.SetGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Never,
            "should not call Discord when status is already None");
    }

    [Fact]
    public async Task RefreshStatusAsync_WithNoSourcesMultipleTimes_ShouldNeverCallDiscord()
    {
        // Act
        await _service.RefreshStatusAsync();
        await _service.RefreshStatusAsync();
        await _service.RefreshStatusAsync();

        // Assert
        _mockClient.Verify(c => c.SetGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityType>()), Times.Never,
            "should never call Discord when status is already None and no sources registered");
    }

    [Fact]
    public async Task RefreshStatusAsync_ShouldLogDebugMessageWithSourceCount()
    {
        // Arrange
        _service.RegisterStatusSource("Source1", 10, () => Task.FromResult<string?>("Message 1"));
        _service.RegisterStatusSource("Source2", 20, () => Task.FromResult<string?>("Message 2"));

        // Act
        await _service.RefreshStatusAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Refreshing bot status") && v.ToString()!.Contains("2")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message with count of registered sources");
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenStatusChanges_ShouldLogInformation()
    {
        // Arrange
        var message = "New status message";
        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>(message));

        // Act
        await _service.RefreshStatusAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Bot status changed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information when status changes");
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenCleared_ShouldLogInformation()
    {
        // Arrange
        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>("Message"));
        await _service.RefreshStatusAsync(); // Set initial status

        _service.UnregisterStatusSource("TestSource"); // Remove all sources

        // Act
        await _service.RefreshStatusAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Bot status cleared")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information when status is cleared");
    }

    #endregion

    #region GetCurrentStatus Tests

    [Fact]
    public void GetCurrentStatus_BeforeAnyRefresh_ShouldReturnNoneAndNull()
    {
        // Act
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("None", "source should be 'None' before any refresh");
        message.Should().BeNull("message should be null before any refresh");
    }

    [Fact]
    public async Task GetCurrentStatus_AfterRefresh_ShouldReturnCorrectSourceAndMessage()
    {
        // Arrange
        var expectedMessage = "Test status message";
        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>(expectedMessage));
        await _service.RefreshStatusAsync();

        // Act
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("TestSource");
        message.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task GetCurrentStatus_AfterSetStatusAsync_ShouldReturnDirectSource()
    {
        // Arrange
        var directMessage = "Direct status";
        await _service.SetStatusAsync(directMessage);

        // Act
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("Direct");
        message.Should().Be(directMessage);
    }

    #endregion

    #region Priority Constant Tests

    [Fact]
    public void StatusSourcePriority_ShouldHaveCorrectOrdering()
    {
        // Assert - verify priority ordering (lower number = higher priority)
        StatusSourcePriority.Maintenance.Should().BeLessThan(StatusSourcePriority.RatWatch,
            "Maintenance should have higher priority than RatWatch");
        StatusSourcePriority.RatWatch.Should().BeLessThan(StatusSourcePriority.CustomStatus,
            "RatWatch should have higher priority than CustomStatus");
        StatusSourcePriority.CustomStatus.Should().BeLessThan(StatusSourcePriority.Default,
            "CustomStatus should have higher priority than Default");
    }

    [Fact]
    public void StatusSourcePriority_Maintenance_ShouldBe10()
    {
        // Assert
        StatusSourcePriority.Maintenance.Should().Be(10);
    }

    [Fact]
    public void StatusSourcePriority_RatWatch_ShouldBe20()
    {
        // Assert
        StatusSourcePriority.RatWatch.Should().Be(20);
    }

    [Fact]
    public void StatusSourcePriority_CustomStatus_ShouldBe100()
    {
        // Assert
        StatusSourcePriority.CustomStatus.Should().Be(100);
    }

    [Fact]
    public void StatusSourcePriority_Default_ShouldBe1000()
    {
        // Assert
        StatusSourcePriority.Default.Should().Be(1000);
    }

    [Fact]
    public async Task StatusSourcePriority_RealWorldScenario_MaintenanceShouldOverrideAll()
    {
        // Arrange
        var maintenanceMessage = "Under maintenance";
        var ratWatchMessage = "Rat Watch active";
        var customMessage = "Custom status";
        var defaultMessage = "Default status";

        _service.RegisterStatusSource("Default", StatusSourcePriority.Default, () => Task.FromResult<string?>(defaultMessage));
        _service.RegisterStatusSource("Custom", StatusSourcePriority.CustomStatus, () => Task.FromResult<string?>(customMessage));
        _service.RegisterStatusSource("RatWatch", StatusSourcePriority.RatWatch, () => Task.FromResult<string?>(ratWatchMessage));
        _service.RegisterStatusSource("Maintenance", StatusSourcePriority.Maintenance, () => Task.FromResult<string?>(maintenanceMessage));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("Maintenance", "Maintenance has highest priority and should win");
        message.Should().Be(maintenanceMessage);
    }

    [Fact]
    public async Task StatusSourcePriority_RealWorldScenario_RatWatchShouldOverrideCustomAndDefault()
    {
        // Arrange
        var ratWatchMessage = "Rat Watch active";
        var customMessage = "Custom status";
        var defaultMessage = "Default status";

        _service.RegisterStatusSource("Default", StatusSourcePriority.Default, () => Task.FromResult<string?>(defaultMessage));
        _service.RegisterStatusSource("Custom", StatusSourcePriority.CustomStatus, () => Task.FromResult<string?>(customMessage));
        _service.RegisterStatusSource("RatWatch", StatusSourcePriority.RatWatch, () => Task.FromResult<string?>(ratWatchMessage));

        // Act
        await _service.RefreshStatusAsync();
        var (sourceName, message) = _service.GetCurrentStatus();

        // Assert
        sourceName.Should().Be("RatWatch", "RatWatch has higher priority than Custom and Default");
        message.Should().Be(ratWatchMessage);
    }

    #endregion

    #region Thread Safety and Edge Cases

    [Fact]
    public async Task RefreshStatusAsync_MultipleConcurrentCalls_ShouldNotThrow()
    {
        // Arrange
        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>("Test message"));

        // Act - make multiple concurrent refresh calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _service.RefreshStatusAsync())
            .ToArray();

        // Assert - should complete without throwing
        await FluentActions.Awaiting(() => Task.WhenAll(tasks))
            .Should().NotThrowAsync("concurrent refreshes should be thread-safe");
    }

    [Fact]
    public async Task SetStatusAsync_MultipleConcurrentCalls_ShouldNotThrow()
    {
        // Act - make multiple concurrent SetStatus calls
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _service.SetStatusAsync($"Status {i}"))
            .ToArray();

        // Assert - should complete without throwing
        await FluentActions.Awaiting(() => Task.WhenAll(tasks))
            .Should().NotThrowAsync("concurrent SetStatusAsync calls should be thread-safe");
    }

    [Fact]
    public async Task RegisterAndUnregister_DuringRefresh_ShouldNotThrow()
    {
        // Arrange
        _service.RegisterStatusSource("InitialSource", 50, () => Task.FromResult<string?>("Initial"));

        // Act - perform operations concurrently
        var refreshTask = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await _service.RefreshStatusAsync();
                await Task.Delay(10);
            }
        });

        var modifyTask = Task.Run(async () =>
        {
            await Task.Delay(5);
            _service.RegisterStatusSource("NewSource", 100, () => Task.FromResult<string?>("New"));
            await Task.Delay(5);
            _service.UnregisterStatusSource("InitialSource");
        });

        // Assert
        await FluentActions.Awaiting(() => Task.WhenAll(refreshTask, modifyTask))
            .Should().NotThrowAsync("concurrent register/unregister during refresh should be thread-safe");
    }

    [Fact]
    public async Task RefreshStatusAsync_WithAsyncProviderDelay_ShouldAwaitCompletion()
    {
        // Arrange
        var delayedMessage = "Delayed message";
        _service.RegisterStatusSource("DelayedSource", 50, async () =>
        {
            await Task.Delay(50);
            return delayedMessage;
        });

        // Act
        await _service.RefreshStatusAsync();
        var (_, message) = _service.GetCurrentStatus();

        // Assert
        message.Should().Be(delayedMessage, "should properly await async status providers");
    }

    #endregion

    #region Discord API Error Handling

    [Fact]
    public async Task RefreshStatusAsync_WhenDiscordThrows_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        _mockClient
            .Setup(c => c.SetGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityType>()))
            .ThrowsAsync(new Exception("Discord API error"));

        _service.RegisterStatusSource("TestSource", 50, () => Task.FromResult<string?>("Test message"));

        // Act & Assert
        await FluentActions.Awaiting(() => _service.RefreshStatusAsync())
            .Should().NotThrowAsync("should handle Discord API errors gracefully");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to update Discord client status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when Discord API fails");
    }

    [Fact]
    public async Task SetStatusAsync_WhenDiscordThrows_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        _mockClient
            .Setup(c => c.SetGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActivityType>()))
            .ThrowsAsync(new Exception("Discord API error"));

        // Act & Assert
        await FluentActions.Awaiting(() => _service.SetStatusAsync("Test"))
            .Should().NotThrowAsync("should handle Discord API errors gracefully");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to update Discord client status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when Discord API fails");
    }

    #endregion
}
