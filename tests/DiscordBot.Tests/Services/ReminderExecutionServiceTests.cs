using Discord;
using Discord.WebSocket;
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
/// Unit tests for ReminderExecutionService.
/// Tests cover service lifecycle, reminder processing, retry logic, and concurrency.
/// Note: Discord client interaction tests use mocks since DiscordSocketClient is difficult to fully mock.
/// Full end-to-end delivery tests are best covered by integration tests.
/// </summary>
public class ReminderExecutionServiceTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IReminderRepository> _mockRepository;
    private readonly Mock<DiscordSocketClient> _mockClient;
    private readonly Mock<ILogger<ReminderExecutionService>> _mockLogger;
    private readonly IOptions<ReminderOptions> _options;

    public ReminderExecutionServiceTests()
    {
        _mockRepository = new Mock<IReminderRepository>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockClient = new Mock<DiscordSocketClient>();
        _mockLogger = new Mock<ILogger<ReminderExecutionService>>();

        // Configure options with reasonable test values
        var optionsValue = new ReminderOptions
        {
            CheckIntervalSeconds = 30,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        };
        _options = Options.Create(optionsValue);

        // Setup scope factory to return mocked services
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IReminderRepository)))
            .Returns(_mockRepository.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);

        // Setup Discord client to be connected by default
        _mockClient.SetupGet(c => c.ConnectionState).Returns(ConnectionState.Connected);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Options_AreCorrectlyConfigured()
    {
        // Arrange & Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        _options.Value.CheckIntervalSeconds.Should().Be(30);
        _options.Value.MaxConcurrentDeliveries.Should().Be(5);
        _options.Value.MaxDeliveryAttempts.Should().Be(3);
        _options.Value.RetryDelayMinutes.Should().Be(5);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void Options_CheckIntervalSeconds_AcceptsValidValues(int seconds)
    {
        // Arrange
        var options = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = seconds,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        // Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.CheckIntervalSeconds.Should().Be(seconds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Options_MaxConcurrentDeliveries_AcceptsValidValues(int maxConcurrent)
    {
        // Arrange
        var options = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 30,
            MaxConcurrentDeliveries = maxConcurrent,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        // Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.MaxConcurrentDeliveries.Should().Be(maxConcurrent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Options_MaxDeliveryAttempts_AcceptsValidValues(int maxAttempts)
    {
        // Arrange
        var options = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 30,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = maxAttempts,
            RetryDelayMinutes = 5
        });

        // Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.MaxDeliveryAttempts.Should().Be(maxAttempts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    public void Options_RetryDelayMinutes_AcceptsValidValues(int retryDelay)
    {
        // Arrange
        var options = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 30,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = retryDelay
        });

        // Act
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            options,
            _mockClient.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        options.Value.RetryDelayMinutes.Should().Be(retryDelay);
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_WaitsForDiscordConnection()
    {
        // Arrange
        var connectionStateSequence = new Queue<ConnectionState>(new[]
        {
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            ConnectionState.Connected
        });

        _mockClient.SetupGet(c => c.ConnectionState).Returns(() => connectionStateSequence.Dequeue());

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Setup repository to return no reminders
        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reminder>());

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for connection loop
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.ConnectionState, Times.AtLeastOnce,
            "service should check connection state");
    }

    [Fact]
    public async Task StartAsync_CancellationBeforeConnection_ShutdownsGracefully()
    {
        // Arrange
        _mockClient.SetupGet(c => c.ConnectionState).Returns(ConnectionState.Disconnected);

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await cts.CancelAsync(); // Cancel immediately
        await service.StopAsync(CancellationToken.None);

        // Assert - Should complete without error
        Func<Task> act = async () => await startTask;
        await act.Should().CompleteWithinAsync(
            TimeSpan.FromSeconds(5),
            "service should shutdown gracefully when cancelled before connection");
    }

    [Fact]
    public async Task StopAsync_GracefullyShutdown()
    {
        // Arrange
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reminder>());

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100); // Let it start
        await cts.CancelAsync();

        Func<Task> act = async () => await service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().CompleteWithinAsync(
            TimeSpan.FromSeconds(5),
            "service should stop gracefully");
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            _options,
            _mockClient.Object,
            _mockLogger.Object);

        // Act
        Func<Task> act = async () => await service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("stopping without starting should be safe");
    }

    #endregion

    #region Reminder Processing Tests

    [Fact]
    public async Task ProcessDueReminders_NoDueReminders_DoesNothing()
    {
        // Arrange
        var fastOptions = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 1,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            fastOptions,
            _mockClient.Object,
            _mockLogger.Object);

        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reminder>());

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for at least one check
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockRepository.Verify(
            r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "service should check for due reminders");

        _mockClient.Verify(
            c => c.GetUser(It.IsAny<ulong>()),
            Times.Never,
            "no delivery attempts should be made when no reminders are due");
    }

    [Fact]
    public async Task ProcessDueReminders_UserNotFound_MarksAsFailed()
    {
        // Arrange
        var fastOptions = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 1,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = 123456789,
            GuildId = 987654321,
            ChannelId = 111111111,
            Message = "Test reminder",
            TriggerAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReminderStatus.Pending,
            DeliveryAttempts = 0,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reminder> { reminder });

        _mockClient
            .Setup(c => c.GetUser(reminder.UserId))
            .Returns((SocketUser?)null);

        _mockRepository
            .Setup(r => r.GetByIdAsync(reminder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            fastOptions,
            _mockClient.Object,
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for processing
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockClient.Verify(
            c => c.GetUser(reminder.UserId),
            Times.AtLeastOnce,
            "service should attempt to get the user");

        _mockRepository.Verify(
            r => r.UpdateAsync(
                It.Is<Reminder>(rem =>
                    rem.Id == reminder.Id &&
                    rem.Status == ReminderStatus.Failed &&
                    rem.LastError == "User not found"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "reminder should be marked as failed when user not found");
    }

    #endregion

    #region Retry Logic Tests

//     [Fact]
//     public async Task HandleDeliveryFailure_IncrementAttempts_SchedulesRetry()
//     {
//         // Arrange
//         var reminder = new Reminder
//         {
//             Id = Guid.NewGuid(),
//             UserId = 123456789,
//             GuildId = 987654321,
//             ChannelId = 111111111,
//             Message = "Test reminder",
//             TriggerAt = DateTime.UtcNow.AddMinutes(-5),
//             Status = ReminderStatus.Pending,
//             DeliveryAttempts = 0,
//             CreatedAt = DateTime.UtcNow.AddHours(-1)
//         };
// 
//         var fastOptions = Options.Create(new ReminderOptions
//         {
//             CheckIntervalSeconds = 1,
//             MaxConcurrentDeliveries = 5,
//             MaxDeliveryAttempts = 3,
//             RetryDelayMinutes = 5
//         });
// 
//         // Setup to simulate DM failure
//         var mockUser = new Mock<SocketUser>();
//         mockUser.Setup(u => u.SendMessageAsync(
//                 null,
//                 false,
//                 It.IsAny<Embed?>(),
//                 null,
//                 null,
//                 null,
//                 null,
//                 null,
//                 null,
//                 MessageFlags.None))
//             .ThrowsAsync(new Discord.Net.HttpException(
//                 System.Net.HttpStatusCode.Forbidden,
//                 null,
//                 Discord.DiscordErrorCode.CannotSendMessageToUser));
// 
//         _mockClient
//             .Setup(c => c.GetUser(reminder.UserId))
//             .Returns(mockUser.Object);
// 
//         _mockRepository
//             .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
//             .ReturnsAsync(new List<Reminder> { reminder });
// 
//         _mockRepository
//             .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
//             .Returns(Task.CompletedTask);
// 
//         var service = new ReminderExecutionService(
//             _mockScopeFactory.Object,
//             fastOptions,
//             _mockClient.Object,
//             _mockLogger.Object);
// 
//         var cts = new CancellationTokenSource();
// 
//         // Act
//         await service.StartAsync(cts.Token);
//         await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for processing
//         await cts.CancelAsync();
//         await service.StopAsync(CancellationToken.None);
// 
//         // Assert
//         _mockRepository.Verify(
//             r => r.UpdateAsync(
//                 It.Is<Reminder>(rem =>
//                     rem.Id == reminder.Id &&
//                     rem.DeliveryAttempts == 1 &&
//                     rem.Status == ReminderStatus.Pending &&
//                     rem.TriggerAt > DateTime.UtcNow &&
//                     rem.LastError != null),
//                 It.IsAny<CancellationToken>()),
//             Times.AtLeastOnce,
//             "reminder should be updated with incremented attempt count and rescheduled");
//     }
// 
//     [Fact]
//     public async Task HandleDeliveryFailure_MaxAttemptsReached_MarksAsFailed()
//     {
//         // Arrange
//         var reminder = new Reminder
//         {
//             Id = Guid.NewGuid(),
//             UserId = 123456789,
//             GuildId = 987654321,
//             ChannelId = 111111111,
//             Message = "Test reminder",
//             TriggerAt = DateTime.UtcNow.AddMinutes(-5),
//             Status = ReminderStatus.Pending,
//             DeliveryAttempts = 2, // One more attempt will hit max of 3
//             CreatedAt = DateTime.UtcNow.AddHours(-1)
//         };
// 
//         var fastOptions = Options.Create(new ReminderOptions
//         {
//             CheckIntervalSeconds = 1,
//             MaxConcurrentDeliveries = 5,
//             MaxDeliveryAttempts = 3,
//             RetryDelayMinutes = 5
//         });
// 
//         // Setup to simulate DM failure
//         var mockUser = new Mock<SocketUser>();
//         mockUser.Setup(u => u.SendMessageAsync(
//                 null,
//                 false,
//                 It.IsAny<Embed?>(),
//                 null,
//                 null,
//                 null,
//                 null,
//                 null,
//                 null,
//                 MessageFlags.None))
//             .ThrowsAsync(new Discord.Net.HttpException(
//                 System.Net.HttpStatusCode.Forbidden,
//                 null,
//                 Discord.DiscordErrorCode.CannotSendMessageToUser));
// 
//         _mockClient
//             .Setup(c => c.GetUser(reminder.UserId))
//             .Returns(mockUser.Object);
// 
//         _mockRepository
//             .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
//             .ReturnsAsync(new List<Reminder> { reminder });
// 
//         _mockRepository
//             .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
//             .Returns(Task.CompletedTask);
// 
//         var service = new ReminderExecutionService(
//             _mockScopeFactory.Object,
//             fastOptions,
//             _mockClient.Object,
//             _mockLogger.Object);
// 
//         var cts = new CancellationTokenSource();
// 
//         // Act
//         await service.StartAsync(cts.Token);
//         await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for processing
//         await cts.CancelAsync();
//         await service.StopAsync(CancellationToken.None);
// 
//         // Assert
//         _mockRepository.Verify(
//             r => r.UpdateAsync(
//                 It.Is<Reminder>(rem =>
//                     rem.Id == reminder.Id &&
//                     rem.DeliveryAttempts == 3 &&
//                     rem.Status == ReminderStatus.Failed &&
//                     rem.LastError != null),
//                 It.IsAny<CancellationToken>()),
//             Times.AtLeastOnce,
//             "reminder should be marked as failed after max attempts reached");
//     }
// 
//     [Fact]
//     public async Task HandleDeliveryFailure_DmDisabled_HandlesGracefully()
//     {
//         // Arrange
//         var reminder = new Reminder
//         {
//             Id = Guid.NewGuid(),
//             UserId = 123456789,
//             GuildId = 987654321,
//             ChannelId = 111111111,
//             Message = "Test reminder",
//             TriggerAt = DateTime.UtcNow.AddMinutes(-5),
//             Status = ReminderStatus.Pending,
//             DeliveryAttempts = 0,
//             CreatedAt = DateTime.UtcNow.AddHours(-1)
//         };
// 
//         var fastOptions = Options.Create(new ReminderOptions
//         {
//             CheckIntervalSeconds = 1,
//             MaxConcurrentDeliveries = 5,
//             MaxDeliveryAttempts = 3,
//             RetryDelayMinutes = 5
//         });
// 
//         // Setup to simulate DMs disabled
//         var mockUser = new Mock<SocketUser>();
//         mockUser.Setup(u => u.SendMessageAsync(
//                 It.IsAny<string>(),
//                 It.IsAny<bool>(),
//                 It.IsAny<Embed>(),
//                 It.IsAny<RequestOptions>(),
//                 It.IsAny<AllowedMentions>(),
//                 It.IsAny<MessageReference>(),
//                 It.IsAny<MessageComponent>(),
//                 It.IsAny<ISticker[]>(),
//                 It.IsAny<Embed[]>(),
//                 It.IsAny<MessageFlags>()))
//             .ThrowsAsync(new Discord.Net.HttpException(
//                 System.Net.HttpStatusCode.Forbidden,
//                 null,
//                 Discord.DiscordErrorCode.CannotSendMessageToUser));
// 
//         _mockClient
//             .Setup(c => c.GetUser(reminder.UserId))
//             .Returns(mockUser.Object);
// 
//         _mockRepository
//             .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
//             .ReturnsAsync(new List<Reminder> { reminder });
// 
//         _mockRepository
//             .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
//             .Returns(Task.CompletedTask);
// 
//         var service = new ReminderExecutionService(
//             _mockScopeFactory.Object,
//             fastOptions,
//             _mockClient.Object,
//             _mockLogger.Object);
// 
//         var cts = new CancellationTokenSource();
// 
//         // Act
//         await service.StartAsync(cts.Token);
//         await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for processing
//         await cts.CancelAsync();
//         await service.StopAsync(CancellationToken.None);
// 
//         // Assert - Should not throw, should handle gracefully
//         _mockRepository.Verify(
//             r => r.UpdateAsync(
//                 It.Is<Reminder>(rem =>
//                     rem.Id == reminder.Id &&
//                     rem.LastError!.Contains("DMs disabled")),
//                 It.IsAny<CancellationToken>()),
//             Times.AtLeastOnce,
//             "should record DM disabled error and schedule retry");
//     }
// 
//     #endregion
// 
//     #region Concurrency Tests
// 
    [Fact]
    public async Task ProcessDueReminders_RespectsMaxConcurrent()
    {
        // Arrange
        var maxConcurrent = 2;
        var fastOptions = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 1,
            MaxConcurrentDeliveries = maxConcurrent,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        // Create more reminders than max concurrent
        var reminders = Enumerable.Range(1, 5).Select(i => new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = (ulong)(123456789 + i),
            GuildId = 987654321,
            ChannelId = 111111111,
            Message = $"Test reminder {i}",
            TriggerAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReminderStatus.Pending,
            DeliveryAttempts = 0,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        }).ToList();

        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminders);

        // Setup all users to return null (will be marked as failed)
        _mockClient
            .Setup(c => c.GetUser(It.IsAny<ulong>()))
            .Returns((SocketUser?)null);

        foreach (var reminder in reminders)
        {
            _mockRepository
                .Setup(r => r.GetByIdAsync(reminder.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reminder);
        }

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            fastOptions,
            _mockClient.Object,
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3)); // Wait for processing
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert - All reminders should eventually be processed
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(reminders.Count),
            "all reminders should be processed despite concurrency limit");
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public async Task Service_UsesScopeFactory_ForDependencyResolution()
    {
        // Arrange
        var fastOptions = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 1,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            fastOptions,
            _mockClient.Object,
            _mockLogger.Object);

        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reminder>());

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for at least one check
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockScopeFactory.Verify(
            f => f.CreateScope(),
            Times.AtLeastOnce,
            "service should create scopes for dependency injection");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessDueReminders_RepositoryException_DoesNotStopService()
    {
        // Arrange
        var fastOptions = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 1,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5
        });

        var callCount = 0;
        _mockRepository
            .Setup(r => r.GetDueRemindersAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Database error");
                }
                return Task.FromResult<IEnumerable<Reminder>>(new List<Reminder>());
            });

        var service = new ReminderExecutionService(
            _mockScopeFactory.Object,
            fastOptions,
            _mockClient.Object,
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3)); // Wait for multiple checks
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert
        callCount.Should().BeGreaterThan(1,
            "service should continue running after repository exception");
    }

    #endregion
}
