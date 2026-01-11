using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for TtsHistoryService.
/// Tests cover message logging, retrieval, stats calculation, and deletion.
/// </summary>
public class TtsHistoryServiceTests
{
    private readonly Mock<ITtsMessageRepository> _mockMessageRepository;
    private readonly Mock<ILogger<TtsHistoryService>> _mockLogger;
    private readonly TtsHistoryService _service;

    public TtsHistoryServiceTests()
    {
        _mockMessageRepository = new Mock<ITtsMessageRepository>();
        _mockLogger = new Mock<ILogger<TtsHistoryService>>();

        _service = new TtsHistoryService(
            _mockMessageRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static TtsMessage CreateTestMessage(
        Guid? id = null,
        ulong guildId = 123456789UL,
        ulong userId = 987654321UL,
        string username = "TestUser",
        string message = "Hello, world!",
        string voice = "default",
        double durationSeconds = 2.5,
        DateTime? createdAt = null)
    {
        return new TtsMessage
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            Username = username,
            Message = message,
            Voice = voice,
            DurationSeconds = durationSeconds,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    #endregion

    #region LogMessageAsync Tests

    [Fact]
    public async Task LogMessageAsync_AddsMessageViaRepository()
    {
        // Arrange
        var message = CreateTestMessage();

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        await _service.LogMessageAsync(message);

        // Assert
        _mockMessageRepository.Verify(
            r => r.AddAsync(It.Is<TtsMessage>(m =>
                m.GuildId == message.GuildId &&
                m.UserId == message.UserId &&
                m.Message == message.Message),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogMessageAsync_GeneratesIdIfNotSet()
    {
        // Arrange
        var message = CreateTestMessage();
        message.Id = Guid.Empty;
        Guid capturedId = Guid.Empty;

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TtsMessage, CancellationToken>((m, _) => capturedId = m.Id)
            .ReturnsAsync(message);

        // Act
        await _service.LogMessageAsync(message);

        // Assert
        capturedId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task LogMessageAsync_SetsCreatedAtIfDefault()
    {
        // Arrange
        var message = CreateTestMessage();
        message.CreatedAt = default;
        DateTime capturedCreatedAt = default;

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TtsMessage, CancellationToken>((m, _) => capturedCreatedAt = m.CreatedAt)
            .ReturnsAsync(message);

        var beforeCall = DateTime.UtcNow;

        // Act
        await _service.LogMessageAsync(message);

        var afterCall = DateTime.UtcNow;

        // Assert
        capturedCreatedAt.Should().BeOnOrAfter(beforeCall);
        capturedCreatedAt.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public async Task LogMessageAsync_PreservesExistingId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var message = CreateTestMessage(id: existingId);
        Guid capturedId = Guid.Empty;

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TtsMessage, CancellationToken>((m, _) => capturedId = m.Id)
            .ReturnsAsync(message);

        // Act
        await _service.LogMessageAsync(message);

        // Assert
        capturedId.Should().Be(existingId);
    }

    #endregion

    #region GetRecentMessagesAsync Tests

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsMessagesAsDtos()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var messages = new List<TtsMessage>
        {
            CreateTestMessage(guildId: guildId, username: "User1", message: "Hello"),
            CreateTestMessage(guildId: guildId, username: "User2", message: "World")
        };

        _mockMessageRepository.Setup(r => r.GetRecentByGuildAsync(guildId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _service.GetRecentMessagesAsync(guildId, 20);

        // Assert
        result.Should().HaveCount(2);
        result.First().Username.Should().Be("User1");
        result.First().Message.Should().Be("Hello");
        result.Last().Username.Should().Be("User2");
        result.Last().Message.Should().Be("World");
    }

    [Fact]
    public async Task GetRecentMessagesAsync_WithCustomCount_PassesCountToRepository()
    {
        // Arrange
        ulong guildId = 123456789UL;
        int customCount = 50;

        _mockMessageRepository.Setup(r => r.GetRecentByGuildAsync(guildId, customCount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TtsMessage>());

        // Act
        await _service.GetRecentMessagesAsync(guildId, customCount);

        // Assert
        _mockMessageRepository.Verify(
            r => r.GetRecentByGuildAsync(guildId, customCount, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_WhenNoMessages_ReturnsEmptyList()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockMessageRepository.Setup(r => r.GetRecentByGuildAsync(guildId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TtsMessage>());

        // Act
        var result = await _service.GetRecentMessagesAsync(guildId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentMessagesAsync_MapsAllDtoProperties()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var messageId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var message = CreateTestMessage(
            id: messageId,
            guildId: guildId,
            userId: 111111UL,
            username: "TestUser",
            message: "Test message",
            voice: "custom-voice",
            durationSeconds: 3.5,
            createdAt: createdAt);

        _mockMessageRepository.Setup(r => r.GetRecentByGuildAsync(guildId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TtsMessage> { message });

        // Act
        var result = (await _service.GetRecentMessagesAsync(guildId)).Single();

        // Assert
        result.Id.Should().Be(messageId);
        result.UserId.Should().Be(111111UL);
        result.Username.Should().Be("TestUser");
        result.Message.Should().Be("Test message");
        result.Voice.Should().Be("custom-voice");
        result.DurationSeconds.Should().Be(3.5);
        result.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsAggregatedStats()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockMessageRepository.Setup(r => r.GetMessageCountAsync(guildId, It.Is<DateTime>(d => d.Date == DateTime.UtcNow.Date), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _mockMessageRepository.Setup(r => r.GetMessageCountAsync(guildId, It.Is<DateTime>(d => d == DateTime.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockMessageRepository.Setup(r => r.GetTotalPlaybackSecondsAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(300.5);

        _mockMessageRepository.Setup(r => r.GetUniqueUserCountAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockMessageRepository.Setup(r => r.GetMostUsedVoiceAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("popular-voice");

        _mockMessageRepository.Setup(r => r.GetTopUserAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((222222UL, "TopUser", 25));

        // Act
        var result = await _service.GetStatsAsync(guildId);

        // Assert
        result.GuildId.Should().Be(guildId);
        result.MessagesToday.Should().Be(10);
        result.TotalMessages.Should().Be(100);
        result.TotalPlaybackSeconds.Should().Be(300.5);
        result.UniqueUsers.Should().Be(5);
        result.MostUsedVoice.Should().Be("popular-voice");
        result.TopUserId.Should().Be(222222UL);
        result.TopUsername.Should().Be("TopUser");
        result.TopUserMessageCount.Should().Be(25);
    }

    [Fact]
    public async Task GetStatsAsync_WhenNoTopUser_ReturnsNullTopUserFields()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockMessageRepository.Setup(r => r.GetMessageCountAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockMessageRepository.Setup(r => r.GetTotalPlaybackSecondsAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockMessageRepository.Setup(r => r.GetUniqueUserCountAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockMessageRepository.Setup(r => r.GetMostUsedVoiceAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockMessageRepository.Setup(r => r.GetTopUserAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((ulong, string, int)?)null);

        // Act
        var result = await _service.GetStatsAsync(guildId);

        // Assert
        result.TopUserId.Should().BeNull();
        result.TopUsername.Should().BeNull();
        result.TopUserMessageCount.Should().Be(0);
        result.MostUsedVoice.Should().BeNull();
    }

    #endregion

    #region DeleteMessageAsync Tests

    [Fact]
    public async Task DeleteMessageAsync_WhenMessageExists_ReturnsTrue()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockMessageRepository.Setup(r => r.DeleteAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteMessageAsync(messageId);

        // Assert
        result.Should().BeTrue();
        _mockMessageRepository.Verify(
            r => r.DeleteAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenMessageNotExists_ReturnsFalse()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockMessageRepository.Setup(r => r.DeleteAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteMessageAsync(messageId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
