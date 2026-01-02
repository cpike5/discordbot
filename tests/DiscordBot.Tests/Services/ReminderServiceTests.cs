using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for ReminderService.
/// Tests cover reminder creation, retrieval, cancellation, and counting operations.
/// </summary>
public class ReminderServiceTests
{
    private readonly Mock<IReminderRepository> _mockReminderRepository;
    private readonly Mock<ILogger<ReminderService>> _mockLogger;
    private readonly IOptions<ReminderOptions> _options;
    private readonly ReminderService _service;

    public ReminderServiceTests()
    {
        _mockReminderRepository = new Mock<IReminderRepository>();
        _mockLogger = new Mock<ILogger<ReminderService>>();
        _options = Options.Create(new ReminderOptions
        {
            CheckIntervalSeconds = 30,
            MaxConcurrentDeliveries = 5,
            MaxDeliveryAttempts = 3,
            RetryDelayMinutes = 5,
            MaxRemindersPerUser = 25,
            MaxAdvanceDays = 365,
            MinAdvanceMinutes = 1
        });

        _service = new ReminderService(
            _mockReminderRepository.Object,
            _options,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static Reminder CreateTestReminder(
        Guid? id = null,
        ulong guildId = 123456789UL,
        ulong channelId = 333333UL,
        ulong userId = 111111UL,
        string message = "Test reminder message",
        DateTime? triggerAt = null,
        ReminderStatus status = ReminderStatus.Pending,
        int deliveryAttempts = 0)
    {
        return new Reminder
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            UserId = userId,
            Message = message,
            TriggerAt = triggerAt ?? DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            Status = status,
            DeliveryAttempts = deliveryAttempts
        };
    }

    #endregion

    #region CreateReminderAsync Tests

    [Fact]
    public async Task CreateReminderAsync_WithValidData_CreatesReminder()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong channelId = 333333UL;
        ulong userId = 111111UL;
        string message = "Test reminder";
        DateTime triggerAt = DateTime.UtcNow.AddHours(1);

        _mockReminderRepository
            .Setup(r => r.AddAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder r, CancellationToken _) => r);

        // Act
        var result = await _service.CreateReminderAsync(
            guildId,
            channelId,
            userId,
            message,
            triggerAt);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guildId);
        result.ChannelId.Should().Be(channelId);
        result.UserId.Should().Be(userId);
        result.Message.Should().Be(message);
        result.TriggerAt.Should().Be(triggerAt);

        _mockReminderRepository.Verify(
            r => r.AddAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateReminderAsync_WithValidData_SetsCorrectProperties()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong channelId = 333333UL;
        ulong userId = 111111UL;
        string message = "Test reminder";
        DateTime triggerAt = DateTime.UtcNow.AddHours(1);
        DateTime beforeCreation = DateTime.UtcNow;

        _mockReminderRepository
            .Setup(r => r.AddAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder r, CancellationToken _) => r);

        // Act
        var result = await _service.CreateReminderAsync(
            guildId,
            channelId,
            userId,
            message,
            triggerAt);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Status.Should().Be(ReminderStatus.Pending);
        result.DeliveryAttempts.Should().Be(0);
        result.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        result.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateReminderAsync_WithValidData_CallsRepositoryAdd()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong channelId = 333333UL;
        ulong userId = 111111UL;
        string message = "Test reminder";
        DateTime triggerAt = DateTime.UtcNow.AddHours(1);

        _mockReminderRepository
            .Setup(r => r.AddAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder r, CancellationToken _) => r);

        // Act
        await _service.CreateReminderAsync(
            guildId,
            channelId,
            userId,
            message,
            triggerAt);

        // Assert
        _mockReminderRepository.Verify(
            r => r.AddAsync(It.Is<Reminder>(rem =>
                rem.GuildId == guildId &&
                rem.ChannelId == channelId &&
                rem.UserId == userId &&
                rem.Message == message &&
                rem.TriggerAt == triggerAt &&
                rem.Status == ReminderStatus.Pending &&
                rem.DeliveryAttempts == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateReminderAsync_OnSuccess_LogsInformation()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong channelId = 333333UL;
        ulong userId = 111111UL;
        string message = "Test reminder";
        DateTime triggerAt = DateTime.UtcNow.AddHours(1);

        _mockReminderRepository
            .Setup(r => r.AddAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder r, CancellationToken _) => r);

        // Act
        await _service.CreateReminderAsync(
            guildId,
            channelId,
            userId,
            message,
            triggerAt);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Reminder created")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetUserRemindersAsync Tests

    [Fact]
    public async Task GetUserRemindersAsync_ReturnsResultsFromRepository()
    {
        // Arrange
        ulong userId = 111111UL;
        int page = 1;
        int pageSize = 10;

        var reminders = new List<Reminder>
        {
            CreateTestReminder(userId: userId),
            CreateTestReminder(userId: userId)
        };

        _mockReminderRepository
            .Setup(r => r.GetByUserAsync(userId, page, pageSize, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reminders, reminders.Count));

        // Act
        var result = await _service.GetUserRemindersAsync(userId, page, pageSize);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(r => r.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetUserRemindersAsync_PassesPendingOnlyTrue()
    {
        // Arrange
        ulong userId = 111111UL;
        int page = 1;
        int pageSize = 10;

        _mockReminderRepository
            .Setup(r => r.GetByUserAsync(userId, page, pageSize, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Reminder>(), 0));

        // Act
        await _service.GetUserRemindersAsync(userId, page, pageSize);

        // Assert
        _mockReminderRepository.Verify(
            r => r.GetByUserAsync(
                userId,
                page,
                pageSize,
                true, // pendingOnly should be true
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserRemindersAsync_WithNoResults_ReturnsEmptyCollection()
    {
        // Arrange
        ulong userId = 111111UL;
        int page = 1;
        int pageSize = 10;

        _mockReminderRepository
            .Setup(r => r.GetByUserAsync(userId, page, pageSize, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Reminder>(), 0));

        // Act
        var result = await _service.GetUserRemindersAsync(userId, page, pageSize);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    #endregion

    #region CancelReminderAsync Tests

    [Fact]
    public async Task CancelReminderAsync_WhenReminderNotFound_ReturnsNull()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder?)null);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().BeNull();

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenReminderNotOwnedByUser_ReturnsNull()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong requestingUserId = 111111UL;

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, requestingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder?)null); // GetByIdForUserAsync returns null when not owned

        // Act
        var result = await _service.CancelReminderAsync(reminderId, requestingUserId);

        // Assert
        result.Should().BeNull();

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenReminderIsDelivered_ReturnsNull()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Delivered);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().BeNull();

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenReminderIsCancelled_ReturnsNull()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Cancelled);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().BeNull();

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenReminderIsFailed_ReturnsNull()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Failed);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().BeNull();

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenPending_CancelsSuccessfully()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Pending);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(reminderId);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CancelReminderAsync_OnSuccess_CallsRepositoryUpdate()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Pending);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        _mockReminderRepository.Verify(
            r => r.UpdateAsync(
                It.Is<Reminder>(rem => rem.Id == reminderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelReminderAsync_OnSuccess_SetsStatusToCancelled()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Pending);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ReminderStatus.Cancelled);

        _mockReminderRepository.Verify(
            r => r.UpdateAsync(
                It.Is<Reminder>(rem => rem.Status == ReminderStatus.Cancelled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenNotFound_LogsDebug()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reminder?)null);

        // Act
        await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cancel reminder failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelReminderAsync_WhenNotPending_LogsDebug()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Delivered);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        // Act
        await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("is not pending")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelReminderAsync_OnSuccess_LogsInformation()
    {
        // Arrange
        Guid reminderId = Guid.NewGuid();
        ulong userId = 111111UL;

        var reminder = CreateTestReminder(
            id: reminderId,
            userId: userId,
            status: ReminderStatus.Pending);

        _mockReminderRepository
            .Setup(r => r.GetByIdForUserAsync(reminderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reminder);

        _mockReminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CancelReminderAsync(reminderId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Reminder cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetPendingCountAsync Tests

    [Fact]
    public async Task GetPendingCountAsync_ReturnsCountFromRepository()
    {
        // Arrange
        ulong userId = 111111UL;
        int expectedCount = 5;

        _mockReminderRepository
            .Setup(r => r.GetPendingCountByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _service.GetPendingCountAsync(userId);

        // Assert
        result.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetPendingCountAsync_WhenNoPendingReminders_ReturnsZero()
    {
        // Arrange
        ulong userId = 111111UL;

        _mockReminderRepository
            .Setup(r => r.GetPendingCountByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetPendingCountAsync(userId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingCountAsync_CallsRepositoryMethod()
    {
        // Arrange
        ulong userId = 111111UL;

        _mockReminderRepository
            .Setup(r => r.GetPendingCountByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        await _service.GetPendingCountAsync(userId);

        // Assert
        _mockReminderRepository.Verify(
            r => r.GetPendingCountByUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
