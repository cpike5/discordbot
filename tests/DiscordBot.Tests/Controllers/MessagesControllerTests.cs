using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="MessagesController"/>.
/// </summary>
public class MessagesControllerTests
{
    private readonly Mock<IMessageLogService> _messageLogServiceMock;
    private readonly Mock<ILogger<MessagesController>> _loggerMock;
    private readonly MessagesController _controller;

    public MessagesControllerTests()
    {
        _messageLogServiceMock = new Mock<IMessageLogService>();
        _loggerMock = new Mock<ILogger<MessagesController>>();
        _controller = new MessagesController(_messageLogServiceMock.Object, _loggerMock.Object);

        // Setup HttpContext for correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items["CorrelationId"] = "test-correlation-id";
    }

    [Fact]
    public async Task GetMessages_ReturnsOkWithPaginatedResults()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            GuildId = 123456789UL,
            Page = 1,
            PageSize = 25
        };

        var paginatedResponse = new PaginatedResponseDto<MessageLogDto>
        {
            Items = new List<MessageLogDto>
            {
                new MessageLogDto
                {
                    Id = 1,
                    DiscordMessageId = 111UL,
                    AuthorId = 222UL,
                    Content = "Test message 1",
                    Timestamp = DateTime.UtcNow
                },
                new MessageLogDto
                {
                    Id = 2,
                    DiscordMessageId = 333UL,
                    AuthorId = 444UL,
                    Content = "Test message 2",
                    Timestamp = DateTime.UtcNow
                }
            }.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _messageLogServiceMock.Setup(x => x.GetLogsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMessages(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<PaginatedResponseDto<MessageLogDto>>();

        var response = okResult.Value as PaginatedResponseDto<MessageLogDto>;
        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(2);
        response.Page.Should().Be(1);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMessages_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1), // End before start
            Page = 1,
            PageSize = 25
        };

        // Act
        var result = await _controller.GetMessages(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid date range");
        error.Detail.Should().Contain("Start date cannot be after end date");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _messageLogServiceMock.Verify(
            x => x.GetLogsAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when date range is invalid");
    }

    [Fact]
    public async Task GetMessages_WithValidDateRange_CallsService()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            Page = 1,
            PageSize = 25
        };

        var paginatedResponse = new PaginatedResponseDto<MessageLogDto>
        {
            Items = new List<MessageLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _messageLogServiceMock.Setup(x => x.GetLogsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMessages(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _messageLogServiceMock.Verify(x => x.GetLogsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessageById_WhenExists_ReturnsOk()
    {
        // Arrange
        const long messageId = 12345L;
        var messageDto = new MessageLogDto
        {
            Id = messageId,
            DiscordMessageId = 999999999UL,
            AuthorId = 123456789UL,
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow
        };

        _messageLogServiceMock.Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageDto);

        // Act
        var result = await _controller.GetMessageById(messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<MessageLogDto>();

        var message = okResult.Value as MessageLogDto;
        message.Should().NotBeNull();
        message!.Id.Should().Be(messageId);
        message.Content.Should().Be("Test message");
    }

    [Fact]
    public async Task GetMessageById_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        const long messageId = 99999L;

        _messageLogServiceMock.Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageLogDto?)null);

        // Act
        var result = await _controller.GetMessageById(messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Message not found");
        error.Detail.Should().Contain(messageId.ToString());
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetStats_ReturnsOkWithStats()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var stats = new MessageLogStatsDto
        {
            TotalMessages = 1000,
            DmMessages = 200,
            ServerMessages = 800,
            UniqueAuthors = 50,
            MessagesByDay = new List<DailyMessageCount>
            {
                new DailyMessageCount(DateOnly.FromDateTime(DateTime.UtcNow), 100),
                new DailyMessageCount(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 75)
            },
            OldestMessage = DateTime.UtcNow.AddMonths(-3),
            NewestMessage = DateTime.UtcNow
        };

        _messageLogServiceMock.Setup(x => x.GetStatsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _controller.GetStats(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<MessageLogStatsDto>();

        var statsResult = okResult.Value as MessageLogStatsDto;
        statsResult.Should().NotBeNull();
        statsResult!.TotalMessages.Should().Be(1000);
        statsResult.DmMessages.Should().Be(200);
        statsResult.ServerMessages.Should().Be(800);
        statsResult.UniqueAuthors.Should().Be(50);
        statsResult.MessagesByDay.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStats_WithNullGuildId_ReturnsGlobalStats()
    {
        // Arrange
        var stats = new MessageLogStatsDto
        {
            TotalMessages = 5000,
            DmMessages = 1000,
            ServerMessages = 4000,
            UniqueAuthors = 250
        };

        _messageLogServiceMock.Setup(x => x.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _controller.GetStats(null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var statsResult = okResult!.Value as MessageLogStatsDto;
        statsResult!.TotalMessages.Should().Be(5000);

        _messageLogServiceMock.Verify(x => x.GetStatsAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserMessages_CallsServiceAndReturnsCount()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const int deletedCount = 50;

        _messageLogServiceMock.Setup(x => x.DeleteUserMessagesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        var result = await _controller.DeleteUserMessages(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        var value = okResult!.Value;
        value.Should().NotBeNull();

        var deletedCountProperty = value!.GetType().GetProperty("deletedCount");
        deletedCountProperty.Should().NotBeNull();
        deletedCountProperty!.GetValue(value).Should().Be(deletedCount);

        _messageLogServiceMock.Verify(x => x.DeleteUserMessagesAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserMessages_WithNoMessages_ReturnsZero()
    {
        // Arrange
        const ulong userId = 999999999UL;

        _messageLogServiceMock.Setup(x => x.DeleteUserMessagesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.DeleteUserMessages(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var value = okResult!.Value;
        var deletedCountProperty = value!.GetType().GetProperty("deletedCount");
        deletedCountProperty!.GetValue(value).Should().Be(0);
    }

    [Fact]
    public async Task CleanupOldMessages_CallsServiceAndReturnsCount()
    {
        // Arrange
        const int deletedCount = 150;

        _messageLogServiceMock.Setup(x => x.CleanupOldMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        var result = await _controller.CleanupOldMessages(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        var value = okResult!.Value;
        value.Should().NotBeNull();

        var deletedCountProperty = value!.GetType().GetProperty("deletedCount");
        deletedCountProperty.Should().NotBeNull();
        deletedCountProperty!.GetValue(value).Should().Be(deletedCount);

        _messageLogServiceMock.Verify(x => x.CleanupOldMessagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupOldMessages_WithNothingToCleanup_ReturnsZero()
    {
        // Arrange
        _messageLogServiceMock.Setup(x => x.CleanupOldMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CleanupOldMessages(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var value = okResult!.Value;
        var deletedCountProperty = value!.GetType().GetProperty("deletedCount");
        deletedCountProperty!.GetValue(value).Should().Be(0);
    }

    [Fact]
    public async Task ExportMessages_ReturnsCsvFile()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            GuildId = 123456789UL,
            Page = 1,
            PageSize = 25
        };

        var csvData = System.Text.Encoding.UTF8.GetBytes("Id,DiscordMessageId,AuthorId,Content\n1,111,222,Test");

        _messageLogServiceMock.Setup(x => x.ExportToCsvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _controller.ExportMessages(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FileContentResult>();

        var fileResult = result as FileContentResult;
        fileResult.Should().NotBeNull();
        fileResult!.FileContents.Should().BeEquivalentTo(csvData);
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().StartWith("message-logs-");
        fileResult.FileDownloadName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task ExportMessages_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var query = new MessageLogQueryDto
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1), // End before start
            Page = 1,
            PageSize = 25
        };

        // Act
        var result = await _controller.ExportMessages(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid date range");
        error.Detail.Should().Contain("Start date cannot be after end date");

        _messageLogServiceMock.Verify(
            x => x.ExportToCsvAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when date range is invalid");
    }

    [Fact]
    public async Task ExportMessages_GeneratesTimestampedFilename()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var csvData = System.Text.Encoding.UTF8.GetBytes("test data");

        _messageLogServiceMock.Setup(x => x.ExportToCsvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _controller.ExportMessages(query, CancellationToken.None);

        // Assert
        var fileResult = result as FileContentResult;
        fileResult.Should().NotBeNull();

        // Filename format: message-logs-yyyyMMddHHmmss.csv
        fileResult!.FileDownloadName.Should().MatchRegex(@"^message-logs-\d{14}\.csv$");
    }

    [Fact]
    public async Task GetMessages_LogsDebugMessage()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var paginatedResponse = new PaginatedResponseDto<MessageLogDto>
        {
            Items = new List<MessageLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _messageLogServiceMock.Setup(x => x.GetLogsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _controller.GetMessages(query, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message logs requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when messages are requested");
    }

    [Fact]
    public async Task DeleteUserMessages_LogsInformationMessage()
    {
        // Arrange
        const ulong userId = 123456789UL;
        _messageLogServiceMock.Setup(x => x.DeleteUserMessagesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        // Act
        await _controller.DeleteUserMessages(userId, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GDPR deletion requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information message when GDPR deletion is requested");
    }

    [Fact]
    public async Task CleanupOldMessages_LogsInformationMessages()
    {
        // Arrange
        _messageLogServiceMock.Setup(x => x.CleanupOldMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        await _controller.CleanupOldMessages(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Manual message cleanup triggered") || v.ToString()!.Contains("Cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log information messages when cleanup is triggered and completed");
    }

    [Fact]
    public async Task ExportMessages_LogsInformationMessages()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var csvData = System.Text.Encoding.UTF8.GetBytes("test");

        _messageLogServiceMock.Setup(x => x.ExportToCsvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        await _controller.ExportMessages(query, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message export")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log information messages when export is requested and completed");
    }

    [Fact]
    public async Task GetMessages_WithCancellationToken_PassesToService()
    {
        // Arrange
        var query = new MessageLogQueryDto { Page = 1, PageSize = 25 };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var paginatedResponse = new PaginatedResponseDto<MessageLogDto>
        {
            Items = new List<MessageLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _messageLogServiceMock.Setup(x => x.GetLogsAsync(query, cancellationToken))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _controller.GetMessages(query, cancellationToken);

        // Assert
        _messageLogServiceMock.Verify(x => x.GetLogsAsync(query, cancellationToken), Times.Once);
    }
}
