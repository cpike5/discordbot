using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

using CoreMessageSource = DiscordBot.Core.Enums.MessageSource;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MessageLoggingHandler"/>.
/// Tests cover message filtering, consent checking, and message logging for both DMs and server channels.
/// </summary>
public class MessageLoggingHandlerTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IConsentService> _mockConsentService;
    private readonly Mock<IMessageLogRepository> _mockMessageLogRepository;
    private readonly Mock<ILogger<MessageLoggingHandler>> _mockLogger;
    private readonly MessageLoggingHandler _handler;

    public MessageLoggingHandlerTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockConsentService = new Mock<IConsentService>();
        _mockMessageLogRepository = new Mock<IMessageLogRepository>();
        _mockLogger = new Mock<ILogger<MessageLoggingHandler>>();

        // Setup service scope chain
        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IConsentService)))
            .Returns(_mockConsentService.Object);

        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IMessageLogRepository)))
            .Returns(_mockMessageLogRepository.Object);

        _handler = new MessageLoggingHandler(_mockScopeFactory.Object, _mockLogger.Object);
    }

    #region Bot Message Filtering Tests

    [Fact]
    public async Task HandleMessageAsync_FiltersOutBotMessages()
    {
        // Arrange
        var mockMessage = new Mock<IDiscordMessage>();
        mockMessage.SetupGet(m => m.Id).Returns(987654321UL);
        mockMessage.SetupGet(m => m.AuthorId).Returns(123456789UL);
        mockMessage.SetupGet(m => m.IsAuthorBot).Returns(true);
        mockMessage.SetupGet(m => m.ChannelId).Returns(444555666UL);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockConsentService.Verify(
            c => c.HasConsentAsync(It.IsAny<ulong>(), It.IsAny<ConsentType>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not check consent for bot messages");

        _mockMessageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not log bot messages");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping bot message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log trace message about skipping bot message");
    }

    #endregion

    #region System Message Filtering Tests

    [Fact]
    public async Task HandleMessageAsync_FiltersOutSystemMessages()
    {
        // Arrange - Create a message that is not a user message
        var mockMessage = new Mock<IDiscordMessage>();
        mockMessage.SetupGet(m => m.Id).Returns(987654321UL);
        mockMessage.SetupGet(m => m.AuthorId).Returns(123456789UL);
        mockMessage.SetupGet(m => m.IsAuthorBot).Returns(false);
        mockMessage.SetupGet(m => m.IsUserMessage).Returns(false); // System message
        mockMessage.SetupGet(m => m.ChannelId).Returns(444555666UL);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockConsentService.Verify(
            c => c.HasConsentAsync(It.IsAny<ulong>(), It.IsAny<ConsentType>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not check consent for system messages");

        _mockMessageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not log system messages");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping system message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log trace message about skipping system message");
    }

    #endregion

    #region Consent Checking Tests

    [Fact]
    public async Task HandleMessageAsync_SkipsLogging_WhenUserHasNoConsent()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockConsentService.Verify(
            c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()),
            Times.Once,
            "should check for message logging consent");

        _mockMessageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not log message when user has not granted consent");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("has not granted message logging consent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message about missing consent");
    }

    [Fact]
    public async Task HandleMessageAsync_LogsMessage_WhenUserHasConsent()
    {
        // Arrange
        var userId = 123456789UL;
        var messageId = 987654321UL;
        var channelId = 111222333UL;
        var mockMessage = CreateMockUserMessage(userId, messageId, channelId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockConsentService.Verify(
            c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "should log message when user has granted consent");

        capturedLog.Should().NotBeNull();
        capturedLog!.AuthorId.Should().Be(userId);
        capturedLog.DiscordMessageId.Should().Be(messageId);
        capturedLog.ChannelId.Should().Be(channelId);
    }

    #endregion

    #region Direct Message Tests

    [Fact]
    public async Task HandleMessageAsync_SetsDirectMessageSource_ForDMChannel()
    {
        // Arrange
        var userId = 123456789UL;
        var messageId = 987654321UL;
        var channelId = 444555666UL;
        var mockMessage = CreateMockUserMessage(userId, messageId, channelId, isDM: true);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.Source.Should().Be(CoreMessageSource.DirectMessage, "message is from DM channel");
        capturedLog.GuildId.Should().BeNull("DM messages have no guild ID");
    }

    #endregion

    #region Server Channel Tests

    [Fact]
    public async Task HandleMessageAsync_SetsServerChannelSource_ForGuildChannel()
    {
        // Arrange
        var userId = 123456789UL;
        var messageId = 987654321UL;
        var channelId = 444555666UL;
        var guildId = 777888999UL;
        var mockMessage = CreateMockUserMessage(userId, messageId, channelId, isDM: false, guildId: guildId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.Source.Should().Be(CoreMessageSource.ServerChannel, "message is from guild channel");
        capturedLog.GuildId.Should().Be(guildId, "guild messages should have guild ID");
    }

    #endregion

    #region Message Metadata Tests

    [Fact]
    public async Task HandleMessageAsync_CapturesMessageContent()
    {
        // Arrange
        var userId = 123456789UL;
        var messageContent = "Hello, world!";
        var mockMessage = CreateMockUserMessage(userId, content: messageContent);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.Content.Should().Be(messageContent, "message content should be captured");
    }

    [Fact]
    public async Task HandleMessageAsync_HandlesEmptyContent()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, content: string.Empty);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.Content.Should().Be(string.Empty, "empty content should be preserved");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesAttachmentFlag_WhenMessageHasAttachments()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, hasAttachments: true);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.HasAttachments.Should().BeTrue("message has attachments");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesAttachmentFlag_WhenMessageHasNoAttachments()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, hasAttachments: false);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.HasAttachments.Should().BeFalse("message has no attachments");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesEmbedFlag_WhenMessageHasEmbeds()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, hasEmbeds: true);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.HasEmbeds.Should().BeTrue("message has embeds");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesEmbedFlag_WhenMessageHasNoEmbeds()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, hasEmbeds: false);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.HasEmbeds.Should().BeFalse("message has no embeds");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesReplyToMessageId_WhenMessageIsReply()
    {
        // Arrange
        var userId = 123456789UL;
        var replyToMessageId = 111222333UL;
        var mockMessage = CreateMockUserMessage(userId, replyToMessageId: replyToMessageId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.ReplyToMessageId.Should().Be(replyToMessageId, "message is a reply");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesNullReplyToMessageId_WhenMessageIsNotReply()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId, replyToMessageId: null);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.ReplyToMessageId.Should().BeNull("message is not a reply");
    }

    [Fact]
    public async Task HandleMessageAsync_CapturesTimestamp()
    {
        // Arrange
        var userId = 123456789UL;
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var mockMessage = CreateMockUserMessage(userId, timestamp: timestamp);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MessageLog? capturedLog = null;
        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog!.Timestamp.Should().Be(timestamp.UtcDateTime, "message timestamp should be captured");
        capturedLog.LoggedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5), "logged timestamp should be recent");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task HandleMessageAsync_CatchesException_WhenConsentServiceThrows()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId);
        var expectedException = new Exception("Database connection failed");

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockMessageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not attempt to log message when consent check fails");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to log message")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log error when exception occurs");
    }

    [Fact]
    public async Task HandleMessageAsync_CatchesException_WhenRepositoryThrows()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId);
        var expectedException = new Exception("Database write failed");

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to log message")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log error when repository throws exception");
    }

    [Fact]
    public async Task HandleMessageAsync_DoesNotThrowException_OnError()
    {
        // Arrange
        var userId = 123456789UL;
        var mockMessage = CreateMockUserMessage(userId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var act = async () => await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        await act.Should().NotThrowAsync("handler should catch all exceptions to prevent bot crashes");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task HandleMessageAsync_LogsDebugMessage_WhenProcessingMessage()
    {
        // Arrange
        var userId = 123456789UL;
        var messageId = 987654321UL;
        var channelId = 444555666UL;
        var mockMessage = CreateMockUserMessage(userId, messageId, channelId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when processing message");
    }

    [Fact]
    public async Task HandleMessageAsync_LogsSuccessMessage_AfterLogging()
    {
        // Arrange
        var userId = 123456789UL;
        var messageId = 987654321UL;
        var mockMessage = CreateMockUserMessage(userId, messageId);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(userId, ConsentType.MessageLogging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockMessageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<MessageLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageLog log, CancellationToken _) => log);

        // Act
        await _handler.HandleMessageAsync(mockMessage.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully logged message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message after successfully logging message");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock IDiscordMessage with the specified parameters for testing.
    /// </summary>
    private Mock<IDiscordMessage> CreateMockUserMessage(
        ulong userId,
        ulong messageId = 987654321UL,
        ulong channelId = 444555666UL,
        bool isDM = false,
        ulong? guildId = null,
        string? content = "Test message",
        bool hasAttachments = false,
        bool hasEmbeds = false,
        ulong? replyToMessageId = null,
        DateTimeOffset? timestamp = null)
    {
        var mockMessage = new Mock<IDiscordMessage>();

        // Setup basic message properties
        mockMessage.SetupGet(m => m.Id).Returns(messageId);
        mockMessage.SetupGet(m => m.AuthorId).Returns(userId);
        mockMessage.SetupGet(m => m.IsAuthorBot).Returns(false);
        mockMessage.SetupGet(m => m.IsUserMessage).Returns(true);
        mockMessage.SetupGet(m => m.ChannelId).Returns(channelId);
        mockMessage.SetupGet(m => m.IsDirectMessage).Returns(isDM);
        mockMessage.SetupGet(m => m.GuildId).Returns(isDM ? null : guildId);
        mockMessage.SetupGet(m => m.Content).Returns(content ?? string.Empty);
        mockMessage.SetupGet(m => m.Timestamp).Returns(timestamp ?? DateTimeOffset.UtcNow);
        mockMessage.SetupGet(m => m.HasAttachments).Returns(hasAttachments);
        mockMessage.SetupGet(m => m.HasEmbeds).Returns(hasEmbeds);
        mockMessage.SetupGet(m => m.ReplyToMessageId).Returns(replyToMessageId);

        return mockMessage;
    }

    #endregion
}
