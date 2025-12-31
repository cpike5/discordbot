using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Controllers;

/// <summary>
/// Unit tests for <see cref="GuildMembersController"/>.
/// </summary>
public class GuildMembersControllerTests
{
    private readonly Mock<IGuildMemberService> _mockGuildMemberService;
    private readonly Mock<ILogger<GuildMembersController>> _mockLogger;
    private readonly GuildMembersController _controller;

    private const ulong TestGuildId = 123456789UL;
    private const ulong TestUserId = 987654321UL;
    private const string TestCorrelationId = "test-correlation-id";

    public GuildMembersControllerTests()
    {
        _mockGuildMemberService = new Mock<IGuildMemberService>();
        _mockLogger = new Mock<ILogger<GuildMembersController>>();
        _controller = new GuildMembersController(_mockGuildMemberService.Object, _mockLogger.Object);

        // Setup HttpContext with correlation ID in Items
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = TestCorrelationId;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region GetMembers Tests

    [Fact]
    public async Task GetMembers_WithValidQuery_ShouldReturnPaginatedResponse()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            Page = 1,
            PageSize = 25,
            SearchTerm = "test"
        };

        var members = new List<GuildMemberDto>
        {
            new GuildMemberDto
            {
                UserId = TestUserId,
                Username = "testuser",
                Discriminator = "1234",
                JoinedAt = DateTime.UtcNow.AddDays(-30),
                IsActive = true
            }
        };

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = members,
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(TestGuildId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<PaginatedResponseDto<GuildMemberDto>>();

        var response = okResult.Value as PaginatedResponseDto<GuildMemberDto>;
        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(1);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(25);
        response.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMembers_WithInvalidPageNumber_ShouldReturnBadRequest()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            Page = 0,
            PageSize = 25
        };

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid page number");
        error.Detail.Should().Contain("Page number must be greater than or equal to 1");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        error.TraceId.Should().Be(TestCorrelationId);

        // Verify service was not called
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(It.IsAny<ulong>(), It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when page number is invalid");
    }

    [Fact]
    public async Task GetMembers_WithPageSizeGreaterThan100_ShouldReturnBadRequest()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            Page = 1,
            PageSize = 101
        };

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid page size");
        error.Detail.Should().Contain("Page size must be between 1 and 100");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        error.TraceId.Should().Be(TestCorrelationId);

        // Verify service was not called
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(It.IsAny<ulong>(), It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when page size is too large");
    }

    [Fact]
    public async Task GetMembers_WithPageSizeLessThan1_ShouldReturnBadRequest()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            Page = 1,
            PageSize = 0
        };

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid page size");
        error.Detail.Should().Contain("Page size must be between 1 and 100");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        error.TraceId.Should().Be(TestCorrelationId);

        // Verify service was not called
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(It.IsAny<ulong>(), It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when page size is invalid");
    }

    [Fact]
    public async Task GetMembers_WithDefaultPaginationValues_ShouldUseDefaults()
    {
        // Arrange
        var query = new GuildMemberQueryDto(); // Uses defaults: Page=1, PageSize=25

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = new List<GuildMemberDto>(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(TestGuildId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<GuildMemberDto>;
        response.Should().NotBeNull();
        response!.Page.Should().Be(1);
        response.PageSize.Should().Be(25);

        // Verify service was called with default values
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(
                TestGuildId,
                It.Is<GuildMemberQueryDto>(q => q.Page == 1 && q.PageSize == 25),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "service should be called with default pagination values");
    }

    [Fact]
    public async Task GetMembers_WithFilters_ShouldPassFiltersToService()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            Page = 1,
            PageSize = 50,
            SearchTerm = "testuser",
            RoleIds = new List<ulong> { 111111UL, 222222UL },
            JoinedAtStart = DateTime.UtcNow.AddDays(-60),
            JoinedAtEnd = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            SortBy = "Username",
            SortDescending = true
        };

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = new List<GuildMemberDto>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(TestGuildId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        // Verify all filters were passed to service
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(
                TestGuildId,
                It.Is<GuildMemberQueryDto>(q =>
                    q.SearchTerm == "testuser" &&
                    q.RoleIds!.Count == 2 &&
                    q.RoleIds.Contains(111111UL) &&
                    q.RoleIds.Contains(222222UL) &&
                    q.JoinedAtStart.HasValue &&
                    q.JoinedAtEnd.HasValue &&
                    q.IsActive == true &&
                    q.SortBy == "Username" &&
                    q.SortDescending == true),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "service should be called with all specified filters");
    }

    [Fact]
    public async Task GetMembers_WithEmptyResults_ShouldReturnEmptyPage()
    {
        // Arrange
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 25 };

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = new List<GuildMemberDto>(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(TestGuildId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<GuildMemberDto>;
        response.Should().NotBeNull();
        response!.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMembers_ShouldLogDebugMessage()
    {
        // Arrange
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 25, SearchTerm = "test" };

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = new List<GuildMemberDto>(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(TestGuildId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _controller.GetMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Members list requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when members list is requested");
    }

    #endregion

    #region GetMemberById Tests

    [Fact]
    public async Task GetMemberById_WithExistingMember_ShouldReturnMember()
    {
        // Arrange
        var member = new GuildMemberDto
        {
            UserId = TestUserId,
            Username = "testuser",
            Discriminator = "1234",
            GlobalDisplayName = "Test User",
            Nickname = "Tester",
            AvatarHash = "abc123",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            LastActiveAt = DateTime.UtcNow.AddHours(-1),
            AccountCreatedAt = DateTime.UtcNow.AddYears(-2),
            RoleIds = new List<ulong> { 111111UL },
            IsActive = true,
            LastCachedAt = DateTime.UtcNow
        };

        _mockGuildMemberService
            .Setup(s => s.GetMemberAsync(TestGuildId, TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _controller.GetMemberById(TestGuildId, TestUserId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<GuildMemberDto>();

        var memberDto = okResult.Value as GuildMemberDto;
        memberDto.Should().NotBeNull();
        memberDto!.UserId.Should().Be(TestUserId);
        memberDto.Username.Should().Be("testuser");
        memberDto.Nickname.Should().Be("Tester");
        memberDto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetMemberById_WithNonExistentMember_ShouldReturnNotFound()
    {
        // Arrange
        const ulong nonExistentUserId = 999999999UL;

        _mockGuildMemberService
            .Setup(s => s.GetMemberAsync(TestGuildId, nonExistentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMemberDto?)null);

        // Act
        var result = await _controller.GetMemberById(TestGuildId, nonExistentUserId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Member not found");
        error.Detail.Should().Contain(nonExistentUserId.ToString());
        error.Detail.Should().Contain(TestGuildId.ToString());
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        error.TraceId.Should().Be(TestCorrelationId);
    }

    [Fact]
    public async Task GetMemberById_ShouldLogDebugMessage()
    {
        // Arrange
        var member = new GuildMemberDto
        {
            UserId = TestUserId,
            Username = "testuser",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = true
        };

        _mockGuildMemberService
            .Setup(s => s.GetMemberAsync(TestGuildId, TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        await _controller.GetMemberById(TestGuildId, TestUserId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Member") && v.ToString()!.Contains("requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when a specific member is requested");
    }

    [Fact]
    public async Task GetMemberById_WithNotFound_ShouldLogWarningMessage()
    {
        // Arrange
        const ulong nonExistentUserId = 999999999UL;

        _mockGuildMemberService
            .Setup(s => s.GetMemberAsync(TestGuildId, nonExistentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMemberDto?)null);

        // Act
        await _controller.GetMemberById(TestGuildId, nonExistentUserId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning log should be written when member is not found");
    }

    #endregion

    #region ExportMembersToCsv Tests

    [Fact]
    public async Task ExportMembers_WithValidQuery_ShouldReturnFileResult()
    {
        // Arrange
        var query = new GuildMemberQueryDto { SearchTerm = "test" };
        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username,JoinedAt\n123456789,testuser,2023-01-01");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                TestGuildId,
                query,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _controller.ExportMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FileContentResult>();

        var fileResult = result as FileContentResult;
        fileResult.Should().NotBeNull();
        fileResult!.FileContents.Should().BeEquivalentTo(csvData);
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().Contain("members");
        fileResult.FileDownloadName.Should().Contain(TestGuildId.ToString());
        fileResult.FileDownloadName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task ExportMembers_ShouldPassFiltersToService()
    {
        // Arrange
        var query = new GuildMemberQueryDto
        {
            SearchTerm = "test",
            RoleIds = new List<ulong> { 111111UL },
            IsActive = true,
            JoinedAtStart = DateTime.UtcNow.AddDays(-60)
        };

        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username\n");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                TestGuildId,
                query,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        await _controller.ExportMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        _mockGuildMemberService.Verify(
            s => s.ExportMembersToCsvAsync(
                TestGuildId,
                It.Is<GuildMemberQueryDto>(q =>
                    q.SearchTerm == "test" &&
                    q.RoleIds!.Count == 1 &&
                    q.RoleIds.Contains(111111UL) &&
                    q.IsActive == true &&
                    q.JoinedAtStart.HasValue),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "service should be called with all specified filters");
    }

    [Fact]
    public async Task ExportMembers_WithEmptyResults_ShouldReturnEmptyCsv()
    {
        // Arrange
        var query = new GuildMemberQueryDto();
        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username,JoinedAt\n");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                TestGuildId,
                query,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _controller.ExportMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FileContentResult>();

        var fileResult = result as FileContentResult;
        fileResult.Should().NotBeNull();
        fileResult!.FileContents.Should().NotBeEmpty();
        fileResult.FileContents.Should().BeEquivalentTo(csvData);
    }

    [Fact]
    public async Task ExportMembers_ShouldGenerateCorrectFilename()
    {
        // Arrange
        var query = new GuildMemberQueryDto();
        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username\n");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                TestGuildId,
                query,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _controller.ExportMembers(TestGuildId, query, CancellationToken.None);

        // Assert
        var fileResult = result as FileContentResult;
        fileResult.Should().NotBeNull();

        // Filename should follow pattern: members-{guildId}-{timestamp}.csv
        fileResult!.FileDownloadName.Should().MatchRegex(@"^members-\d+-\d{8}-\d{6}\.csv$");
        fileResult.FileDownloadName.Should().Contain(TestGuildId.ToString());
    }

    [Fact]
    public async Task ExportMembers_ShouldLogInformationMessages()
    {
        // Arrange
        var query = new GuildMemberQueryDto { SearchTerm = "test" };
        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username\n");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                TestGuildId,
                query,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        await _controller.ExportMembers(TestGuildId, query, CancellationToken.None);

        // Assert - Should log when export is requested
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Member export requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when export is requested");

        // Assert - Should log when export is completed
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Member export completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when export is completed");
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GetMembers_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 25 };

        var paginatedResponse = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = new List<GuildMemberDto>(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(It.IsAny<ulong>(), It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _controller.GetMembers(TestGuildId, query, cancellationToken);

        // Assert
        _mockGuildMemberService.Verify(
            s => s.GetMembersAsync(TestGuildId, query, cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    [Fact]
    public async Task GetMemberById_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var member = new GuildMemberDto
        {
            UserId = TestUserId,
            Username = "testuser",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _mockGuildMemberService
            .Setup(s => s.GetMemberAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        await _controller.GetMemberById(TestGuildId, TestUserId, cancellationToken);

        // Assert
        _mockGuildMemberService.Verify(
            s => s.GetMemberAsync(TestGuildId, TestUserId, cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    [Fact]
    public async Task ExportMembers_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var query = new GuildMemberQueryDto();
        var csvData = System.Text.Encoding.UTF8.GetBytes("UserId,Username\n");

        _mockGuildMemberService
            .Setup(s => s.ExportMembersToCsvAsync(
                It.IsAny<ulong>(),
                It.IsAny<GuildMemberQueryDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        await _controller.ExportMembers(TestGuildId, query, cancellationToken);

        // Assert
        _mockGuildMemberService.Verify(
            s => s.ExportMembersToCsvAsync(TestGuildId, query, It.IsAny<int>(), cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    #endregion
}
