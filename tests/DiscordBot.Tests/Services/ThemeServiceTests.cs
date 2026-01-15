using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ThemeService"/>.
/// Tests cover theme retrieval, user preferences, and default theme handling.
/// </summary>
public class ThemeServiceTests : IDisposable
{
    private readonly Mock<IThemeRepository> _mockThemeRepository;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<ThemeService>> _mockLogger;
    private readonly BotDbContext _dbContext;
    private readonly ThemeService _service;
    private readonly SqliteConnection _connection;

    // Test themes
    private readonly Theme _discordDarkTheme;
    private readonly Theme _purpleDuskTheme;

    public ThemeServiceTests()
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new BotDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Initialize test themes
        _discordDarkTheme = new Theme
        {
            Id = 1,
            ThemeKey = "discord-dark",
            DisplayName = "Discord Dark",
            Description = "Default dark theme",
            ColorDefinition = "{\"bgPrimary\":\"#1d2022\"}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _purpleDuskTheme = new Theme
        {
            Id = 2,
            ThemeKey = "purple-dusk",
            DisplayName = "Purple Dusk",
            Description = "Light purple theme",
            ColorDefinition = "{\"bgPrimary\":\"#E8E3DF\"}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Setup mocks
        _mockThemeRepository = new Mock<IThemeRepository>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<ThemeService>>();

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        // Default repository setup
        _mockThemeRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Theme> { _discordDarkTheme, _purpleDuskTheme });

        _mockThemeRepository
            .Setup(r => r.GetByKeyAsync("discord-dark", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_discordDarkTheme);

        _mockThemeRepository
            .Setup(r => r.GetByKeyAsync("purple-dusk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_purpleDuskTheme);

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_discordDarkTheme);

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_purpleDuskTheme);

        // Default settings service returns null (no configured default)
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        _service = new ThemeService(
            _mockThemeRepository.Object,
            _mockSettingsService.Object,
            _mockUserManager.Object,
            _dbContext,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetActiveThemesAsync Tests

    [Fact]
    public async Task GetActiveThemesAsync_ReturnsAllActiveThemes()
    {
        // Act
        var result = await _service.GetActiveThemesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.ThemeKey == "discord-dark");
        result.Should().Contain(t => t.ThemeKey == "purple-dusk");
    }

    [Fact]
    public async Task GetActiveThemesAsync_ReturnsCorrectDtoProperties()
    {
        // Act
        var result = await _service.GetActiveThemesAsync();

        // Assert
        var discordDark = result.First(t => t.ThemeKey == "discord-dark");
        discordDark.Id.Should().Be(1);
        discordDark.DisplayName.Should().Be("Discord Dark");
        discordDark.Description.Should().Be("Default dark theme");
        discordDark.ColorDefinition.Should().Contain("bgPrimary");
        discordDark.IsActive.Should().BeTrue();
    }

    #endregion

    #region GetThemeByKeyAsync Tests

    [Fact]
    public async Task GetThemeByKeyAsync_ReturnsTheme_WhenExists()
    {
        // Act
        var result = await _service.GetThemeByKeyAsync("discord-dark");

        // Assert
        result.Should().NotBeNull();
        result!.ThemeKey.Should().Be("discord-dark");
        result.DisplayName.Should().Be("Discord Dark");
    }

    [Fact]
    public async Task GetThemeByKeyAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _mockThemeRepository
            .Setup(r => r.GetByKeyAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var result = await _service.GetThemeByKeyAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUserThemeAsync Tests

    [Fact]
    public async Task GetUserThemeAsync_ReturnsUserPreference_WhenSet()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user123",
            Email = "test@example.com",
            PreferredThemeId = 2,
            PreferredTheme = _purpleDuskTheme
        };

        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserThemeAsync("user123");

        // Assert
        result.Should().NotBeNull();
        result.Theme.ThemeKey.Should().Be("purple-dusk");
        result.Source.Should().Be(ThemeSource.User);
    }

    [Fact]
    public async Task GetUserThemeAsync_ReturnsSystemDefault_WhenNoUserPreference()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user456",
            Email = "test2@example.com",
            PreferredThemeId = null,
            PreferredTheme = null
        };

        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserThemeAsync("user456");

        // Assert
        result.Should().NotBeNull();
        result.Theme.ThemeKey.Should().Be("discord-dark");
        result.Source.Should().Be(ThemeSource.System);
    }

    [Fact]
    public async Task GetUserThemeAsync_ReturnsSystemDefault_WhenUserNotFound()
    {
        // Act
        var result = await _service.GetUserThemeAsync("nonexistent-user");

        // Assert
        result.Should().NotBeNull();
        result.Theme.ThemeKey.Should().Be("discord-dark");
        result.Source.Should().Be(ThemeSource.System);
    }

    [Fact]
    public async Task GetUserThemeAsync_FallsBackToDefault_WhenUserThemeInactive()
    {
        // Arrange
        var inactiveTheme = new Theme
        {
            Id = 3,
            ThemeKey = "inactive-theme",
            DisplayName = "Inactive Theme",
            IsActive = false,
            ColorDefinition = "{}"
        };

        var user = new ApplicationUser
        {
            Id = "user789",
            Email = "test3@example.com",
            PreferredThemeId = 3,
            PreferredTheme = inactiveTheme
        };

        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserThemeAsync("user789");

        // Assert
        result.Should().NotBeNull();
        result.Theme.ThemeKey.Should().Be("discord-dark");
        result.Source.Should().Be(ThemeSource.System);
    }

    #endregion

    #region GetDefaultThemeAsync Tests

    [Fact]
    public async Task GetDefaultThemeAsync_ReturnsConfiguredDefault_WhenSet()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<int?>("Appearance:DefaultThemeId", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _service.GetDefaultThemeAsync();

        // Assert
        result.Should().NotBeNull();
        result.ThemeKey.Should().Be("purple-dusk");
    }

    [Fact]
    public async Task GetDefaultThemeAsync_ReturnsDiscordDark_WhenNoConfiguredDefault()
    {
        // Act
        var result = await _service.GetDefaultThemeAsync();

        // Assert
        result.Should().NotBeNull();
        result.ThemeKey.Should().Be("discord-dark");
    }

    [Fact]
    public async Task GetDefaultThemeAsync_FallsBackToDiscordDark_WhenConfiguredThemeNotFound()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<int?>("Appearance:DefaultThemeId", It.IsAny<CancellationToken>()))
            .ReturnsAsync(999); // Non-existent theme ID

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var result = await _service.GetDefaultThemeAsync();

        // Assert
        result.Should().NotBeNull();
        result.ThemeKey.Should().Be("discord-dark");
    }

    #endregion

    #region SetUserThemeAsync Tests

    [Fact]
    public async Task SetUserThemeAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-set-1",
            Email = "settest@example.com",
            PreferredThemeId = null
        };

        _mockUserManager
            .Setup(m => m.FindByIdAsync("user-set-1"))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.SetUserThemeAsync("user-set-1", 2);

        // Assert
        result.Should().BeTrue();
        user.PreferredThemeId.Should().Be(2);
    }

    [Fact]
    public async Task SetUserThemeAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        _mockUserManager
            .Setup(m => m.FindByIdAsync("nonexistent"))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.SetUserThemeAsync("nonexistent", 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetUserThemeAsync_ReturnsFalse_WhenThemeNotFound()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-set-2",
            Email = "settest2@example.com"
        };

        _mockUserManager
            .Setup(m => m.FindByIdAsync("user-set-2"))
            .ReturnsAsync(user);

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var result = await _service.SetUserThemeAsync("user-set-2", 999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetUserThemeAsync_ReturnsFalse_WhenThemeInactive()
    {
        // Arrange
        var inactiveTheme = new Theme
        {
            Id = 3,
            ThemeKey = "inactive-theme",
            IsActive = false
        };

        var user = new ApplicationUser
        {
            Id = "user-set-3",
            Email = "settest3@example.com"
        };

        _mockUserManager
            .Setup(m => m.FindByIdAsync("user-set-3"))
            .ReturnsAsync(user);

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveTheme);

        // Act
        var result = await _service.SetUserThemeAsync("user-set-3", 3);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetUserThemeAsync_ClearsPreference_WhenThemeIdNull()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-clear-1",
            Email = "cleartest@example.com",
            PreferredThemeId = 2
        };

        _mockUserManager
            .Setup(m => m.FindByIdAsync("user-clear-1"))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.SetUserThemeAsync("user-clear-1", null);

        // Assert
        result.Should().BeTrue();
        user.PreferredThemeId.Should().BeNull();
    }

    #endregion

    #region SetDefaultThemeAsync Tests

    [Fact]
    public async Task SetDefaultThemeAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(
                It.IsAny<SettingsUpdateDto>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsUpdateResultDto { Success = true });

        // Act
        var result = await _service.SetDefaultThemeAsync(2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultThemeAsync_ReturnsFalse_WhenThemeNotFound()
    {
        // Arrange
        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Theme?)null);

        // Act
        var result = await _service.SetDefaultThemeAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultThemeAsync_ReturnsFalse_WhenThemeInactive()
    {
        // Arrange
        var inactiveTheme = new Theme
        {
            Id = 3,
            ThemeKey = "inactive-theme",
            IsActive = false
        };

        _mockThemeRepository
            .Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveTheme);

        // Act
        var result = await _service.SetDefaultThemeAsync(3);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultThemeAsync_ReturnsFalse_WhenSettingsUpdateFails()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(
                It.IsAny<SettingsUpdateDto>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsUpdateResultDto
            {
                Success = false,
                Errors = new List<string> { "Update failed" }
            });

        // Act
        var result = await _service.SetDefaultThemeAsync(1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
