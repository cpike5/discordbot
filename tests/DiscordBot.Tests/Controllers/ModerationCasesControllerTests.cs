using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Middleware;
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
/// Unit tests for <see cref="ModerationCasesController"/>.
/// Tests cover all endpoints: GetCases, GetCaseById, GetCaseByNumber,
/// CreateCase, and UpdateCaseReason.
/// </summary>
[Trait("Category", "Unit")]
public class ModerationCasesControllerTests
{
    private readonly Mock<IModerationService> _mockModerationService;
    private readonly Mock<ILogger<ModerationCasesController>> _mockLogger;
    private readonly ModerationCasesController _controller;
    private const ulong TestGuildId = 111222333UL;

    public ModerationCasesControllerTests()
    {
        _mockModerationService = new Mock<IModerationService>();
        _mockLogger = new Mock<ILogger<ModerationCasesController>>();

        _controller = new ModerationCasesController(
            _mockModerationService.Object,
            _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = "test-correlation-id";
    }

    #region GetCases Tests

    [Fact]
    public async Task GetCases_ShouldReturnOkWithPaginatedResults_WhenCasesExist()
    {
        // Arrange
        var cases = new List<ModerationCaseDto>
        {
            CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: 1),
            CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: 2),
            CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: 3)
        };

        _mockModerationService
            .Setup(s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _controller.GetCases(TestGuildId, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<ModerationCaseDto>;

        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(3);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
        response.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetCases_ShouldReturnOkWithEmptyList_WhenNoCasesExist()
    {
        // Arrange
        _mockModerationService
            .Setup(s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ModerationCaseDto>(), 0));

        // Act
        var result = await _controller.GetCases(TestGuildId, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<ModerationCaseDto>;

        response.Should().NotBeNull();
        response!.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCases_ShouldReturnBadRequest_WhenPageNumberIsLessThanOne()
    {
        // Arrange
        const int invalidPage = 0;

        // Act
        var result = await _controller.GetCases(TestGuildId, page: invalidPage, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid page number");
        apiError.Detail.Should().Be("Page number must be greater than or equal to 1.");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockModerationService.Verify(
            s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when page number is invalid");
    }

    [Fact]
    public async Task GetCases_ShouldReturnBadRequest_WhenPageNumberIsNegative()
    {
        // Arrange
        const int invalidPage = -5;

        // Act
        var result = await _controller.GetCases(TestGuildId, page: invalidPage, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid page number");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetCases_ShouldReturnBadRequest_WhenPageSizeIsLessThanOne()
    {
        // Arrange
        const int invalidPageSize = 0;

        // Act
        var result = await _controller.GetCases(TestGuildId, pageSize: invalidPageSize, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid page size");
        apiError.Detail.Should().Be("Page size must be between 1 and 100.");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockModerationService.Verify(
            s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when page size is invalid");
    }

    [Fact]
    public async Task GetCases_ShouldReturnBadRequest_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        const int invalidPageSize = 101;

        // Act
        var result = await _controller.GetCases(TestGuildId, pageSize: invalidPageSize, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid page size");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetCases_ShouldIncludeCorrelationId_InBadRequestErrorResponse()
    {
        // Arrange
        const string expectedCorrelationId = "cases-correlation-id";
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = expectedCorrelationId;

        // Act
        var result = await _controller.GetCases(TestGuildId, page: 0, cancellationToken: CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError!.TraceId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task GetCases_ShouldPassFilters_ToService()
    {
        // Arrange
        var type = CaseType.Ban;
        const ulong targetUserId = 555666777UL;
        const ulong moderatorUserId = 888999000UL;
        const int page = 2;
        const int pageSize = 10;

        _mockModerationService
            .Setup(s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ModerationCaseDto>(), 0));

        // Act
        await _controller.GetCases(
            TestGuildId,
            type: type,
            targetUserId: targetUserId,
            moderatorUserId: moderatorUserId,
            page: page,
            pageSize: pageSize,
            cancellationToken: CancellationToken.None);

        // Assert
        _mockModerationService.Verify(
            s => s.GetCasesAsync(
                It.Is<ModerationCaseQueryDto>(q =>
                    q.GuildId == TestGuildId &&
                    q.Type == type &&
                    q.TargetUserId == targetUserId &&
                    q.ModeratorUserId == moderatorUserId &&
                    q.Page == page &&
                    q.PageSize == pageSize),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCases_ShouldAllowMaximumPageSize()
    {
        // Arrange
        const int maxPageSize = 100;

        _mockModerationService
            .Setup(s => s.GetCasesAsync(It.IsAny<ModerationCaseQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ModerationCaseDto>(), 0));

        // Act
        var result = await _controller.GetCases(TestGuildId, pageSize: maxPageSize, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetCaseById Tests

    [Fact]
    public async Task GetCaseById_ShouldReturnOkWithCase_WhenCaseExists()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var moderationCase = CreateTestModerationCaseDto(id: caseId, guildId: TestGuildId, caseNumber: 5);

        _mockModerationService
            .Setup(s => s.GetCaseAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderationCase);

        // Act
        var result = await _controller.GetCaseById(TestGuildId, caseId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var returnedCase = okResult!.Value as ModerationCaseDto;

        returnedCase.Should().NotBeNull();
        returnedCase!.Id.Should().Be(caseId);
        returnedCase.GuildId.Should().Be(TestGuildId);
        returnedCase.CaseNumber.Should().Be(5);
    }

    [Fact]
    public async Task GetCaseById_ShouldReturnNotFound_WhenCaseDoesNotExist()
    {
        // Arrange
        var caseId = Guid.NewGuid();

        _mockModerationService
            .Setup(s => s.GetCaseAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCaseDto?)null);

        // Act
        var result = await _controller.GetCaseById(TestGuildId, caseId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Moderation case not found");
        apiError.Detail.Should().Contain(caseId.ToString());
        apiError.Detail.Should().Contain(TestGuildId.ToString());
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetCaseById_ShouldReturnNotFound_WhenCaseBelongsToDifferentGuild()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        const ulong differentGuildId = 999888777UL;

        // Case exists but belongs to a different guild
        var moderationCase = CreateTestModerationCaseDto(id: caseId, guildId: differentGuildId, caseNumber: 1);

        _mockModerationService
            .Setup(s => s.GetCaseAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderationCase);

        // Act — request case for TestGuildId but case belongs to differentGuildId
        var result = await _controller.GetCaseById(TestGuildId, caseId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Moderation case not found");
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetCaseById_ShouldIncludeCorrelationId_InNotFoundErrorResponse()
    {
        // Arrange
        const string expectedCorrelationId = "case-not-found-correlation";
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = expectedCorrelationId;

        var caseId = Guid.NewGuid();

        _mockModerationService
            .Setup(s => s.GetCaseAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCaseDto?)null);

        // Act
        var result = await _controller.GetCaseById(TestGuildId, caseId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError!.TraceId.Should().Be(expectedCorrelationId);
    }

    #endregion

    #region GetCaseByNumber Tests

    [Fact]
    public async Task GetCaseByNumber_ShouldReturnOkWithCase_WhenCaseExists()
    {
        // Arrange
        const long caseNumber = 42L;
        var moderationCase = CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: (int)caseNumber);

        _mockModerationService
            .Setup(s => s.GetCaseByNumberAsync(TestGuildId, caseNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderationCase);

        // Act
        var result = await _controller.GetCaseByNumber(TestGuildId, caseNumber, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var returnedCase = okResult!.Value as ModerationCaseDto;

        returnedCase.Should().NotBeNull();
        returnedCase!.CaseNumber.Should().Be((int)caseNumber);
        returnedCase.GuildId.Should().Be(TestGuildId);
    }

    [Fact]
    public async Task GetCaseByNumber_ShouldReturnNotFound_WhenCaseDoesNotExist()
    {
        // Arrange
        const long caseNumber = 99999L;

        _mockModerationService
            .Setup(s => s.GetCaseByNumberAsync(TestGuildId, caseNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCaseDto?)null);

        // Act
        var result = await _controller.GetCaseByNumber(TestGuildId, caseNumber, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Moderation case not found");
        apiError.Detail.Should().Contain(caseNumber.ToString());
        apiError.Detail.Should().Contain(TestGuildId.ToString());
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region CreateCase Tests

    [Fact]
    public async Task CreateCase_ShouldReturnCreatedWithCase_WhenRequestIsValid()
    {
        // Arrange
        var request = new ModerationCaseCreateDto
        {
            TargetUserId = 555666777UL,
            ModeratorUserId = 888999000UL,
            Type = CaseType.Warn,
            Reason = "Spamming in #general"
        };

        var createdCase = CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: 10);

        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);

        // Act
        var result = await _controller.CreateCase(TestGuildId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        var createdResult = result.Result as CreatedAtActionResult;
        var returnedCase = createdResult!.Value as ModerationCaseDto;

        returnedCase.Should().NotBeNull();
        returnedCase!.GuildId.Should().Be(TestGuildId);
        returnedCase.CaseNumber.Should().Be(10);
    }

    [Fact]
    public async Task CreateCase_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.CreateCase(TestGuildId, null!, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid request");
        apiError.Detail.Should().Contain("null");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when request is null");
    }

    [Fact]
    public async Task CreateCase_ShouldReturnBadRequest_WhenServiceThrowsArgumentException()
    {
        // Arrange
        var request = new ModerationCaseCreateDto
        {
            TargetUserId = 0UL, // invalid: zero user ID
            ModeratorUserId = 888999000UL,
            Type = CaseType.Ban
        };

        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Target user ID must be a valid Discord snowflake."));

        // Act
        var result = await _controller.CreateCase(TestGuildId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid request");
        apiError.Detail.Should().Contain("Target user ID must be a valid Discord snowflake.");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateCase_ShouldOverrideRequestGuildId_WithRouteGuildId()
    {
        // Arrange — request has a different GuildId, should be overridden by route
        const ulong differentGuildId = 999888777UL;
        var request = new ModerationCaseCreateDto
        {
            GuildId = differentGuildId,
            TargetUserId = 555666777UL,
            ModeratorUserId = 888999000UL,
            Type = CaseType.Kick,
            Reason = "Disruptive behavior"
        };

        var createdCase = CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: 1);

        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);

        // Act
        await _controller.CreateCase(TestGuildId, request, CancellationToken.None);

        // Assert — GuildId should be overridden by the route value
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(
                It.Is<ModerationCaseCreateDto>(dto => dto.GuildId == TestGuildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateCase_ShouldReturnCreatedAtAction_WithCorrectLocation()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var request = new ModerationCaseCreateDto
        {
            TargetUserId = 555666777UL,
            ModeratorUserId = 888999000UL,
            Type = CaseType.Warn,
            Reason = "Test reason"
        };

        var createdCase = CreateTestModerationCaseDto(id: caseId, guildId: TestGuildId, caseNumber: 15);

        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);

        // Act
        var result = await _controller.CreateCase(TestGuildId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.ActionName.Should().Be(nameof(ModerationCasesController.GetCaseById));

        var routeValues = createdResult.RouteValues;
        routeValues.Should().NotBeNull();
        routeValues!["guildId"].Should().Be(TestGuildId);
        routeValues["caseId"].Should().Be(caseId);
    }

    #endregion

    #region UpdateCaseReason Tests

    [Fact]
    public async Task UpdateCaseReason_ShouldReturnOkWithUpdatedCase_WhenCaseExists()
    {
        // Arrange
        const long caseNumber = 5L;
        var request = new CaseReasonUpdateDto
        {
            Reason = "Updated reason: repeat offender",
            ModeratorId = 888999000UL
        };

        var updatedCase = CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: (int)caseNumber);

        _mockModerationService
            .Setup(s => s.UpdateCaseReasonAsync(
                TestGuildId, caseNumber, request.Reason, request.ModeratorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCase);

        // Act
        var result = await _controller.UpdateCaseReason(TestGuildId, caseNumber, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var returnedCase = okResult!.Value as ModerationCaseDto;

        returnedCase.Should().NotBeNull();
        returnedCase!.CaseNumber.Should().Be((int)caseNumber);
    }

    [Fact]
    public async Task UpdateCaseReason_ShouldReturnNotFound_WhenCaseDoesNotExist()
    {
        // Arrange
        const long caseNumber = 99999L;
        var request = new CaseReasonUpdateDto
        {
            Reason = "Updated reason",
            ModeratorId = 888999000UL
        };

        _mockModerationService
            .Setup(s => s.UpdateCaseReasonAsync(
                TestGuildId, caseNumber, request.Reason, request.ModeratorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCaseDto?)null);

        // Act
        var result = await _controller.UpdateCaseReason(TestGuildId, caseNumber, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Moderation case not found");
        apiError.Detail.Should().Contain(caseNumber.ToString());
        apiError.Detail.Should().Contain(TestGuildId.ToString());
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateCaseReason_ShouldReturnBadRequest_WhenReasonIsEmpty()
    {
        // Arrange
        const long caseNumber = 5L;
        var request = new CaseReasonUpdateDto
        {
            Reason = string.Empty, // invalid: empty reason
            ModeratorId = 888999000UL
        };

        // Act
        var result = await _controller.UpdateCaseReason(TestGuildId, caseNumber, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid request");
        apiError.Detail.Should().Contain("Reason is required");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockModerationService.Verify(
            s => s.UpdateCaseReasonAsync(
                It.IsAny<ulong>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when reason is empty");
    }

    [Fact]
    public async Task UpdateCaseReason_ShouldReturnBadRequest_WhenReasonIsWhitespace()
    {
        // Arrange
        const long caseNumber = 5L;
        var request = new CaseReasonUpdateDto
        {
            Reason = "   ", // invalid: whitespace only
            ModeratorId = 888999000UL
        };

        // Act
        var result = await _controller.UpdateCaseReason(TestGuildId, caseNumber, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid request");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateCaseReason_ShouldPassAllParameters_ToService()
    {
        // Arrange
        const long caseNumber = 7L;
        const ulong moderatorId = 777888999UL;
        const string reason = "Banned for repeated violations";

        var request = new CaseReasonUpdateDto
        {
            Reason = reason,
            ModeratorId = moderatorId
        };

        var updatedCase = CreateTestModerationCaseDto(guildId: TestGuildId, caseNumber: (int)caseNumber);

        _mockModerationService
            .Setup(s => s.UpdateCaseReasonAsync(
                TestGuildId, caseNumber, reason, moderatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCase);

        // Act
        await _controller.UpdateCaseReason(TestGuildId, caseNumber, request, CancellationToken.None);

        // Assert
        _mockModerationService.Verify(
            s => s.UpdateCaseReasonAsync(TestGuildId, caseNumber, reason, moderatorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test moderation case DTO with default values.
    /// </summary>
    private static ModerationCaseDto CreateTestModerationCaseDto(
        Guid? id = null,
        ulong guildId = TestGuildId,
        int caseNumber = 1,
        CaseType type = CaseType.Warn,
        ulong targetUserId = 555666777UL,
        ulong moderatorUserId = 888999000UL)
    {
        return new ModerationCaseDto
        {
            Id = id ?? Guid.NewGuid(),
            CaseNumber = caseNumber,
            GuildId = guildId,
            TargetUserId = targetUserId,
            TargetUsername = "TargetUser",
            ModeratorUserId = moderatorUserId,
            ModeratorUsername = "ModeratorUser",
            Type = type,
            Reason = "Test moderation reason",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
