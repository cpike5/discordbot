using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MessageLogService"/>.
/// </summary>
public class MessageLogServiceTests
{
    private readonly Mock<IMessageLogRepository> _repositoryMock;
    private readonly Mock<IOptions<MessageLogRetentionOptions>> _optionsMock;
    private readonly Mock<ILogger<MessageLogService>> _loggerMock;
    private readonly MessageLogService _sut;

    public MessageLogServiceTests()
    {
        _repositoryMock = new Mock<IMessageLogRepository>();
        _optionsMock = new Mock<IOptions<MessageLogRetentionOptions>>();
        _loggerMock = new Mock<ILogger<MessageLogService>>();

        _optionsMock.Setup(x => x.Value).Returns(new MessageLogRetentionOptions
        {
            Enabled = true,
            RetentionDays = 90,
            CleanupBatchSize = 1000,
            CleanupIntervalHours = 24
        });

        _sut = new MessageLogService(
            _repositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetLogsAsync_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            AuthorId = 123456789UL,
            GuildId = 987654321UL,
            Page = 1,
            PageSize = 25
        };

        var messages = new List<MessageLog>
        {
            CreateMessageLog(1, authorId: 123456789UL, guildId: 987654321UL),
            CreateMessageLog(2, authorId: 123456789UL, guildId: 987654321UL),
            CreateMessageLog(3, authorId: 123456789UL, guildId: 987654321UL)
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 3));

        // Act
        var result = await _sut.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
        result.TotalCount.Should().Be(3);
        result.Items.Should().AllSatisfy(item =>
        {
            item.AuthorId.Should().Be(123456789UL);
            item.GuildId.Should().Be(987654321UL);
        });
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesPaginationDefaults_PageLessThanOne()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            Page = 0, // Invalid
            PageSize = 25
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<MessageLog>(), 0));

        // Act
        var result = await _sut.GetLogsAsync(query);

        // Assert
        result.Page.Should().Be(1, "page should default to 1 when less than 1");
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesPaginationDefaults_PageSizeLessThanOne()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            Page = 1,
            PageSize = 0 // Invalid
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<MessageLog>(), 0));

        // Act
        var result = await _sut.GetLogsAsync(query);

        // Assert
        result.PageSize.Should().Be(25, "page size should default to 25 when less than 1");
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesPaginationDefaults_PageSizeGreaterThan100()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            Page = 1,
            PageSize = 150 // Too large
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<MessageLog>(), 0));

        // Act
        var result = await _sut.GetLogsAsync(query);

        // Assert
        result.PageSize.Should().Be(25, "page size should default to 25 when greater than 100");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsDto()
    {
        // Arrange
        const long messageId = 12345L;
        var message = CreateMessageLog(messageId, authorId: 111111111UL, guildId: 222222222UL);

        _repositoryMock.Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var result = await _sut.GetByIdAsync(messageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.DiscordMessageId.Should().Be(999999999UL);
        result.AuthorId.Should().Be(111111111UL);
        result.GuildId.Should().Be(222222222UL);
        result.Content.Should().Be("Test message content");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        const long messageId = 99999L;

        _repositoryMock.Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageLog?)null);

        // Act
        var result = await _sut.GetByIdAsync(messageId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsAggregatedStats()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(DateTime.UtcNow), 100),
            (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 75),
            (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), 50)
        };

        _repositoryMock.Setup(x => x.GetBasicStatsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1000L, 200L, 800L, 50L));

        _repositoryMock.Setup(x => x.GetMessagesByDayAsync(7, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        _repositoryMock.Setup(x => x.GetOldestMessageDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddMonths(-3));

        _repositoryMock.Setup(x => x.GetNewestMessageDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        // Act
        var result = await _sut.GetStatsAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result.TotalMessages.Should().Be(1000);
        result.DmMessages.Should().Be(200);
        result.ServerMessages.Should().Be(800);
        result.UniqueAuthors.Should().Be(50);
        result.MessagesByDay.Should().HaveCount(3);
        result.OldestMessage.Should().NotBeNull();
        result.NewestMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatsAsync_WithNullGuildId_ReturnsGlobalStats()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetBasicStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5000L, 1000L, 4000L, 250L));

        _repositoryMock.Setup(x => x.GetMessagesByDayAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        _repositoryMock.Setup(x => x.GetOldestMessageDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddYears(-1));

        _repositoryMock.Setup(x => x.GetNewestMessageDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        // Act
        var result = await _sut.GetStatsAsync(null);

        // Assert
        result.Should().NotBeNull();
        result.TotalMessages.Should().Be(5000);
        result.DmMessages.Should().Be(1000);
        result.ServerMessages.Should().Be(4000);
        result.UniqueAuthors.Should().Be(250);

        _repositoryMock.Verify(x => x.GetBasicStatsAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserMessagesAsync_CallsRepository()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const int expectedDeleteCount = 50;

        _repositoryMock.Setup(x => x.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDeleteCount);

        // Act
        var result = await _sut.DeleteUserMessagesAsync(userId);

        // Assert
        result.Should().Be(expectedDeleteCount);
        _repositoryMock.Verify(x => x.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserMessagesAsync_WithNoMessages_ReturnsZero()
    {
        // Arrange
        const ulong userId = 999999999UL;

        _repositoryMock.Setup(x => x.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.DeleteUserMessagesAsync(userId);

        // Assert
        result.Should().Be(0);
        _repositoryMock.Verify(x => x.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WhenDisabled_ReturnsZero()
    {
        // Arrange
        _optionsMock.Setup(x => x.Value).Returns(new MessageLogRetentionOptions
        {
            Enabled = false,
            RetentionDays = 90,
            CleanupBatchSize = 1000
        });

        var service = new MessageLogService(
            _repositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.CleanupOldMessagesAsync();

        // Assert
        result.Should().Be(0);
        _repositoryMock.Verify(
            x => x.DeleteBatchOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository should not be called when cleanup is disabled");
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_DeletesInBatches()
    {
        // Arrange
        var options = new MessageLogRetentionOptions
        {
            Enabled = true,
            RetentionDays = 90,
            CleanupBatchSize = 1000
        };

        _optionsMock.Setup(x => x.Value).Returns(options);

        // Simulate 3 batches: 1000, 1000, 500
        var callCount = 0;
        _repositoryMock.Setup(x => x.DeleteBatchOlderThanAsync(
                It.IsAny<DateTime>(),
                1000,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => 1000, // First batch: full
                    2 => 1000, // Second batch: full
                    3 => 500,  // Third batch: partial (stops here)
                    _ => 0
                };
            });

        var service = new MessageLogService(
            _repositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.CleanupOldMessagesAsync();

        // Assert
        result.Should().Be(2500, "total deleted should be sum of all batches");
        _repositoryMock.Verify(
            x => x.DeleteBatchOlderThanAsync(It.IsAny<DateTime>(), 1000, It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "should call repository 3 times for 3 batches");
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WithNothingToDelete_ReturnsZero()
    {
        // Arrange
        _repositoryMock.Setup(x => x.DeleteBatchOlderThanAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.CleanupOldMessagesAsync();

        // Assert
        result.Should().Be(0);
        _repositoryMock.Verify(
            x => x.DeleteBatchOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_UsesCutoffDateBasedOnRetentionDays()
    {
        // Arrange
        var options = new MessageLogRetentionOptions
        {
            Enabled = true,
            RetentionDays = 30,
            CleanupBatchSize = 1000
        };

        _optionsMock.Setup(x => x.Value).Returns(options);

        DateTime? capturedCutoffDate = null;
        _repositoryMock.Setup(x => x.DeleteBatchOlderThanAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((cutoff, batch, ct) => capturedCutoffDate = cutoff)
            .ReturnsAsync(0);

        var service = new MessageLogService(
            _repositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        // Act
        await service.CleanupOldMessagesAsync();

        // Assert
        capturedCutoffDate.Should().NotBeNull();
        var expectedCutoff = DateTime.UtcNow.AddDays(-30);
        capturedCutoffDate.Should().BeCloseTo(expectedCutoff, TimeSpan.FromSeconds(5), "cutoff should be 30 days ago");
    }

    [Fact]
    public async Task ExportToCsvAsync_GeneratesValidCsv()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            GuildId = 123456789UL,
            Page = 1,
            PageSize = 25
        };

        var messages = new List<MessageLog>
        {
            CreateMessageLog(1, authorId: 111UL, guildId: 123456789UL, content: "Hello world"),
            CreateMessageLog(2, authorId: 222UL, guildId: 123456789UL, content: "Test message"),
            CreateMessageLog(3, authorId: 333UL, guildId: null, content: "DM message", source: MessageSource.DirectMessage)
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 3));

        // Act
        var result = await _sut.ExportToCsvAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        csvContent.Should().Contain("Id,DiscordMessageId,AuthorId,ChannelId,GuildId,Source,Content,Timestamp,LoggedAt,HasAttachments,HasEmbeds,ReplyToMessageId");
        csvContent.Should().Contain("Hello world");
        csvContent.Should().Contain("Test message");
        csvContent.Should().Contain("DM message");
        csvContent.Should().Contain("DirectMessage");
        csvContent.Should().Contain("ServerChannel");
    }

    [Fact]
    public async Task ExportToCsvAsync_EscapesSpecialCharacters()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };

        var messages = new List<MessageLog>
        {
            CreateMessageLog(1, content: "Message with \"quotes\" and, commas"),
            CreateMessageLog(2, content: "Message with\nnewlines\r\nand returns")
        };

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 2));

        // Act
        var result = await _sut.ExportToCsvAsync(query);

        // Assert
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        csvContent.Should().Contain("\"Message with \"\"quotes\"\" and, commas\"");
        csvContent.Should().Contain("\"Message with\nnewlines\r\nand returns\"");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithLargeResultSet_LogsWarning()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var messages = Enumerable.Range(1, 100).Select(i => CreateMessageLog(i)).ToList();

        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 15000)); // Total count > 10000

        // Act
        var result = await _sut.ExportToCsvAsync(query);

        // Assert
        result.Should().NotBeNull();
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("10000") || v.ToString()!.Contains("10,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when total count exceeds page size");
    }

    [Fact]
    public async Task ExportToCsvAsync_SetsLargePageSize()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            GuildId = 123456789UL,
            Page = 1,
            PageSize = 25 // Original page size
        };

        MessageLogQueryDto? capturedQuery = null;
        _repositoryMock.Setup(x => x.GetPaginatedAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLogQueryDto, CancellationToken>((q, ct) => capturedQuery = q)
            .ReturnsAsync((new List<MessageLog>(), 0));

        // Act
        await _sut.ExportToCsvAsync(query);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.PageSize.Should().Be(10000, "export should use large page size");
        capturedQuery.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetLogsAsync_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _repositoryMock.Setup(x => x.GetPaginatedAsync(query, cancellationToken))
            .ReturnsAsync((new List<MessageLog>(), 0));

        // Act
        await _sut.GetLogsAsync(query, cancellationToken);

        // Assert
        _repositoryMock.Verify(x => x.GetPaginatedAsync(query, cancellationToken), Times.Once);
    }

    // Helper methods for creating test data

    private MessageLog CreateMessageLog(
        long id = 1,
        ulong authorId = 111111111UL,
        ulong? guildId = 222222222UL,
        string content = "Test message content",
        MessageSource source = MessageSource.ServerChannel)
    {
        var guild = guildId.HasValue ? new Guild
        {
            Id = guildId.Value,
            Name = $"Test Guild {guildId}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        } : null;

        var user = new User
        {
            Id = authorId,
            Username = $"TestUser{authorId}",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        return new MessageLog
        {
            Id = id,
            DiscordMessageId = 999999999UL,
            AuthorId = authorId,
            User = user,
            ChannelId = 888888888UL,
            GuildId = guildId,
            Guild = guild,
            Source = source,
            Content = content,
            Timestamp = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow,
            HasAttachments = false,
            HasEmbeds = false,
            ReplyToMessageId = null
        };
    }
}
