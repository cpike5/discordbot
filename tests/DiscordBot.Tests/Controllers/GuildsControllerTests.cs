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
/// Unit tests for <see cref="GuildsController"/>.
/// </summary>
public class GuildsControllerTests
{
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ILogger<GuildsController>> _mockLogger;
    private readonly GuildsController _controller;

    public GuildsControllerTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockLogger = new Mock<ILogger<GuildsController>>();
        _controller = new GuildsController(_mockGuildService.Object, _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetAllGuilds_ShouldReturnOkWithGuildList()
    {
        // Arrange
        var guilds = new List<GuildDto>
        {
            new GuildDto
            {
                Id = 111111111UL,
                Name = "Test Guild 1",
                JoinedAt = DateTime.UtcNow.AddDays(-30),
                IsActive = true,
                Prefix = "!",
                Settings = null,
                MemberCount = 100,
                IconUrl = "https://cdn.discord.com/icon1.png"
            },
            new GuildDto
            {
                Id = 222222222UL,
                Name = "Test Guild 2",
                JoinedAt = DateTime.UtcNow.AddDays(-15),
                IsActive = true,
                Prefix = "?",
                Settings = "{}",
                MemberCount = 50,
                IconUrl = null
            }
        }.AsReadOnly();

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.GetAllGuilds(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeAssignableTo<IReadOnlyList<GuildDto>>();

        var guildList = okResult.Value as IReadOnlyList<GuildDto>;
        guildList.Should().HaveCount(2);
        guildList![0].Id.Should().Be(111111111UL);
        guildList[1].Id.Should().Be(222222222UL);
    }

    [Fact]
    public async Task GetAllGuilds_WithNoGuilds_ShouldReturnEmptyList()
    {
        // Arrange
        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());

        // Act
        var result = await _controller.GetAllGuilds(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var guildList = okResult!.Value as IReadOnlyList<GuildDto>;
        guildList.Should().NotBeNull();
        guildList.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuildById_WithExistingGuild_ShouldReturnOkWithGuild()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            Prefix = "!",
            Settings = null,
            MemberCount = 100,
            IconUrl = "https://cdn.discord.com/icon.png"
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        // Act
        var result = await _controller.GetGuildById(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<GuildDto>();

        var guildDto = okResult.Value as GuildDto;
        guildDto.Should().NotBeNull();
        guildDto!.Id.Should().Be(guildId);
        guildDto.Name.Should().Be("Test Guild");
    }

    [Fact]
    public async Task GetGuildById_WithNonExistentGuild_ShouldReturnNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _controller.GetGuildById(guildId, CancellationToken.None);

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
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateGuild_WithValidRequest_ShouldReturnOkWithUpdatedGuild()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new GuildUpdateRequestDto
        {
            Prefix = "?",
            Settings = "{\"feature\":true}",
            IsActive = false
        };

        var updatedGuild = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = false,
            Prefix = "?",
            Settings = "{\"feature\":true}",
            MemberCount = 100,
            IconUrl = null
        };

        _mockGuildService
            .Setup(s => s.UpdateGuildAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedGuild);

        // Act
        var result = await _controller.UpdateGuild(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<GuildDto>();

        var guildDto = okResult.Value as GuildDto;
        guildDto.Should().NotBeNull();
        guildDto!.Id.Should().Be(guildId);
        guildDto.Prefix.Should().Be("?");
        guildDto.Settings.Should().Be("{\"feature\":true}");
        guildDto.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGuild_WithNonExistentGuild_ShouldReturnNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var request = new GuildUpdateRequestDto { Prefix = "?" };

        _mockGuildService
            .Setup(s => s.UpdateGuildAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _controller.UpdateGuild(guildId, request, CancellationToken.None);

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
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateGuild_WithNullRequest_ShouldReturnBadRequest()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        // Act
        var result = await _controller.UpdateGuild(guildId, null!, CancellationToken.None);

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
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockGuildService.Verify(
            s => s.UpdateGuildAsync(It.IsAny<ulong>(), It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when request is null");
    }

    [Fact]
    public async Task SyncGuild_WithSuccessfulSync_ShouldReturnOkWithMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SyncGuild(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        // The result contains an anonymous object with Message and GuildId properties
        var value = okResult!.Value;
        value.Should().NotBeNull();

        var messageProperty = value!.GetType().GetProperty("Message");
        messageProperty.Should().NotBeNull();
        messageProperty!.GetValue(value).Should().Be("Guild synced successfully");

        var guildIdProperty = value.GetType().GetProperty("GuildId");
        guildIdProperty.Should().NotBeNull();
        guildIdProperty!.GetValue(value).Should().Be(guildId);
    }

    [Fact]
    public async Task SyncGuild_WithNonExistentGuild_ShouldReturnNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.SyncGuild(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Guild not found");
        error.Detail.Should().Contain(guildId.ToString());
        error.Detail.Should().Contain("connected to the bot");
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetAllGuilds_ShouldLogDebugMessage()
    {
        // Arrange
        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());

        // Act
        await _controller.GetAllGuilds(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All guilds list requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when guilds list is requested");
    }

    [Fact]
    public async Task GetGuildById_ShouldLogDebugMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = new GuildDto { Id = guildId, Name = "Test", JoinedAt = DateTime.UtcNow, IsActive = true };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        // Act
        await _controller.GetGuildById(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Guild") && v.ToString()!.Contains("requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when a specific guild is requested");
    }

    [Fact]
    public async Task UpdateGuild_ShouldLogInformationMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new GuildUpdateRequestDto { Prefix = "?" };
        var updatedGuild = new GuildDto { Id = guildId, Name = "Test", JoinedAt = DateTime.UtcNow, IsActive = true, Prefix = "?" };

        _mockGuildService
            .Setup(s => s.UpdateGuildAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedGuild);

        // Act
        await _controller.UpdateGuild(guildId, request, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Guild") && v.ToString()!.Contains("update requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when guild update is requested");
    }

    [Fact]
    public async Task SyncGuild_ShouldLogInformationMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _controller.SyncGuild(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Guild") && v.ToString()!.Contains("sync requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when guild sync is requested");
    }

    [Fact]
    public async Task GetAllGuilds_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());

        // Act
        await _controller.GetAllGuilds(cancellationToken);

        // Assert
        _mockGuildService.Verify(
            s => s.GetAllGuildsAsync(cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }
}
