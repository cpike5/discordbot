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
/// Unit tests for ScheduledMessagesController.
/// Tests cover all CRUD endpoints, validation, error handling, and API responses.
/// </summary>
public class ScheduledMessagesControllerTests
{
    private readonly Mock<IScheduledMessageService> _mockService;
    private readonly Mock<ILogger<ScheduledMessagesController>> _mockLogger;
    private readonly ScheduledMessagesController _controller;
    private const ulong TestGuildId = 123456789UL;

    public ScheduledMessagesControllerTests()
    {
        _mockService = new Mock<IScheduledMessageService>();
        _mockLogger = new Mock<ILogger<ScheduledMessagesController>>();
        _controller = new ScheduledMessagesController(_mockService.Object, _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Helper Methods

    private static ScheduledMessageDto CreateTestDto(
        Guid? id = null,
        ulong guildId = TestGuildId,
        string title = "Test Message")
    {
        return new ScheduledMessageDto
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            GuildName = "Test Guild",
            ChannelId = 987654321UL,
            Title = title,
            Content = "Test Content",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user",
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region GetScheduledMessages Tests

    [Fact]
    public async Task GetScheduledMessages_WithValidParameters_ReturnsOkWithPaginatedResults()
    {
        // Arrange
        var messages = new List<ScheduledMessageDto>
        {
            CreateTestDto(title: "Message 1"),
            CreateTestDto(title: "Message 2")
        };

        _mockService
            .Setup(s => s.GetByGuildIdAsync(TestGuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, 2));

        // Act
        var result = await _controller.GetScheduledMessages(TestGuildId, 1, 20);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<PaginatedResponseDto<ScheduledMessageDto>>();

        var response = okResult.Value as PaginatedResponseDto<ScheduledMessageDto>;
        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetScheduledMessages_WithInvalidPage_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetScheduledMessages(TestGuildId, 0, 20);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid page number");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockService.Verify(
            s => s.GetByGuildIdAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetScheduledMessages_WithInvalidPageSize_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetScheduledMessages(TestGuildId, 1, 0);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid page size");
        error.Detail.Should().Contain("between 1 and 100");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    public async Task GetScheduledMessages_WithPageSizeAboveLimit_ReturnsBadRequest(int pageSize)
    {
        // Act
        var result = await _controller.GetScheduledMessages(TestGuildId, 1, pageSize);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Message.Should().Be("Invalid page size");
    }

    [Fact]
    public async Task GetScheduledMessages_WithEmptyResults_ReturnsOkWithEmptyList()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetByGuildIdAsync(TestGuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ScheduledMessageDto>(), 0));

        // Act
        var result = await _controller.GetScheduledMessages(TestGuildId, 1, 20);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<ScheduledMessageDto>;
        response!.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    #endregion

    #region GetScheduledMessageById Tests

    [Fact]
    public async Task GetScheduledMessageById_WithExistingMessage_ReturnsOk()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var dto = CreateTestDto(id: messageId);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetScheduledMessageById(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<ScheduledMessageDto>();

        var returnedDto = okResult.Value as ScheduledMessageDto;
        returnedDto.Should().NotBeNull();
        returnedDto!.Id.Should().Be(messageId);
        returnedDto.Title.Should().Be("Test Message");
    }

    [Fact]
    public async Task GetScheduledMessageById_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessageDto?)null);

        // Act
        var result = await _controller.GetScheduledMessageById(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Scheduled message not found");
        error.Detail.Should().Contain(messageId.ToString());
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetScheduledMessageById_WithWrongGuild_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var dto = CreateTestDto(id: messageId, guildId: 999999999UL); // Different guild

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetScheduledMessageById(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateScheduledMessage Tests

    [Fact]
    public async Task CreateScheduledMessage_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = TestGuildId,
            ChannelId = 987654321UL,
            Title = "New Message",
            Content = "New Content",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = DateTime.UtcNow.AddDays(1),
            CreatedBy = "user123"
        };

        var createdDto = CreateTestDto(title: "New Message");

        _mockService
            .Setup(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        // Act
        var result = await _controller.CreateScheduledMessage(TestGuildId, createDto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.ActionName.Should().Be(nameof(ScheduledMessagesController.GetScheduledMessageById));
        createdResult.Value.Should().BeOfType<ScheduledMessageDto>();

        var returnedDto = createdResult.Value as ScheduledMessageDto;
        returnedDto.Should().NotBeNull();
        returnedDto!.Title.Should().Be("New Message");

        _mockService.Verify(
            s => s.CreateAsync(It.Is<ScheduledMessageCreateDto>(d => d.GuildId == TestGuildId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateScheduledMessage_WithNullRequest_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.CreateScheduledMessage(TestGuildId, null!, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("cannot be null");

        _mockService.Verify(
            s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateScheduledMessage_WithServiceArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = TestGuildId,
            ChannelId = 987654321UL,
            Title = "Invalid",
            Content = "Content",
            Frequency = ScheduleFrequency.Custom,
            CronExpression = "invalid",
            IsEnabled = true,
            CreatedBy = "user123"
        };

        _mockService
            .Setup(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid cron expression format"));

        // Act
        var result = await _controller.CreateScheduledMessage(TestGuildId, createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Detail.Should().Contain("Invalid cron expression format");
    }

    [Fact]
    public async Task CreateScheduledMessage_OverridesGuildIdFromRoute()
    {
        // Arrange
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = 999999999UL, // Wrong guild ID in request
            ChannelId = 987654321UL,
            Title = "Message",
            Content = "Content",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            CreatedBy = "user123"
        };

        var createdDto = CreateTestDto();

        _mockService
            .Setup(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        // Act
        await _controller.CreateScheduledMessage(TestGuildId, createDto, CancellationToken.None);

        // Assert
        _mockService.Verify(
            s => s.CreateAsync(
                It.Is<ScheduledMessageCreateDto>(d => d.GuildId == TestGuildId),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "controller should override GuildId with route parameter");
    }

    #endregion

    #region UpdateScheduledMessage Tests

    [Fact]
    public async Task UpdateScheduledMessage_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);
        var updateDto = new ScheduledMessageUpdateDto
        {
            Title = "Updated Title",
            Content = "Updated Content"
        };

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        var updatedDto = CreateTestDto(id: messageId);
        updatedDto.Title = "Updated Title";
        updatedDto.Content = "Updated Content";

        _mockService
            .Setup(s => s.UpdateAsync(messageId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDto);

        // Act
        var result = await _controller.UpdateScheduledMessage(TestGuildId, messageId, updateDto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<ScheduledMessageDto>();

        var returnedDto = okResult.Value as ScheduledMessageDto;
        returnedDto.Should().NotBeNull();
        returnedDto!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateScheduledMessage_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act
        var result = await _controller.UpdateScheduledMessage(TestGuildId, messageId, null!, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Message.Should().Be("Invalid request");

        _mockService.Verify(
            s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateScheduledMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var updateDto = new ScheduledMessageUpdateDto { Title = "Updated" };

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessageDto?)null);

        // Act
        var result = await _controller.UpdateScheduledMessage(TestGuildId, messageId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        _mockService.Verify(
            s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "update should not be called if message doesn't exist");
    }

    [Fact]
    public async Task UpdateScheduledMessage_WithWrongGuild_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId, guildId: 999999999UL); // Different guild
        var updateDto = new ScheduledMessageUpdateDto { Title = "Updated" };

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        // Act
        var result = await _controller.UpdateScheduledMessage(TestGuildId, messageId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateScheduledMessage_WithServiceArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);
        var updateDto = new ScheduledMessageUpdateDto
        {
            Frequency = ScheduleFrequency.Custom,
            CronExpression = "invalid"
        };

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        _mockService
            .Setup(s => s.UpdateAsync(messageId, updateDto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid cron expression"));

        // Act
        var result = await _controller.UpdateScheduledMessage(TestGuildId, messageId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteScheduledMessage Tests

    [Fact]
    public async Task DeleteScheduledMessage_WithExistingMessage_ReturnsNoContent()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        _mockService
            .Setup(s => s.DeleteAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();

        _mockService.Verify(
            s => s.DeleteAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteScheduledMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessageDto?)null);

        // Act
        var result = await _controller.DeleteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();

        _mockService.Verify(
            s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "delete should not be called if message doesn't exist");
    }

    [Fact]
    public async Task DeleteScheduledMessage_WithWrongGuild_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId, guildId: 999999999UL);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        // Act
        var result = await _controller.DeleteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteScheduledMessage_WhenDeleteFails_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        _mockService
            .Setup(s => s.DeleteAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ExecuteScheduledMessage Tests

    [Fact]
    public async Task ExecuteScheduledMessage_WithValidMessage_ReturnsOk()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        _mockService
            .Setup(s => s.ExecuteScheduledMessageAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ExecuteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        _mockService.Verify(
            s => s.ExecuteScheduledMessageAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteScheduledMessage_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledMessageDto?)null);

        // Act
        var result = await _controller.ExecuteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();

        _mockService.Verify(
            s => s.ExecuteScheduledMessageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteScheduledMessage_WithWrongGuild_ReturnsNotFound()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId, guildId: 999999999UL);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        // Act
        var result = await _controller.ExecuteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ExecuteScheduledMessage_WhenExecutionFails_ReturnsInternalServerError()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var existingDto = CreateTestDto(id: messageId);

        _mockService
            .Setup(s => s.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        _mockService
            .Setup(s => s.ExecuteScheduledMessageAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ExecuteScheduledMessage(TestGuildId, messageId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().BeOfType<ApiErrorDto>();

        var error = objectResult.Value as ApiErrorDto;
        error!.Message.Should().Be("Execution failed");
    }

    #endregion

    #region ValidateCronExpression Tests

    [Fact]
    public async Task ValidateCronExpression_WithValidExpression_ReturnsOk()
    {
        // Arrange
        var request = new CronValidationRequestDto
        {
            CronExpression = "0 0 9 * * *"
        };

        _mockService
            .Setup(s => s.ValidateCronExpressionAsync(request.CronExpression))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.ValidateCronExpression(TestGuildId, request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        _mockService.Verify(
            s => s.ValidateCronExpressionAsync(request.CronExpression),
            Times.Once);
    }

    [Fact]
    public async Task ValidateCronExpression_WithInvalidExpression_ReturnsBadRequest()
    {
        // Arrange
        var request = new CronValidationRequestDto
        {
            CronExpression = "invalid cron"
        };

        _mockService
            .Setup(s => s.ValidateCronExpressionAsync(request.CronExpression))
            .ReturnsAsync((false, "Invalid cron expression format"));

        // Act
        var result = await _controller.ValidateCronExpression(TestGuildId, request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid cron expression");
        error.Detail.Should().Contain("Invalid cron expression format");
    }

    [Fact]
    public async Task ValidateCronExpression_WithNullRequest_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ValidateCronExpression(TestGuildId, null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Cron expression is required");

        _mockService.Verify(
            s => s.ValidateCronExpressionAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateCronExpression_WithEmptyExpression_ReturnsBadRequest()
    {
        // Arrange
        var request = new CronValidationRequestDto
        {
            CronExpression = ""
        };

        // Act
        var result = await _controller.ValidateCronExpression(TestGuildId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Detail.Should().Contain("Cron expression is required");
    }

    #endregion
}
