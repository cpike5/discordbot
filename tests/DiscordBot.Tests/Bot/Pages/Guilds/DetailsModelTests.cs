using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// Unit tests for <see cref="DetailsModel"/> Razor Page.
/// </summary>
public class DetailsModelTests
{
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly Mock<IWelcomeService> _mockWelcomeService;
    private readonly Mock<IScheduledMessageService> _mockScheduledMessageService;
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<IReminderRepository> _mockReminderRepository;
    private readonly Mock<IGuildMemberService> _mockGuildMemberService;
    private readonly Mock<IGuildAudioSettingsService> _mockGuildAudioSettingsService;
    private readonly Mock<ISoundRepository> _mockSoundRepository;
    private readonly Mock<ITtsMessageRepository> _mockTtsMessageRepository;
    private readonly Mock<IAssistantGuildSettingsService> _mockAssistantGuildSettingsService;
    private readonly Mock<ILogger<DetailsModel>> _mockLogger;
    private readonly DetailsModel _detailsModel;

    public DetailsModelTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockCommandLogService = new Mock<ICommandLogService>();
        _mockWelcomeService = new Mock<IWelcomeService>();
        _mockScheduledMessageService = new Mock<IScheduledMessageService>();
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockReminderRepository = new Mock<IReminderRepository>();
        _mockGuildMemberService = new Mock<IGuildMemberService>();
        _mockGuildAudioSettingsService = new Mock<IGuildAudioSettingsService>();
        _mockSoundRepository = new Mock<ISoundRepository>();
        _mockTtsMessageRepository = new Mock<ITtsMessageRepository>();
        _mockAssistantGuildSettingsService = new Mock<IAssistantGuildSettingsService>();
        _mockLogger = new Mock<ILogger<DetailsModel>>();

        // Setup default scheduled message service behavior
        _mockScheduledMessageService.Setup(s => s.GetByGuildIdAsync(
            It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ScheduledMessageDto>(), 0));

        // Setup default Rat Watch service behavior
        _mockRatWatchService.Setup(s => s.GetGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { IsEnabled = true, Timezone = "Eastern Standard Time" });
        _mockRatWatchService.Setup(s => s.GetByGuildAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<RatWatchDto>(), 0));
        _mockRatWatchService.Setup(s => s.GetLeaderboardAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RatLeaderboardEntryDto>());

        // Setup default Reminder repository behavior
        _mockReminderRepository.Setup(r => r.GetGuildStatsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, 0, 0));

        var assistantOptions = Options.Create(new AssistantOptions());

        _detailsModel = new DetailsModel(
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _mockWelcomeService.Object,
            _mockScheduledMessageService.Object,
            _mockRatWatchService.Object,
            _mockReminderRepository.Object,
            _mockGuildMemberService.Object,
            _mockGuildAudioSettingsService.Object,
            _mockSoundRepository.Object,
            _mockTtsMessageRepository.Object,
            _mockAssistantGuildSettingsService.Object,
            assistantOptions,
            _mockLogger.Object);

        // Setup PageContext
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _detailsModel.PageContext = pageContext;
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_ReturnsPageWithViewModel()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var joinedDate = DateTime.UtcNow.AddMonths(-3);

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 150,
            IconUrl = "https://cdn.discord.com/icons/123456789/icon.png",
            IsActive = true,
            JoinedAt = joinedDate,
            Prefix = "!",
            Settings = "{\"WelcomeChannel\":\"welcome\"}"
        };

        var commandLogs = new List<CommandLogDto>
        {
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 111UL,
                Username = "User1",
                CommandName = "ping",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-10),
                ResponseTimeMs = 50,
                Success = true
            }
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs,
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("valid guild should return PageResult");
        _detailsModel.ViewModel.Should().NotBeNull();
        _detailsModel.ViewModel.Id.Should().Be(guildId, "ViewModel should contain the guild ID");
        _detailsModel.ViewModel.Name.Should().Be("Test Guild", "ViewModel should contain the guild name");
        _detailsModel.ViewModel.MemberCount.Should().Be(150, "ViewModel should contain the member count");
        _detailsModel.ViewModel.IsActive.Should().BeTrue("ViewModel should contain the active status");
        _detailsModel.ViewModel.JoinedAt.Should().Be(joinedDate, "ViewModel should contain the joined date");
        _detailsModel.ViewModel.Prefix.Should().Be("!", "ViewModel should contain the prefix");
        _detailsModel.ViewModel.RecentCommandLogs.Should().HaveCount(1, "ViewModel should contain recent command logs");

        _mockGuildService.Verify(
            s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called once");
    }

    [Fact]
    public async Task OnGetAsync_WithInvalidGuildId_ReturnsNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("non-existent guild should return NotFound");

        _mockGuildService.Verify(
            s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "command log service should not be called if guild is not found");
    }

    [Fact]
    public async Task OnGetAsync_FetchesRecentCommandsForGuild()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var commandLogs = new List<CommandLogDto>
        {
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 111UL,
                Username = "User1",
                CommandName = "ping",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
                ResponseTimeMs = 50,
                Success = true
            },
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 222UL,
                Username = "User2",
                CommandName = "status",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-10),
                ResponseTimeMs = 75,
                Success = true
            }
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs,
            Page = 1,
            PageSize = 10,
            TotalCount = 2
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.GuildId == guildId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _detailsModel.ViewModel.RecentCommandLogs.Should().HaveCount(2, "recent command logs should be fetched for the guild");
        _detailsModel.ViewModel.RecentCommandLogs[0].CommandName.Should().Be("ping");
        _detailsModel.ViewModel.RecentCommandLogs[1].CommandName.Should().Be("status");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.GuildId == guildId),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called with the guild ID filter");
    }

    [Fact]
    public async Task OnGetAsync_LimitsRecentCommandsTo10()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.GuildId == guildId &&
                    q.Page == 1 &&
                    q.PageSize == 10),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called with PageSize = 10 to limit recent commands");
    }

    [Fact]
    public async Task OnGetAsync_LogsInformationMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User accessing guild details page") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when accessing guild details");
    }

    [Fact]
    public async Task OnGetAsync_LogsWarningWhenGuildNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Guild") &&
                    v.ToString()!.Contains("not found") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning log should be written when guild is not found");
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessageWithCommandCount()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var commandLogs = new List<CommandLogDto>
        {
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 111UL,
                Username = "User1",
                CommandName = "ping",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = 50,
                Success = true
            },
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 222UL,
                Username = "User2",
                CommandName = "status",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = 75,
                Success = true
            },
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 333UL,
                Username = "User3",
                CommandName = "help",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = 100,
                Success = true
            }
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs,
            Page = 1,
            PageSize = 10,
            TotalCount = 3
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Retrieved guild") &&
                    v.ToString()!.Contains(guildId.ToString()) &&
                    v.ToString()!.Contains("3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written with guild ID and command count");
    }

    [Fact]
    public async Task OnGetAsync_WithCancellationToken_PassesToServices()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _detailsModel.OnGetAsync(guildId, cancellationToken);

        // Assert
        _mockGuildService.Verify(
            s => s.GetGuildByIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to guild service");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), cancellationToken),
            Times.Once,
            "cancellation token should be passed to command log service");
    }

    [Fact]
    public async Task OnGetAsync_WithNoRecentCommands_ReturnsEmptyCommandList()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _detailsModel.ViewModel.RecentCommandLogs.Should().NotBeNull();
        _detailsModel.ViewModel.RecentCommandLogs.Should().BeEmpty("guild with no command history should have empty command list");
    }

    [Fact]
    public async Task OnGetAsync_ParsesGuildSettings()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = "{\"WelcomeChannel\":\"welcome\",\"LogChannel\":\"logs\",\"AutoModEnabled\":true}"
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _detailsModel.ViewModel.Settings.Should().NotBeNull();
        _detailsModel.ViewModel.Settings.WelcomeChannel.Should().Be("welcome", "settings should be parsed correctly");
        _detailsModel.ViewModel.Settings.LogChannel.Should().Be("logs", "settings should be parsed correctly");
        _detailsModel.ViewModel.Settings.AutoModEnabled.Should().BeTrue("settings should be parsed correctly");
        _detailsModel.ViewModel.Settings.HasSettings.Should().BeTrue("parsed settings should have HasSettings = true");
    }

    [Fact]
    public void ViewModel_InitializesWithEmptyInstance()
    {
        // Arrange & Act
        var assistantOptions = Options.Create(new AssistantOptions());
        var detailsModel = new DetailsModel(
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _mockWelcomeService.Object,
            _mockScheduledMessageService.Object,
            _mockRatWatchService.Object,
            _mockReminderRepository.Object,
            _mockGuildMemberService.Object,
            _mockGuildAudioSettingsService.Object,
            _mockSoundRepository.Object,
            _mockTtsMessageRepository.Object,
            _mockAssistantGuildSettingsService.Object,
            assistantOptions,
            _mockLogger.Object);

        // Assert
        detailsModel.ViewModel.Should().NotBeNull("ViewModel should be initialized");
        detailsModel.ViewModel.Should().BeOfType<GuildDetailViewModel>();
    }
}
