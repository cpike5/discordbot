using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for ScheduledMessageExecutionService.
/// Tests cover initialization, configuration, and dependency resolution.
/// Note: Full end-to-end execution tests are covered by integration tests due to the 10-second startup delay.
/// </summary>
public class ScheduledMessageExecutionServiceTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IScheduledMessageRepository> _mockRepository;
    private readonly Mock<IScheduledMessageService> _mockService;
    private readonly Mock<ILogger<ScheduledMessageExecutionService>> _mockLogger;
    private readonly IOptions<ScheduledMessagesOptions> _options;

    public ScheduledMessageExecutionServiceTests()
    {
        _mockRepository = new Mock<IScheduledMessageRepository>();
        _mockService = new Mock<IScheduledMessageService>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<ScheduledMessageExecutionService>>();

        // Configure options with reasonable values
        var optionsValue = new ScheduledMessagesOptions
        {
            CheckIntervalSeconds = 60,
            MaxConcurrentExecutions = 5,
            ExecutionTimeoutSeconds = 30
        };
        _options = Options.Create(optionsValue);

        // Setup scope factory to return mocked services
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IScheduledMessageRepository)))
            .Returns(_mockRepository.Object);

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IScheduledMessageService)))
            .Returns(_mockService.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);
    }

    #region Service Initialization Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Assert
        service.Should().NotBeNull();
    }

    // Note: Constructor null parameter tests are not included because the service uses
    // nullable reference types and doesn't throw ArgumentNullException. Null parameters
    // would result in NullReferenceException at runtime when the null values are accessed.

    #endregion

    #region Configuration Tests

    [Fact]
    public void Options_AreCorrectlyConfigured()
    {
        // Arrange & Act
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Assert
        _options.Value.CheckIntervalSeconds.Should().Be(60);
        _options.Value.MaxConcurrentExecutions.Should().Be(5);
        _options.Value.ExecutionTimeoutSeconds.Should().Be(30);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void Options_CheckIntervalSeconds_AcceptsValidValues(int seconds)
    {
        // Arrange
        var options = Options.Create(new ScheduledMessagesOptions
        {
            CheckIntervalSeconds = seconds,
            MaxConcurrentExecutions = 5,
            ExecutionTimeoutSeconds = 30
        });

        // Act
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.CheckIntervalSeconds.Should().Be(seconds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Options_MaxConcurrentExecutions_AcceptsValidValues(int maxConcurrent)
    {
        // Arrange
        var options = Options.Create(new ScheduledMessagesOptions
        {
            CheckIntervalSeconds = 60,
            MaxConcurrentExecutions = maxConcurrent,
            ExecutionTimeoutSeconds = 30
        });

        // Act
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.MaxConcurrentExecutions.Should().Be(maxConcurrent);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void Options_ExecutionTimeoutSeconds_AcceptsValidValues(int timeout)
    {
        // Arrange
        var options = Options.Create(new ScheduledMessagesOptions
        {
            CheckIntervalSeconds = 60,
            MaxConcurrentExecutions = 5,
            ExecutionTimeoutSeconds = timeout
        });

        // Act
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.ExecutionTimeoutSeconds.Should().Be(timeout);
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_WithValidConfiguration_Starts()
    {
        // Arrange
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        var cts = new CancellationTokenSource();

        // Act
        Func<Task> act = async () =>
        {
            await service.StartAsync(cts.Token);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);
        };

        // Assert
        await act.Should().NotThrowAsync("service should start and stop cleanly");
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        // Act
        Func<Task> act = async () => await service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("stopping without starting should be safe");
    }

    [Fact]
    public async Task StartAsync_RespectsCancellationToken()
    {
        // Arrange
        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        var cts = new CancellationTokenSource();

        // Start service
        var startTask = service.StartAsync(cts.Token);

        // Act - Cancel immediately
        await cts.CancelAsync();

        // Assert - Should complete gracefully
        Func<Task> act = async () => await service.StopAsync(CancellationToken.None);
        await act.Should().CompleteWithinAsync(
            TimeSpan.FromSeconds(5),
            "service should respect cancellation and shutdown quickly");
    }

    #endregion

    #region Dependency Injection Tests

    [Fact(Skip = "Timing-sensitive test that depends on 10-second startup delay. " +
                 "Covered by integration tests per class documentation.")]
    public async Task Service_UsesScopeFactory_ForDependencyResolution()
    {
        // Arrange
        var options = Options.Create(new ScheduledMessagesOptions
        {
            CheckIntervalSeconds = 1, // Fast interval for this test
            MaxConcurrentExecutions = 5,
            ExecutionTimeoutSeconds = 30
        });

        var service = new ScheduledMessageExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockLogger.Object,
            _mockServiceProvider.Object);

        _mockRepository
            .Setup(r => r.GetDueMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduledMessage>());

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        // Wait for initial delay (10s) + at least one check cycle
        // Extended to 15s to ensure the service has time to complete the initial delay
        // and enter the processing loop after Task.Yield() in MonitoredBackgroundService
        await Task.Delay(TimeSpan.FromSeconds(15));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockScopeFactory.Verify(
            f => f.CreateScope(),
            Times.AtLeastOnce,
            "service should create scopes for dependency injection");
    }

    #endregion
}
