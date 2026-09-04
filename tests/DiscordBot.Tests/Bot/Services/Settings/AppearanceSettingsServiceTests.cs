using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Settings;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DiscordBot.Tests.Bot.Services.Settings;

/// <summary>
/// Unit tests for <see cref="AppearanceSettingsService"/>.
/// </summary>
public class AppearanceSettingsServiceTests
{
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly Mock<IAuthorizationService> _mockAuthorizationService = new();
    private readonly Mock<IAuditLogQueue> _mockAuditLogQueue = new();
    private readonly AppearanceSettingsService _service;

    public AppearanceSettingsServiceTests()
    {
        _service = new AppearanceSettingsService(
            _mockThemeService.Object,
            _mockAuthorizationService.Object,
            _mockAuditLogQueue.Object,
            Mock.Of<ILogger<AppearanceSettingsService>>());
    }

    [Fact]
    public async Task SaveThemeAsync_WhenThemeExistsAndSetSucceeds_ReturnsSuccessAndAuditLogs()
    {
        // Arrange
        var theme = new ThemeDto { Id = 2, DisplayName = "Light" };
        _mockThemeService.Setup(t => t.GetThemeByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(theme);
        _mockThemeService.Setup(t => t.GetDefaultThemeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThemeDto { Id = 1, DisplayName = "Discord Dark" });
        _mockThemeService.Setup(t => t.SetDefaultThemeAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _service.SaveThemeAsync(2, "user-1");

        // Assert
        result.Success.Should().BeTrue();
        result.ThemeName.Should().Be("Light");
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Once);
    }

    [Fact]
    public async Task SaveThemeAsync_WhenThemeDoesNotExist_ReturnsFailureWithoutCallingSetDefault()
    {
        // Arrange
        _mockThemeService.Setup(t => t.GetThemeByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((ThemeDto?)null);

        // Act
        var result = await _service.SaveThemeAsync(99, "user-1");

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        _mockThemeService.Verify(t => t.SetDefaultThemeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Never);
    }

    [Fact]
    public async Task IsSuperAdminAsync_ReflectsAuthorizationResult()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(user, null, "RequireSuperAdmin"))
            .ReturnsAsync(AuthorizationResult.Success());

        // Act
        var isSuperAdmin = await _service.IsSuperAdminAsync(user);

        // Assert
        isSuperAdmin.Should().BeTrue();
    }
}
