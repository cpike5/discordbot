using System.Security.Claims;
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
/// Unit tests for <see cref="ThemeController"/>.
/// Tests cover theme retrieval, user preference setting, and default theme management.
/// </summary>
public class ThemeControllerTests
{
    private readonly Mock<IThemeService> _mockThemeService;
    private readonly Mock<ILogger<ThemeController>> _mockLogger;
    private readonly ThemeController _controller;

    // Test themes
    private readonly ThemeDto _discordDarkTheme;
    private readonly ThemeDto _purpleDuskTheme;

    public ThemeControllerTests()
    {
        _mockThemeService = new Mock<IThemeService>();
        _mockLogger = new Mock<ILogger<ThemeController>>();

        // Initialize test theme DTOs
        _discordDarkTheme = new ThemeDto
        {
            Id = 1,
            ThemeKey = "discord-dark",
            DisplayName = "Discord Dark",
            Description = "Default dark theme",
            ColorDefinition = "{\"bgPrimary\":\"#1d2022\"}",
            IsActive = true
        };

        _purpleDuskTheme = new ThemeDto
        {
            Id = 2,
            ThemeKey = "purple-dusk",
            DisplayName = "Purple Dusk",
            Description = "Light purple theme",
            ColorDefinition = "{\"bgPrimary\":\"#E8E3DF\"}",
            IsActive = true
        };

        _controller = new ThemeController(
            _mockThemeService.Object,
            _mockLogger.Object);

        // Setup default HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext.HttpContext.User = principal;
    }

    #region GetAvailableThemes Tests

    [Fact]
    public async Task GetAvailableThemes_ReturnsOkWithThemeList()
    {
        // Arrange
        var themes = new List<ThemeDto> { _discordDarkTheme, _purpleDuskTheme };
        _mockThemeService
            .Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(themes);

        // Act
        var result = await _controller.GetAvailableThemes();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(themes);
    }

    [Fact]
    public async Task GetAvailableThemes_ReturnsEmptyList_WhenNoThemes()
    {
        // Arrange
        _mockThemeService
            .Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThemeDto>());

        // Act
        var result = await _controller.GetAvailableThemes();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var themes = okResult!.Value as IEnumerable<ThemeDto>;
        themes.Should().BeEmpty();
    }

    #endregion

    #region GetCurrentTheme Tests

    [Fact]
    public async Task GetCurrentTheme_ReturnsOk_WhenAuthenticated()
    {
        // Arrange
        SetupAuthenticatedUser("user123");

        var currentTheme = new CurrentThemeDto
        {
            Theme = _discordDarkTheme,
            Source = ThemeSource.User
        };

        _mockThemeService
            .Setup(s => s.GetUserThemeAsync("user123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentTheme);

        // Act
        var result = await _controller.GetCurrentTheme();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(currentTheme);
    }

    [Fact]
    public async Task GetCurrentTheme_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange - no user setup (unauthenticated)

        // Act
        var result = await _controller.GetCurrentTheme();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCurrentTheme_ReturnsCorrectSource_WhenSystemDefault()
    {
        // Arrange
        SetupAuthenticatedUser("user456");

        var currentTheme = new CurrentThemeDto
        {
            Theme = _discordDarkTheme,
            Source = ThemeSource.System
        };

        _mockThemeService
            .Setup(s => s.GetUserThemeAsync("user456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentTheme);

        // Act
        var result = await _controller.GetCurrentTheme();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as CurrentThemeDto;
        response!.Source.Should().Be(ThemeSource.System);
    }

    #endregion

    #region SetUserTheme Tests

    [Fact]
    public async Task SetUserTheme_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        SetupAuthenticatedUser("user123");

        _mockThemeService
            .Setup(s => s.SetUserThemeAsync("user123", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new SetUserThemeDto { ThemeId = 2 };

        // Act
        var result = await _controller.SetUserTheme(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetUserTheme_ReturnsBadRequest_WhenThemeNotFound()
    {
        // Arrange
        SetupAuthenticatedUser("user123");

        _mockThemeService
            .Setup(s => s.SetUserThemeAsync("user123", 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new SetUserThemeDto { ThemeId = 999 };

        // Act
        var result = await _controller.SetUserTheme(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().BeOfType<ApiErrorDto>();
    }

    [Fact]
    public async Task SetUserTheme_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange - no user setup

        var request = new SetUserThemeDto { ThemeId = 1 };

        // Act
        var result = await _controller.SetUserTheme(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetUserTheme_ClearsPreference_WhenThemeIdNull()
    {
        // Arrange
        SetupAuthenticatedUser("user123");

        _mockThemeService
            .Setup(s => s.SetUserThemeAsync("user123", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new SetUserThemeDto { ThemeId = null };

        // Act
        var result = await _controller.SetUserTheme(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockThemeService.Verify(
            s => s.SetUserThemeAsync("user123", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SetDefaultTheme Tests

    [Fact]
    public async Task SetDefaultTheme_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        SetupAuthenticatedUser("superadmin123");

        _mockThemeService
            .Setup(s => s.SetDefaultThemeAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new SetDefaultThemeDto { ThemeId = 2 };

        // Act
        var result = await _controller.SetDefaultTheme(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetDefaultTheme_ReturnsBadRequest_WhenThemeNotFound()
    {
        // Arrange
        SetupAuthenticatedUser("superadmin123");

        _mockThemeService
            .Setup(s => s.SetDefaultThemeAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new SetDefaultThemeDto { ThemeId = 999 };

        // Act
        var result = await _controller.SetDefaultTheme(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().BeOfType<ApiErrorDto>();
    }

    [Fact]
    public async Task SetDefaultTheme_CallsServiceWithCorrectThemeId()
    {
        // Arrange
        SetupAuthenticatedUser("superadmin123");

        _mockThemeService
            .Setup(s => s.SetDefaultThemeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new SetDefaultThemeDto { ThemeId = 1 };

        // Act
        await _controller.SetDefaultTheme(request);

        // Assert
        _mockThemeService.Verify(
            s => s.SetDefaultThemeAsync(1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
