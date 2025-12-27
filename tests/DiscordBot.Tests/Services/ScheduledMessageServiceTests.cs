using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for ScheduledMessageService.
/// Tests cover CRUD operations, message execution, cron validation, and next execution calculations.
/// </summary>
public class ScheduledMessageServiceTests
{
    private readonly Mock<IScheduledMessageRepository> _mockRepository;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<ScheduledMessageService>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly ScheduledMessageService _service;

    public ScheduledMessageServiceTests()
    {
        _mockRepository = new Mock<IScheduledMessageRepository>();
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockLogger = new Mock<ILogger<ScheduledMessageService>>();
        _mockAuditLogService = new Mock<IAuditLogService>();

        // Setup audit log service to return a builder that returns itself for fluent API
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.BySystem()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByBot()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.InGuild(It.IsAny<ulong>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<Dictionary<string, object?>>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithCorrelationId(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        _service = new ScheduledMessageService(
            _mockRepository.Object,
            _mockDiscordClient.Object,
            _mockLogger.Object,
            _mockAuditLogService.Object);
    }

    #region Helper Methods

    private static ScheduledMessage CreateTestScheduledMessage(
        Guid? id = null,
        ulong guildId = 123456789UL,
        ulong channelId = 987654321UL,
        string title = "Test Message",
        string content = "Test Content",
        ScheduleFrequency frequency = ScheduleFrequency.Daily,
        bool isEnabled = true,
        DateTime? nextExecutionAt = null,
        DateTime? lastExecutedAt = null,
        string? cronExpression = null)
    {
        return new ScheduledMessage
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            Title = title,
            Content = content,
            Frequency = frequency,
            IsEnabled = isEnabled,
            NextExecutionAt = nextExecutionAt,
            LastExecutedAt = lastExecutedAt,
            CronExpression = cronExpression,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user",
            UpdatedAt = DateTime.UtcNow,
            Guild = new Guild
            {
                Id = guildId,
                Name = "Test Guild",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            }
        };
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingMessage_ReturnsDto()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateTestScheduledMessage(id: messageId);

        _mockRepository
            .Setup(r => r.GetByIdWithGuildAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var result = await _service.GetByIdAsync(messageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.Title.Should().Be("Test Message");
        result.Content.Should().Be("Test Content");
        result.GuildName.Should().Be("Test Guild");

        _mockRepository.Verify(
            r => r.GetByIdWithGuildAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentMessage_ReturnsNull()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdWithGuildAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage?)null);

        // Act
        var result = await _service.GetByIdAsync(messageId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByGuildIdAsync Tests

    [Fact]
    public async Task GetByGuildIdAsync_ReturnsPaginatedResults()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var messages = new List<ScheduledMessage>
        {
            CreateTestScheduledMessage(title: "Message 1", guildId: guildId),
            CreateTestScheduledMessage(title: "Message 2", guildId: guildId)
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 2));

        // Act
        var (items, totalCount) = await _service.GetByGuildIdAsync(guildId, 1, 10);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().Contain(m => m.Title == "Message 1");
        items.Should().Contain(m => m.Title == "Message 2");

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, 1, 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithNoResults_ReturnsEmpty()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ScheduledMessage>(), 0));

        // Act
        var (items, totalCount) = await _service.GetByGuildIdAsync(guildId, 1, 10);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidDto_CreatesAndReturnsMessage()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = 123456789UL,
            ChannelId = 987654321UL,
            Title = "New Message",
            Content = "New Content",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = DateTime.UtcNow.AddDays(1),
            CreatedBy = "user123"
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage m, CancellationToken ct) => m);

        // Act
        var result = await _service.CreateAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(123456789UL);
        result.ChannelId.Should().Be(987654321UL);
        result.Title.Should().Be("New Message");
        result.Content.Should().Be("New Content");
        result.Frequency.Should().Be(ScheduleFrequency.Daily);
        result.IsEnabled.Should().BeTrue();
        result.CreatedBy.Should().Be("user123");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithCustomFrequency_ValidatesCronExpression()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = 123456789UL,
            ChannelId = 987654321UL,
            Title = "Cron Message",
            Content = "Content",
            Frequency = ScheduleFrequency.Custom,
            CronExpression = "0 0 9 * * *", // Valid cron: 9 AM daily
            IsEnabled = true,
            CreatedBy = "user123"
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage m, CancellationToken ct) => m);

        // Act
        var result = await _service.CreateAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.Frequency.Should().Be(ScheduleFrequency.Custom);
        result.CronExpression.Should().Be("0 0 9 * * *");
    }

    [Fact]
    public async Task CreateAsync_WithCustomFrequency_AndNullCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = 123456789UL,
            ChannelId = 987654321UL,
            Title = "Invalid Message",
            Content = "Content",
            Frequency = ScheduleFrequency.Custom,
            CronExpression = null,
            IsEnabled = true,
            CreatedBy = "user123"
        };

        // Act
        Func<Task> act = async () => await _service.CreateAsync(createDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Cron expression is required*");
    }

    [Fact]
    public async Task CreateAsync_WithCustomFrequency_AndInvalidCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = 123456789UL,
            ChannelId = 987654321UL,
            Title = "Invalid Cron",
            Content = "Content",
            Frequency = ScheduleFrequency.Custom,
            CronExpression = "invalid cron",
            IsEnabled = true,
            CreatedBy = "user123"
        };

        // Act
        Func<Task> act = async () => await _service.CreateAsync(createDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid cron expression*");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithExistingMessage_UpdatesAndReturnsDto()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingMessage = CreateTestScheduledMessage(id: messageId, title: "Old Title");

        var updateDto = new ScheduledMessageUpdateDto
        {
            Title = "Updated Title",
            Content = "Updated Content",
            IsEnabled = false
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMessage);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAsync(messageId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.Title.Should().Be("Updated Title");
        result.Content.Should().Be("Updated Content");
        result.IsEnabled.Should().BeFalse();

        _mockRepository.Verify(
            r => r.UpdateAsync(It.Is<ScheduledMessage>(m =>
                m.Id == messageId &&
                m.Title == "Updated Title" &&
                m.Content == "Updated Content" &&
                m.IsEnabled == false),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentMessage_ReturnsNull()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var updateDto = new ScheduledMessageUpdateDto { Title = "Updated" };

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage?)null);

        // Act
        var result = await _service.UpdateAsync(messageId, updateDto);

        // Assert
        result.Should().BeNull();

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithPartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingMessage = CreateTestScheduledMessage(
            id: messageId,
            title: "Original Title",
            content: "Original Content",
            isEnabled: true);

        var updateDto = new ScheduledMessageUpdateDto
        {
            Title = "New Title"
            // Only updating title, other fields should remain unchanged
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMessage);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAsync(messageId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("New Title");
        result.Content.Should().Be("Original Content"); // Unchanged
        result.IsEnabled.Should().BeTrue(); // Unchanged
    }

    [Fact]
    public async Task UpdateAsync_WithCustomFrequency_ValidatesCronExpression()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingMessage = CreateTestScheduledMessage(id: messageId, frequency: ScheduleFrequency.Daily);

        var updateDto = new ScheduledMessageUpdateDto
        {
            Frequency = ScheduleFrequency.Custom,
            CronExpression = "invalid cron"
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMessage);

        // Act
        Func<Task> act = async () => await _service.UpdateAsync(messageId, updateDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid cron expression*");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithExistingMessage_DeletesAndReturnsTrue()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateTestScheduledMessage(id: messageId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _mockRepository
            .Setup(r => r.DeleteAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteAsync(messageId);

        // Assert
        result.Should().BeTrue();

        _mockRepository.Verify(
            r => r.DeleteAsync(It.Is<ScheduledMessage>(m => m.Id == messageId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentMessage_ReturnsFalse()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage?)null);

        // Act
        var result = await _service.DeleteAsync(messageId);

        // Assert
        result.Should().BeFalse();

        _mockRepository.Verify(
            r => r.DeleteAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CalculateNextExecutionAsync Tests

    [Fact]
    public async Task CalculateNextExecutionAsync_WithOnceFrequency_ReturnsNull()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Once, null, baseTime);

        // Assert
        result.Should().BeNull("Once frequency should not have a next execution");
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithHourlyFrequency_ReturnsOneHourLater()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Hourly, null, baseTime);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithDailyFrequency_ReturnsOneDayLater()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Daily, null, baseTime);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithWeeklyFrequency_ReturnsOneWeekLater()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Weekly, null, baseTime);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(new DateTime(2025, 1, 8, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithMonthlyFrequency_ReturnsOneMonthLater()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Monthly, null, baseTime);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(new DateTime(2025, 2, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithCustomFrequency_AndValidCron_ReturnsNextOccurrence()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        const string cronExpression = "0 0 9 * * *"; // 9 AM daily

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Custom, cronExpression, baseTime);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Hour.Should().Be(9);
        result.Value.Minute.Should().Be(0);
        result.Value.Should().BeAfter(baseTime);
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithCustomFrequency_AndNullCron_ReturnsNull()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Custom, null, baseTime);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateNextExecutionAsync_WithCustomFrequency_AndInvalidCron_ReturnsNull()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        const string invalidCron = "invalid cron expression";

        // Act
        var result = await _service.CalculateNextExecutionAsync(ScheduleFrequency.Custom, invalidCron, baseTime);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ValidateCronExpressionAsync Tests

    [Fact]
    public async Task ValidateCronExpressionAsync_WithValidExpression_ReturnsTrue()
    {
        // Arrange
        const string validCron = "0 0 9 * * *"; // 9 AM daily

        // Act
        var (isValid, errorMessage) = await _service.ValidateCronExpressionAsync(validCron);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_WithInvalidExpression_ReturnsFalseWithError()
    {
        // Arrange
        const string invalidCron = "invalid cron";

        // Act
        var (isValid, errorMessage) = await _service.ValidateCronExpressionAsync(invalidCron);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("Invalid cron expression");
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_WithNullExpression_ReturnsFalseWithError()
    {
        // Arrange
        string? nullCron = null;

        // Act
        var (isValid, errorMessage) = await _service.ValidateCronExpressionAsync(nullCron!);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be("Cron expression cannot be empty");
    }

    [Fact]
    public async Task ValidateCronExpressionAsync_WithEmptyExpression_ReturnsFalseWithError()
    {
        // Arrange
        const string emptyCron = "";

        // Act
        var (isValid, errorMessage) = await _service.ValidateCronExpressionAsync(emptyCron);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be("Cron expression cannot be empty");
    }

    #endregion

    #region ExecuteScheduledMessageAsync Tests

    // Note: Tests that require mocking Discord.NET's SocketChannel (SendsMessageAndUpdatesState, DisablesMessageAfterExecution)
    // are not included here because SocketChannel is a concrete class that cannot be mocked with Moq.
    // These scenarios should be covered by integration tests instead.

    [Fact]
    public async Task ExecuteScheduledMessageAsync_WithNonExistentMessage_ReturnsFalse()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessage?)null);

        // Act
        var result = await _service.ExecuteScheduledMessageAsync(messageId);

        // Assert
        result.Should().BeFalse();

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteScheduledMessageAsync_WithNonExistentChannel_ReturnsFalse()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = CreateTestScheduledMessage(id: messageId);

        _mockDiscordClient
            .Setup(c => c.GetChannel(message.ChannelId))
            .Returns((SocketChannel?)null);

        _mockRepository
            .Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var result = await _service.ExecuteScheduledMessageAsync(messageId);

        // Assert
        result.Should().BeFalse();

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ScheduledMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Note: ExecuteScheduledMessageAsync_WhenSendMessageFails test removed because
    // SocketChannel is a concrete class that cannot be mocked with Moq.
    // This scenario should be covered by integration tests instead.

    #endregion
}
