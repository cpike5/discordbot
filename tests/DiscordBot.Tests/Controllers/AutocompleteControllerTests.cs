using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="AutocompleteController"/>.
/// </summary>
public class AutocompleteControllerTests
{
    private readonly Mock<ILogger<AutocompleteController>> _mockLogger;
    private readonly Mock<IMessageLogRepository> _mockMessageLogRepository;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ICommandMetadataService> _mockCommandMetadataService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly AutocompleteController _controller;

    public AutocompleteControllerTests()
    {
        _mockLogger = new Mock<ILogger<AutocompleteController>>();
        _mockMessageLogRepository = new Mock<IMessageLogRepository>();
        _mockGuildService = new Mock<IGuildService>();
        _mockCommandMetadataService = new Mock<ICommandMetadataService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>();

        _controller = new AutocompleteController(
            _mockLogger.Object,
            _mockMessageLogRepository.Object,
            _mockGuildService.Object,
            _mockCommandMetadataService.Object,
            _mockDiscordClient.Object);

        // Setup HttpContext for correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items["CorrelationId"] = "test-correlation-id";
    }

    #region SearchUsers Tests

    [Fact]
    public async Task SearchUsers_WithValidSearch_ReturnsOkWithResults()
    {
        // Arrange
        const string searchTerm = "test";
        var authors = new List<(ulong UserId, string Username)>
        {
            (123456789UL, "testuser1"),
            (987654321UL, "testuser2")
        };

        _mockMessageLogRepository
            .Setup(r => r.SearchAuthorsAsync(searchTerm, null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authors);

        // Act
        var result = await _controller.SearchUsers(searchTerm, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeAssignableTo<List<AutocompleteSuggestionDto>>();

        var suggestions = okResult.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(2);
        suggestions![0].Id.Should().Be("123456789");
        suggestions[0].DisplayText.Should().Be("testuser1");
        suggestions[1].Id.Should().Be("987654321");
        suggestions[1].DisplayText.Should().Be("testuser2");
    }

    [Fact]
    public async Task SearchUsers_WithGuildIdFilter_PassesGuildIdToRepository()
    {
        // Arrange
        const string searchTerm = "test";
        const ulong guildId = 111222333UL;
        var authors = new List<(ulong UserId, string Username)>
        {
            (123456789UL, "testuser1")
        };

        _mockMessageLogRepository
            .Setup(r => r.SearchAuthorsAsync(searchTerm, guildId, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authors);

        // Act
        var result = await _controller.SearchUsers(searchTerm, guildId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mockMessageLogRepository.Verify(
            r => r.SearchAuthorsAsync(searchTerm, guildId, 25, It.IsAny<CancellationToken>()),
            Times.Once,
            "should pass guildId filter to repository");
    }

    [Fact]
    public async Task SearchUsers_WithEmptySearch_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "";

        // Act
        var result = await _controller.SearchUsers(searchTerm, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Search term cannot be empty");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockMessageLogRepository.Verify(
            r => r.SearchAuthorsAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository should not be called when search term is empty");
    }

    [Fact]
    public async Task SearchUsers_WithWhitespaceSearch_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "   ";

        // Act
        var result = await _controller.SearchUsers(searchTerm, null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error!.Detail.Should().Contain("Search term cannot be empty");

        _mockMessageLogRepository.Verify(
            r => r.SearchAuthorsAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchUsers_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        const string searchTerm = "nonexistent";
        var emptyAuthors = new List<(ulong UserId, string Username)>();

        _mockMessageLogRepository
            .Setup(r => r.SearchAuthorsAsync(searchTerm, null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyAuthors);

        // Act
        var result = await _controller.SearchUsers(searchTerm, null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().NotBeNull();
        suggestions.Should().BeEmpty();
    }

    #endregion

    #region SearchGuilds Tests

    [Fact]
    public async Task SearchGuilds_WithValidSearch_ReturnsOkWithResults()
    {
        // Arrange
        const string searchTerm = "test";
        var guilds = new List<GuildDto>
        {
            new GuildDto
            {
                Id = 111111111UL,
                Name = "Test Guild 1",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            },
            new GuildDto
            {
                Id = 222222222UL,
                Name = "Test Guild 2",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            }
        }.AsReadOnly();

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeAssignableTo<List<AutocompleteSuggestionDto>>();

        var suggestions = okResult.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(2);
        suggestions![0].Id.Should().Be("111111111");
        suggestions[0].DisplayText.Should().Be("Test Guild 1");
        suggestions[1].Id.Should().Be("222222222");
        suggestions[1].DisplayText.Should().Be("Test Guild 2");
    }

    [Fact]
    public async Task SearchGuilds_WithCaseInsensitiveSearch_ReturnsMatchingGuilds()
    {
        // Arrange
        const string searchTerm = "TEST";
        var guilds = new List<GuildDto>
        {
            new GuildDto
            {
                Id = 111111111UL,
                Name = "test guild",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            }
        }.AsReadOnly();

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(1);
        suggestions![0].DisplayText.Should().Be("test guild");
    }

    [Fact]
    public async Task SearchGuilds_WithPartialMatch_ReturnsMatchingGuilds()
    {
        // Arrange
        const string searchTerm = "prod";
        var guilds = new List<GuildDto>
        {
            new GuildDto { Id = 1UL, Name = "Production Server", JoinedAt = DateTime.UtcNow, IsActive = true },
            new GuildDto { Id = 2UL, Name = "Test Server", JoinedAt = DateTime.UtcNow, IsActive = true },
            new GuildDto { Id = 3UL, Name = "Prod Environment", JoinedAt = DateTime.UtcNow, IsActive = true }
        }.AsReadOnly();

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(2);
        suggestions!.Should().OnlyContain(s => s.DisplayText.Contains("Prod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchGuilds_WithEmptySearch_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "";

        // Act
        var result = await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Search term cannot be empty");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockGuildService.Verify(
            s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when search term is empty");
    }

    [Fact]
    public async Task SearchGuilds_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        const string searchTerm = "nonexistent";
        var guilds = new List<GuildDto>
        {
            new GuildDto { Id = 1UL, Name = "Test Server", JoinedAt = DateTime.UtcNow, IsActive = true }
        }.AsReadOnly();

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().NotBeNull();
        suggestions.Should().BeEmpty();
    }

    #endregion

    #region SearchChannels Tests

    // Note: Channel search tests are limited due to Discord.NET's SocketGuild.Channels not being virtual.
    // Integration tests would be needed to fully test channel searching functionality.

    [Fact]
    public async Task SearchChannels_WithEmptySearch_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "";
        const ulong guildId = 123456789UL;

        // Act
        var result = await _controller.SearchChannels(searchTerm, guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Search term cannot be empty");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SearchChannels_WithoutGuildId_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "general";
        ulong? guildId = null;

        // Act
        var result = await _controller.SearchChannels(searchTerm, guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Guild ID is required");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockDiscordClient.Verify(
            c => c.GetGuild(It.IsAny<ulong>()),
            Times.Never,
            "Discord client should not be called when guild ID is null");
    }

    [Fact]
    public async Task SearchChannels_WithNonExistentGuild_ReturnsNotFound()
    {
        // Arrange
        const string searchTerm = "general";
        const ulong guildId = 999999999UL;

        _mockDiscordClient
            .Setup(c => c.GetGuild(guildId))
            .Returns((SocketGuild?)null);

        // Act
        var result = await _controller.SearchChannels(searchTerm, guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Guild not found");
        error.Detail.Should().Contain(guildId.ToString());
        error.Detail.Should().Contain("connected to the bot");
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region SearchCommands Tests

    [Fact]
    public async Task SearchCommands_WithValidSearch_ReturnsOkWithResults()
    {
        // Arrange
        const string searchTerm = "ping";
        var modules = new List<CommandModuleDto>
        {
            new CommandModuleDto
            {
                Name = "GeneralModule",
                DisplayName = "General",
                Commands = new List<CommandInfoDto>
                {
                    new CommandInfoDto
                    {
                        Name = "ping",
                        FullName = "ping",
                        Description = "Check bot latency"
                    },
                    new CommandInfoDto
                    {
                        Name = "help",
                        FullName = "help",
                        Description = "Show help information"
                    }
                }
            }
        }.AsReadOnly();

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        // Act
        var result = await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeAssignableTo<List<AutocompleteSuggestionDto>>();

        var suggestions = okResult.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(1);
        suggestions![0].Id.Should().Be("ping");
        suggestions[0].DisplayText.Should().Be("/ping - Check bot latency");
    }

    [Fact]
    public async Task SearchCommands_WithPartialMatch_ReturnsMatchingCommands()
    {
        // Arrange
        const string searchTerm = "rat";
        var modules = new List<CommandModuleDto>
        {
            new CommandModuleDto
            {
                Name = "RatWatchModule",
                DisplayName = "Rat Watch",
                Commands = new List<CommandInfoDto>
                {
                    new CommandInfoDto
                    {
                        Name = "rat-clear",
                        FullName = "rat-clear",
                        Description = "Clear rat accusations"
                    },
                    new CommandInfoDto
                    {
                        Name = "rat-stats",
                        FullName = "rat-stats",
                        Description = "View rat statistics"
                    }
                }
            }
        }.AsReadOnly();

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        // Act
        var result = await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(2);
        suggestions!.Should().OnlyContain(s => s.DisplayText.Contains("rat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchCommands_WithEmptySearch_ReturnsBadRequest()
    {
        // Arrange
        const string searchTerm = "";

        // Act
        var result = await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("Search term cannot be empty");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockCommandMetadataService.Verify(
            s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when search term is empty");
    }

    [Fact]
    public async Task SearchCommands_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        const string searchTerm = "nonexistent";
        var modules = new List<CommandModuleDto>
        {
            new CommandModuleDto
            {
                Name = "GeneralModule",
                DisplayName = "General",
                Commands = new List<CommandInfoDto>
                {
                    new CommandInfoDto
                    {
                        Name = "ping",
                        FullName = "ping",
                        Description = "Check bot latency"
                    }
                }
            }
        }.AsReadOnly();

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        // Act
        var result = await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().NotBeNull();
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCommands_WithGroupedCommands_ReturnsFullCommandName()
    {
        // Arrange
        const string searchTerm = "consent";
        var modules = new List<CommandModuleDto>
        {
            new CommandModuleDto
            {
                Name = "ConsentModule",
                DisplayName = "Consent",
                IsSlashGroup = true,
                GroupName = "consent",
                Commands = new List<CommandInfoDto>
                {
                    new CommandInfoDto
                    {
                        Name = "grant",
                        FullName = "consent grant",
                        Description = "Grant consent"
                    }
                }
            }
        }.AsReadOnly();

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        // Act
        var result = await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var suggestions = okResult!.Value as List<AutocompleteSuggestionDto>;
        suggestions.Should().HaveCount(1);
        suggestions![0].Id.Should().Be("consent grant");
        suggestions[0].DisplayText.Should().Be("/consent grant - Grant consent");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task SearchUsers_LogsDebugMessage()
    {
        // Arrange
        const string searchTerm = "test";
        _mockMessageLogRepository
            .Setup(r => r.SearchAuthorsAsync(searchTerm, null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(ulong UserId, string Username)>());

        // Act
        await _controller.SearchUsers(searchTerm, null, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User search requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when user search is requested");
    }

    [Fact]
    public async Task SearchGuilds_LogsDebugMessage()
    {
        // Arrange
        const string searchTerm = "test";
        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());

        // Act
        await _controller.SearchGuilds(searchTerm, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Guild search requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when guild search is requested");
    }

    // Note: Channel search logging test omitted due to Discord.NET mocking limitations

    [Fact]
    public async Task SearchCommands_LogsDebugMessage()
    {
        // Arrange
        const string searchTerm = "ping";
        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleDto>().AsReadOnly());

        // Act
        await _controller.SearchCommands(searchTerm, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command search requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when command search is requested");
    }

    [Fact]
    public async Task SearchChannels_WithNonExistentGuild_LogsWarning()
    {
        // Arrange
        const string searchTerm = "general";
        const ulong guildId = 999999999UL;

        _mockDiscordClient
            .Setup(c => c.GetGuild(guildId))
            .Returns((SocketGuild?)null);

        // Act
        await _controller.SearchChannels(searchTerm, guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found for channel search")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when guild is not found");
    }

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task SearchUsers_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        const string searchTerm = "test";
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockMessageLogRepository
            .Setup(r => r.SearchAuthorsAsync(searchTerm, null, 25, cancellationToken))
            .ReturnsAsync(new List<(ulong UserId, string Username)>());

        // Act
        await _controller.SearchUsers(searchTerm, null, cancellationToken);

        // Assert
        _mockMessageLogRepository.Verify(
            r => r.SearchAuthorsAsync(searchTerm, null, 25, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task SearchGuilds_WithCancellationToken_PassesToService()
    {
        // Arrange
        const string searchTerm = "test";
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(cancellationToken))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());

        // Act
        await _controller.SearchGuilds(searchTerm, cancellationToken);

        // Assert
        _mockGuildService.Verify(
            s => s.GetAllGuildsAsync(cancellationToken),
            Times.Once,
            "cancellation token should be passed to service");
    }

    [Fact]
    public async Task SearchCommands_WithCancellationToken_PassesToService()
    {
        // Arrange
        const string searchTerm = "ping";
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(cancellationToken))
            .ReturnsAsync(new List<CommandModuleDto>().AsReadOnly());

        // Act
        await _controller.SearchCommands(searchTerm, cancellationToken);

        // Assert
        _mockCommandMetadataService.Verify(
            s => s.GetAllModulesAsync(cancellationToken),
            Times.Once,
            "cancellation token should be passed to service");
    }

    #endregion
}
