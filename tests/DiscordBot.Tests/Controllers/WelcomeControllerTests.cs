using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="WelcomeController"/>.
/// </summary>
public class WelcomeControllerTests
{
    private readonly Mock<IWelcomeService> _mockWelcomeService;
    private readonly Mock<IAuditLogService> _mockAuditLog;
    private readonly Mock<IAuditLogBuilder> _mockAuditBuilder;
    private readonly Mock<ILogger<WelcomeController>> _mockLogger;
    private readonly WelcomeController _controller;

    public WelcomeControllerTests()
    {
        _mockWelcomeService = new Mock<IWelcomeService>();
        _mockLogger = new Mock<ILogger<WelcomeController>>();
        _mockAuditLog = new Mock<IAuditLogService>();
        _mockAuditBuilder = new Mock<IAuditLogBuilder>();
        _mockAuditBuilder.Setup(b => b.ForCategory(It.IsAny<AuditLogCategory>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.WithAction(It.IsAny<AuditLogAction>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.ByUser(It.IsAny<string>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.InGuild(It.IsAny<ulong>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.WithDetails(It.IsAny<object>())).Returns(_mockAuditBuilder.Object);
        _mockAuditBuilder.Setup(b => b.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditLog.Setup(a => a.CreateBuilder()).Returns(_mockAuditBuilder.Object);
        _controller = new WelcomeController(_mockWelcomeService.Object, _mockAuditLog.Object, _mockLogger.Object);

        // Setup HttpContext for GetCorrelationId() extension method
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetConfiguration Tests

    [Fact]
    public async Task GetConfiguration_ReturnsOk_WhenConfigurationExists()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var configuration = new WelcomeConfigurationDto
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome {user} to {guild}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#5865F2",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _controller.GetConfiguration(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<WelcomeConfigurationDto>();

        var dto = okResult.Value as WelcomeConfigurationDto;
        dto.Should().NotBeNull();
        dto!.GuildId.Should().Be(guildId);
        dto.IsEnabled.Should().BeTrue();
        dto.WelcomeChannelId.Should().Be(987654321UL);
        dto.WelcomeMessage.Should().Be("Welcome {user} to {guild}!");
        dto.IncludeAvatar.Should().BeTrue();
        dto.UseEmbed.Should().BeTrue();
        dto.EmbedColor.Should().Be("#5865F2");
    }

    [Fact]
    public async Task GetConfiguration_ReturnsNotFound_WhenConfigurationNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        // Act
        var result = await _controller.GetConfiguration(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Welcome configuration not found");
        error.Detail.Should().Contain(guildId.ToString());
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        error.TraceId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region UpdateConfiguration Tests

    [Fact]
    public async Task UpdateConfiguration_ReturnsOk_WhenUpdateSucceeds()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome to the server!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#FF5733"
        };

        var updatedConfiguration = new WelcomeConfigurationDto
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome to the server!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#FF5733",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _mockWelcomeService
            .Setup(s => s.UpdateConfigurationAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedConfiguration);

        // Act
        var result = await _controller.UpdateConfiguration(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<WelcomeConfigurationDto>();

        var dto = okResult.Value as WelcomeConfigurationDto;
        dto.Should().NotBeNull();
        dto!.GuildId.Should().Be(guildId);
        dto.IsEnabled.Should().BeTrue();
        dto.WelcomeChannelId.Should().Be(987654321UL);
        dto.WelcomeMessage.Should().Be("Welcome to the server!");
        dto.UseEmbed.Should().BeTrue();
        dto.EmbedColor.Should().Be("#FF5733");
    }

    [Fact]
    public async Task UpdateConfiguration_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        // Act
        var result = await _controller.UpdateConfiguration(guildId, null!, CancellationToken.None);

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
        error.TraceId.Should().NotBeNullOrEmpty();

        // Verify service was not called
        _mockWelcomeService.Verify(
            s => s.UpdateConfigurationAsync(It.IsAny<ulong>(), It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when request is null");
    }

    [Fact]
    public async Task UpdateConfiguration_ReturnsNotFound_WhenGuildNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var request = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true,
            WelcomeMessage = "Welcome!"
        };

        _mockWelcomeService
            .Setup(s => s.UpdateConfigurationAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        // Act
        var result = await _controller.UpdateConfiguration(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Server not found");
        error.Detail.Should().Contain(guildId.ToString());
        error.Detail.Should().Contain("exists in the database");
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        error.TraceId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region PreviewMessage Tests

    [Fact]
    public async Task PreviewMessage_ReturnsOk_WithPreviewMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 555555555UL;
        const string previewMessage = "Welcome TestUser to Test Guild! We now have 150 members.";

        var request = new WelcomePreviewRequestDto
        {
            PreviewUserId = userId
        };

        _mockWelcomeService
            .Setup(s => s.PreviewWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previewMessage);

        // Act
        var result = await _controller.PreviewMessage(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        // The result contains an anonymous object with Message property
        var value = okResult!.Value;
        value.Should().NotBeNull();

        var messageProperty = value!.GetType().GetProperty("Message");
        messageProperty.Should().NotBeNull();
        messageProperty!.GetValue(value).Should().Be(previewMessage);
    }

    [Fact]
    public async Task PreviewMessage_ReturnsBadRequest_WhenRequestIsNull()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        // Act
        var result = await _controller.PreviewMessage(guildId, null!, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("cannot be null");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        error.TraceId.Should().NotBeNullOrEmpty();

        // Verify service was not called
        _mockWelcomeService.Verify(
            s => s.PreviewWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when request is null");
    }

    [Fact]
    public async Task PreviewMessage_ReturnsBadRequest_WhenPreviewUserIdIsZero()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new WelcomePreviewRequestDto
        {
            PreviewUserId = 0
        };

        // Act
        var result = await _controller.PreviewMessage(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = badRequestResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid request");
        error.Detail.Should().Contain("PreviewUserId");
        error.Detail.Should().Contain("valid Discord user ID");
        error.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        error.TraceId.Should().NotBeNullOrEmpty();

        // Verify service was not called
        _mockWelcomeService.Verify(
            s => s.PreviewWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when PreviewUserId is zero");
    }

    [Fact]
    public async Task PreviewMessage_ReturnsNotFound_WhenConfigurationNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        const ulong userId = 555555555UL;

        var request = new WelcomePreviewRequestDto
        {
            PreviewUserId = userId
        };

        _mockWelcomeService
            .Setup(s => s.PreviewWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.PreviewMessage(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.Value.Should().BeOfType<ApiErrorDto>();

        var error = notFoundResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Welcome configuration not found");
        error.Detail.Should().Contain(guildId.ToString());
        error.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        error.TraceId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task GetConfiguration_ShouldLogDebugMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var configuration = new WelcomeConfigurationDto { GuildId = guildId };

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        await _controller.GetConfiguration(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Welcome configuration requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when configuration is requested");
    }

    [Fact]
    public async Task UpdateConfiguration_ShouldLogInformationMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new WelcomeConfigurationUpdateDto { IsEnabled = true };
        var updatedConfiguration = new WelcomeConfigurationDto { GuildId = guildId };

        _mockWelcomeService
            .Setup(s => s.UpdateConfigurationAsync(guildId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedConfiguration);

        // Act
        await _controller.UpdateConfiguration(guildId, request, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Welcome configuration update requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when configuration update is requested");
    }

    [Fact]
    public async Task PreviewMessage_ShouldLogDebugMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 555555555UL;
        var request = new WelcomePreviewRequestDto { PreviewUserId = userId };

        _mockWelcomeService
            .Setup(s => s.PreviewWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Preview message");

        // Act
        await _controller.PreviewMessage(guildId, request, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Welcome message preview requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when preview is requested");
    }

    [Fact]
    public async Task GetConfiguration_ShouldLogWarning_WhenNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        // Act
        await _controller.GetConfiguration(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Welcome configuration not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning log should be written when configuration is not found");
    }

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task GetConfiguration_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var configuration = new WelcomeConfigurationDto { GuildId = guildId };

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        await _controller.GetConfiguration(guildId, cancellationToken);

        // Assert
        _mockWelcomeService.Verify(
            s => s.GetConfigurationAsync(guildId, cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    [Fact]
    public async Task UpdateConfiguration_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new WelcomeConfigurationUpdateDto { IsEnabled = true };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var configuration = new WelcomeConfigurationDto { GuildId = guildId };

        _mockWelcomeService
            .Setup(s => s.UpdateConfigurationAsync(It.IsAny<ulong>(), It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        await _controller.UpdateConfiguration(guildId, request, cancellationToken);

        // Assert
        _mockWelcomeService.Verify(
            s => s.UpdateConfigurationAsync(guildId, request, cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    [Fact]
    public async Task PreviewMessage_WithCancellationToken_ShouldPassToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 555555555UL;
        var request = new WelcomePreviewRequestDto { PreviewUserId = userId };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockWelcomeService
            .Setup(s => s.PreviewWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Preview message");

        // Act
        await _controller.PreviewMessage(guildId, request, cancellationToken);

        // Assert
        _mockWelcomeService.Verify(
            s => s.PreviewWelcomeMessageAsync(guildId, userId, cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the service");
    }

    #endregion

    #region SendTestMessage Tests

    private const ulong TestGuildId = 123456789UL;
    private const ulong TestAdminId = 555000111UL;

    private void SignInAsDiscordUser(ulong? discordUserId)
    {
        var claims = new List<Claim>();
        if (discordUserId.HasValue)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.DiscordUserIdClaimType, discordUserId.Value.ToString()));
        }

        _controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static WelcomeConfigurationDto ReadyConfig() => new()
    {
        GuildId = TestGuildId,
        IsEnabled = true,
        WelcomeChannelId = 987654321UL,
        WelcomeMessage = "Welcome {user}!"
    };

    [Fact]
    public async Task SendTestMessage_ReturnsOkAndAuditLogs_WhenMessageSent()
    {
        // Arrange
        SignInAsDiscordUser(TestAdminId);
        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadyConfig());
        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(TestGuildId, TestAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWelcomeService.Verify(
            s => s.SendWelcomeMessageAsync(TestGuildId, TestAdminId, It.IsAny<CancellationToken>()),
            Times.Once,
            "the signed-in admin stands in as the new member");
        _mockAuditBuilder.Verify(b => b.ByUser(TestAdminId.ToString()), Times.Once);
        _mockAuditBuilder.Verify(b => b.InGuild(TestGuildId), Times.Once);
        _mockAuditBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTestMessage_ReturnsBadRequest_WhenWelcomeDisabled()
    {
        // Arrange
        SignInAsDiscordUser(TestAdminId);
        var config = ReadyConfig();
        config.IsEnabled = false;
        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ApiErrorDto>().Which.Detail.Should().Contain("disabled");
        _mockWelcomeService.Verify(
            s => s.SendWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockAuditBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTestMessage_ReturnsBadRequest_WhenNoChannelConfigured()
    {
        // Arrange
        SignInAsDiscordUser(TestAdminId);
        var config = ReadyConfig();
        config.WelcomeChannelId = null;
        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ApiErrorDto>().Which.Detail.Should().Contain("channel");
        _mockWelcomeService.Verify(
            s => s.SendWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendTestMessage_ReturnsBadRequest_WhenNoConfigurationExists()
    {
        // Arrange
        SignInAsDiscordUser(TestAdminId);
        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SendTestMessage_Returns500AndDoesNotAudit_WhenSendFails()
    {
        // Arrange
        SignInAsDiscordUser(TestAdminId);
        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadyConfig());
        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(TestGuildId, TestAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        _mockAuditBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTestMessage_ReturnsForbidden_WhenNoLinkedDiscordAccount()
    {
        // Arrange
        SignInAsDiscordUser(null);

        // Act
        var result = await _controller.SendTestMessage(TestGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _mockWelcomeService.Verify(
            s => s.SendWelcomeMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
