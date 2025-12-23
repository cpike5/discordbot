using DiscordBot.Bot.Pages.CommandLogs;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace DiscordBot.Tests.Bot.Pages.CommandLogs;

/// <summary>
/// Unit tests for <see cref="IndexModel"/> Razor Page.
/// </summary>
public class IndexModelTests
{
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _indexModel;

    public IndexModelTests()
    {
        _mockCommandLogService = new Mock<ICommandLogService>();
        _mockGuildService = new Mock<IGuildService>();
        _mockLogger = new Mock<ILogger<IndexModel>>();

        _indexModel = new IndexModel(
            _mockCommandLogService.Object,
            _mockGuildService.Object,
            _mockLogger.Object);

        // Setup PageContext
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _indexModel.PageContext = pageContext;
    }

    [Fact]
    public async Task OnGetAsync_WithNoFilters_ReturnsAllLogs()
    {
        // Arrange
        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1"),
            CreateCommandLogDto(2, "status", "TestGuild2", "User2"),
            CreateCommandLogDto(3, "help", "TestGuild1", "User3")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 3
        };

        var guilds = new List<GuildDto>
        {
            new GuildDto { Id = 111UL, Name = "TestGuild1", IsActive = true, JoinedAt = DateTime.UtcNow },
            new GuildDto { Id = 222UL, Name = "TestGuild2", IsActive = true, JoinedAt = DateTime.UtcNow }
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("valid request should return PageResult");
        _indexModel.ViewModel.Should().NotBeNull();
        _indexModel.ViewModel.Logs.Should().HaveCount(3, "all logs should be returned");
        _indexModel.ViewModel.TotalCount.Should().Be(3);
        _indexModel.ViewModel.CurrentPage.Should().Be(1);
        _indexModel.ViewModel.PageSize.Should().Be(25);
        _indexModel.AvailableGuilds.Should().HaveCount(2, "all guilds should be loaded");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called once");

        _mockGuildService.Verify(
            s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnGetAsync_WithSearchTerm_AppliesSearchFilter()
    {
        // Arrange
        _indexModel.SearchTerm = "ping";

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.SearchTerm == "ping"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.SearchTerm.Should().Be("ping");
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("search term is an active filter");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.SearchTerm == "ping"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "search term should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithGuildFilter_AppliesGuildFilter()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        _indexModel.GuildId = guildId;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", guildId: guildId)
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.GuildId == guildId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.GuildId.Should().Be(guildId);
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("guild filter is active");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.GuildId == guildId),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "guild ID should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithCommandNameFilter_AppliesCommandNameFilter()
    {
        // Arrange
        _indexModel.CommandName = "ping";

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.CommandName == "ping"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.CommandName.Should().Be("ping");
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("command name filter is active");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.CommandName == "ping"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "command name should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithDateRangeFilter_AppliesDateRangeFilter()
    {
        // Arrange
        var startDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2023, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _indexModel.StartDate = startDate;
        _indexModel.EndDate = endDate;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", executedAt: startDate.AddDays(1))
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.StartDate == startDate && q.EndDate == endDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.StartDate.Should().Be(startDate);
        _indexModel.ViewModel.Filters.EndDate.Should().Be(endDate);
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("date range filter is active");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.StartDate == startDate && q.EndDate == endDate),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "date range should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithStatusFilter_AppliesStatusFilter()
    {
        // Arrange
        _indexModel.StatusFilter = true;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", success: true)
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.SuccessOnly == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.SuccessOnly.Should().BeTrue();
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("status filter is active");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.SuccessOnly == true),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "status filter should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithMultipleFilters_AppliesAllFilters()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var startDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _indexModel.SearchTerm = "ping";
        _indexModel.GuildId = guildId;
        _indexModel.CommandName = "ping";
        _indexModel.StartDate = startDate;
        _indexModel.StatusFilter = true;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", guildId: guildId)
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.SearchTerm == "ping" &&
                    q.GuildId == guildId &&
                    q.CommandName == "ping" &&
                    q.StartDate == startDate &&
                    q.SuccessOnly == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Filters.HasActiveFilters.Should().BeTrue("multiple filters are active");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.SearchTerm == "ping" &&
                    q.GuildId == guildId &&
                    q.CommandName == "ping" &&
                    q.StartDate == startDate &&
                    q.SuccessOnly == true),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "all filters should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithPagination_AppliesPaginationParameters()
    {
        // Arrange
        _indexModel.CurrentPage = 2;
        _indexModel.PageSize = 50;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 2,
            PageSize = 50,
            TotalCount = 100
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.Page == 2 && q.PageSize == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.CurrentPage.Should().Be(2);
        _indexModel.ViewModel.PageSize.Should().Be(50);
        _indexModel.ViewModel.TotalCount.Should().Be(100);

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.Page == 2 && q.PageSize == 50),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "pagination parameters should be passed to service");
    }

    [Fact]
    public async Task OnGetAsync_WithDefaultPagination_UsesDefaultValues()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.CurrentPage.Should().Be(1, "default page is 1");
        _indexModel.ViewModel.PageSize.Should().Be(25, "default page size is 25");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.Page == 1 && q.PageSize == 25),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "default pagination values should be used");
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyResults_ShowsEmptyViewModel()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _indexModel.ViewModel.Logs.Should().BeEmpty("no logs should be returned");
        _indexModel.ViewModel.TotalCount.Should().Be(0);
        _indexModel.ViewModel.TotalPages.Should().Be(0, "when no results, total pages should be 0");
        _indexModel.ViewModel.HasNextPage.Should().BeFalse();
        _indexModel.ViewModel.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task OnGetAsync_LogsInformationMessage()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User accessing command logs page")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when accessing command logs page");
    }

    [Fact]
    public async Task OnGetAsync_WithCancellationToken_PassesToServices()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        // Act
        await _indexModel.OnGetAsync(cancellationToken);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), cancellationToken),
            Times.Once,
            "cancellation token should be passed to command log service");

        _mockGuildService.Verify(
            s => s.GetAllGuildsAsync(cancellationToken),
            Times.Once,
            "cancellation token should be passed to guild service");
    }

    [Fact]
    public async Task OnGetExportAsync_ReturnsCSVFile()
    {
        // Arrange
        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", success: true),
            CreateCommandLogDto(2, "status", "TestGuild2", "User2", success: false, errorMessage: "Test error")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 10000,
            TotalCount = 2
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _indexModel.OnGetExportAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>("export should return file");
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().StartWith("command-logs-");
        fileResult.FileDownloadName.Should().EndWith(".csv");

        var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
        csvContent.Should().Contain("Timestamp,Command,Guild,User,Duration (ms),Status,Error Message");
        csvContent.Should().Contain("ping");
        csvContent.Should().Contain("status");
        csvContent.Should().Contain("TestGuild1");
        csvContent.Should().Contain("TestGuild2");
        csvContent.Should().Contain("User1");
        csvContent.Should().Contain("User2");
        csvContent.Should().Contain("Success");
        csvContent.Should().Contain("Failed");
        csvContent.Should().Contain("Test error");
    }

    [Fact]
    public async Task OnGetExportAsync_AppliesFilters()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        _indexModel.GuildId = guildId;
        _indexModel.SearchTerm = "ping";
        _indexModel.StatusFilter = true;

        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", "TestGuild1", "User1", guildId: guildId)
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 10000,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.GuildId == guildId &&
                    q.SearchTerm == "ping" &&
                    q.SuccessOnly == true &&
                    q.PageSize == 10000),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _indexModel.OnGetExportAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.GuildId == guildId &&
                    q.SearchTerm == "ping" &&
                    q.SuccessOnly == true &&
                    q.PageSize == 10000),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "filters should be applied to export query");
    }

    [Fact]
    public async Task OnGetExportAsync_EscapesSpecialCharactersInCSV()
    {
        // Arrange
        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "test,command", "Guild,With,Commas", "User\"With\"Quotes", errorMessage: "Error\nWith\nNewlines")
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 10000,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _indexModel.OnGetExportAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;

        var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
        csvContent.Should().Contain("\"test,command\"", "commas should be escaped with quotes");
        csvContent.Should().Contain("\"Guild,With,Commas\"", "commas in guild name should be escaped");
        csvContent.Should().Contain("\"User\"\"With\"\"Quotes\"", "quotes should be escaped with double quotes");
        csvContent.Should().Contain("\"Error\nWith\nNewlines\"", "newlines should be escaped with quotes");
    }

    [Fact]
    public async Task OnGetExportAsync_HandlesDirectMessageAndNullValues()
    {
        // Arrange
        var commandLogs = new List<CommandLogDto>
        {
            CreateCommandLogDto(1, "ping", null, null, success: true)
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 10000,
            TotalCount = 1
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _indexModel.OnGetExportAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;

        var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
        csvContent.Should().Contain("Direct Message", "null guild name should show as Direct Message");
        csvContent.Should().Contain("Unknown", "null username should show as Unknown");
    }

    [Fact]
    public async Task OnGetExportAsync_LogsExportAction()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 10000,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _indexModel.OnGetExportAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User exporting command logs")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when exporting");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exported") && v.ToString()!.Contains("command logs to CSV")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written with export count");
    }

    [Fact]
    public void ViewModel_InitializesWithEmptyInstance()
    {
        // Arrange & Act
        var indexModel = new IndexModel(
            _mockCommandLogService.Object,
            _mockGuildService.Object,
            _mockLogger.Object);

        // Assert
        indexModel.ViewModel.Should().NotBeNull("ViewModel should be initialized");
        indexModel.ViewModel.Should().BeOfType<CommandLogListViewModel>();
    }

    [Fact]
    public void AvailableGuilds_InitializesWithEmptyArray()
    {
        // Arrange & Act
        var indexModel = new IndexModel(
            _mockCommandLogService.Object,
            _mockGuildService.Object,
            _mockLogger.Object);

        // Assert
        indexModel.AvailableGuilds.Should().NotBeNull("AvailableGuilds should be initialized");
        indexModel.AvailableGuilds.Should().BeEmpty("AvailableGuilds should start empty");
    }

    // Helper method for creating test data
    private static CommandLogDto CreateCommandLogDto(
        int id,
        string commandName,
        string? guildName,
        string? username,
        ulong? guildId = null,
        ulong userId = 123456789UL,
        DateTime? executedAt = null,
        bool success = true,
        string? errorMessage = null)
    {
        return new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            GuildName = guildName,
            UserId = userId,
            Username = username,
            CommandName = commandName,
            Parameters = null,
            ExecutedAt = executedAt ?? DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = success,
            ErrorMessage = errorMessage
        };
    }
}
