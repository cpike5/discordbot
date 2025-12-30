using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SettingsService"/>.
/// Tests cover settings retrieval, updates, validation, and restart flag management.
/// </summary>
public class SettingsServiceTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISettingsRepository> _mockRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _mockRepository = new Mock<ISettingsRepository>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SettingsService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();

        // Setup the scope factory to return a scope that provides the repository
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISettingsRepository)))
            .Returns(_mockRepository.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);

        // Default setup for GetAllAsync - returns empty list (tests can override if needed)
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApplicationSetting>());

        _service = new SettingsService(
            _mockScopeFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    #region GetSettingsByCategoryAsync Tests

    [Fact]
    public async Task GetSettingsByCategoryAsync_ReturnsAllSettingsForCategory()
    {
        // Arrange
        var category = SettingCategory.General;
        var dbSettings = new List<ApplicationSetting>();

        _mockRepository
            .Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        // Act
        var result = await _service.GetSettingsByCategoryAsync(category);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0, "the General category has defined settings");
        result.Should().OnlyContain(s => s.Category == category, "all returned settings should be from the requested category");

        _mockRepository.Verify(
            r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be called once");
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_ReturnsDatabaseValues_WhenTheyExist()
    {
        // Arrange
        var category = SettingCategory.General;
        var dbSettings = new List<ApplicationSetting>
        {
            new()
            {
                Key = "General:DefaultTimezone",
                Value = "America/New_York",
                Category = category,
                DataType = SettingDataType.String,
                RequiresRestart = false
            }
        };

        _mockRepository
            .Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        // Act
        var result = await _service.GetSettingsByCategoryAsync(category);

        // Assert
        var timezoneSetting = result.FirstOrDefault(s => s.Key == "General:DefaultTimezone");
        timezoneSetting.Should().NotBeNull();
        timezoneSetting!.Value.Should().Be("America/New_York", "database value should be returned");
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_FallsBackToConfigValue_WhenNotInDatabase()
    {
        // Arrange
        var category = SettingCategory.General;
        var dbSettings = new List<ApplicationSetting>();

        _mockRepository
            .Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        _mockConfiguration
            .Setup(c => c["General:DefaultTimezone"])
            .Returns("Europe/London");

        // Act
        var result = await _service.GetSettingsByCategoryAsync(category);

        // Assert
        var timezoneSetting = result.FirstOrDefault(s => s.Key == "General:DefaultTimezone");
        timezoneSetting.Should().NotBeNull();
        timezoneSetting!.Value.Should().Be("Europe/London", "configuration value should be used when not in database");
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_FallsBackToDefaultValue_WhenNotInDatabaseOrConfig()
    {
        // Arrange
        var category = SettingCategory.General;
        var dbSettings = new List<ApplicationSetting>();

        _mockRepository
            .Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        _mockConfiguration
            .Setup(c => c["General:DefaultTimezone"])
            .Returns((string?)null);

        // Act
        var result = await _service.GetSettingsByCategoryAsync(category);

        // Assert
        var timezoneSetting = result.FirstOrDefault(s => s.Key == "General:DefaultTimezone");
        timezoneSetting.Should().NotBeNull();
        timezoneSetting!.Value.Should().Be("UTC", "definition default value should be used as final fallback");
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_PassesCancellationToken()
    {
        // Arrange
        var category = SettingCategory.General;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApplicationSetting>());

        // Act
        await _service.GetSettingsByCategoryAsync(category, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByCategoryAsync(category, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region GetAllSettingsAsync Tests

    [Fact]
    public async Task GetAllSettingsAsync_ReturnsAllSettings()
    {
        // Arrange
        var dbSettings = new List<ApplicationSetting>();

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        // Act
        var result = await _service.GetAllSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0, "there are defined settings across all categories");
        result.Should().Contain(s => s.Category == SettingCategory.General);
        // Note: Logging category was removed as part of Settings Overhaul (#399) - logging settings now managed via appsettings.json
        result.Should().Contain(s => s.Category == SettingCategory.Features);
        result.Should().Contain(s => s.Category == SettingCategory.Advanced);
    }

    [Fact]
    public async Task GetAllSettingsAsync_MergesDatabaseAndConfigValues()
    {
        // Arrange
        var dbSettings = new List<ApplicationSetting>
        {
            new()
            {
                Key = "General:BotEnabled",
                Value = "false",
                Category = SettingCategory.General,
                DataType = SettingDataType.Boolean,
                RequiresRestart = false
            }
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);

        _mockConfiguration
            .Setup(c => c["Features:MessageLoggingEnabled"])
            .Returns("false");

        // Act
        var result = await _service.GetAllSettingsAsync();

        // Assert
        var botEnabledSetting = result.FirstOrDefault(s => s.Key == "General:BotEnabled");
        botEnabledSetting.Should().NotBeNull();
        botEnabledSetting!.Value.Should().Be("false", "database value should be used");

        var messageLoggingSetting = result.FirstOrDefault(s => s.Key == "Features:MessageLoggingEnabled");
        messageLoggingSetting.Should().NotBeNull();
        messageLoggingSetting!.Value.Should().Be("false", "configuration value should be used when not in database");
    }

    [Fact]
    public async Task GetAllSettingsAsync_PassesCancellationToken()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApplicationSetting>());

        // Act
        await _service.GetAllSettingsAsync(cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region GetSettingValueAsync<T> Tests

    [Fact]
    public async Task GetSettingValueAsync_ReturnsDatabaseValue_WhenExists()
    {
        // Arrange
        const string key = "General:BotEnabled";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "false",
            Category = SettingCategory.General,
            DataType = SettingDataType.Boolean
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<bool>(key);

        // Assert
        result.Should().BeFalse("database value should be returned");
    }

    [Fact]
    public async Task GetSettingValueAsync_FallsBackToConfiguration_WhenNotInDatabase()
    {
        // Arrange
        const string key = "General:BotEnabled";

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationSetting?)null);

        _mockConfiguration
            .Setup(c => c[key])
            .Returns("true");

        // Act
        var result = await _service.GetSettingValueAsync<bool>(key);

        // Assert
        result.Should().BeTrue("configuration value should be returned when not in database");
    }

    [Fact]
    public async Task GetSettingValueAsync_ReturnsDefault_WhenNeitherExists()
    {
        // Arrange
        const string key = "NonExistent:Key";

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationSetting?)null);

        _mockConfiguration
            .Setup(c => c[key])
            .Returns((string?)null);

        // Act
        var result = await _service.GetSettingValueAsync<string>(key);

        // Assert
        result.Should().BeNull("default value should be returned when setting doesn't exist");
    }

    [Fact]
    public async Task GetSettingValueAsync_ConvertsStringCorrectly()
    {
        // Arrange
        const string key = "General:StatusMessage";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "Custom Status",
            Category = SettingCategory.General,
            DataType = SettingDataType.String
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<string>(key);

        // Assert
        result.Should().Be("Custom Status", "string should be returned as-is");
    }

    [Fact]
    public async Task GetSettingValueAsync_ConvertsIntegerCorrectly()
    {
        // Arrange
        const string key = "Logging:RetainedFileCountLimit";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "30",
            Category = SettingCategory.Logging,
            DataType = SettingDataType.Integer
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<int>(key);

        // Assert
        result.Should().Be(30, "integer should be parsed correctly");
    }

    [Fact]
    public async Task GetSettingValueAsync_ConvertsBooleanCorrectly()
    {
        // Arrange
        const string key = "Advanced:DebugMode";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "true",
            Category = SettingCategory.Advanced,
            DataType = SettingDataType.Boolean
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<bool>(key);

        // Assert
        result.Should().BeTrue("boolean should be parsed correctly");
    }

    [Fact]
    public async Task GetSettingValueAsync_ConvertsDecimalCorrectly()
    {
        // Arrange
        const string key = "Discord:DefaultRateLimitPeriodSeconds";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "120.5",
            Category = SettingCategory.Features,
            DataType = SettingDataType.Decimal
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<decimal>(key);

        // Assert
        result.Should().Be(120.5m, "decimal should be parsed correctly");
    }

    [Fact]
    public async Task GetSettingValueAsync_ReturnsDefault_OnInvalidConversion()
    {
        // Arrange
        const string key = "TestKey";
        var dbSetting = new ApplicationSetting
        {
            Key = key,
            Value = "not-a-number",
            Category = SettingCategory.General,
            DataType = SettingDataType.Integer
        };

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSetting);

        // Act
        var result = await _service.GetSettingValueAsync<int>(key);

        // Assert
        result.Should().Be(0, "default value should be returned when conversion fails");
    }

    [Fact]
    public async Task GetSettingValueAsync_PassesCancellationToken()
    {
        // Arrange
        const string key = "TestKey";
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationSetting?)null);

        // Act
        await _service.GetSettingValueAsync<string>(key, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByKeyAsync(key, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Fact]
    public async Task UpdateSettingsAsync_SuccessfullyUpdatesValidSettings()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "false" },
                { "General:DefaultTimezone", "America/New_York" }
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("all settings are valid");
        result.Errors.Should().BeEmpty("there should be no errors");
        result.UpdatedKeys.Should().HaveCount(2, "two settings were updated");
        result.UpdatedKeys.Should().Contain("General:BotEnabled");
        result.UpdatedKeys.Should().Contain("General:DefaultTimezone");
        result.RestartRequired.Should().BeFalse("neither setting requires restart");

        _mockRepository.Verify(
            r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "repository should be called for each setting");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ReturnsErrors_ForUnknownKeys()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Unknown:Key", "value" },
                { "Another:Unknown:Key", "value" }
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("there are unknown keys");
        result.Errors.Should().HaveCount(2, "two settings have unknown keys");
        result.Errors.Should().Contain(e => e.Contains("Unknown setting key: Unknown:Key"));
        result.Errors.Should().Contain(e => e.Contains("Unknown setting key: Another:Unknown:Key"));
        result.UpdatedKeys.Should().BeEmpty("no settings were updated");

        _mockRepository.Verify(
            r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository should not be called for unknown keys");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesIntegerValues()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "not-a-number" }
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("value is not a valid integer");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("must be a valid integer");
        result.UpdatedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesMaximumValue()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "400" } // max is 365
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("value exceeds maximum");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("at most 365");
        result.UpdatedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesBooleanValues()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "yes" } // must be "true" or "false"
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("value is not a valid boolean");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("must be 'true' or 'false'");
        result.UpdatedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesAllowedValues()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:DefaultTimezone", "InvalidTimezone" }
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("value is not in allowed values list");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("must be one of:");
        result.UpdatedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSettingsAsync_DoesNotRequireRestart_ForRuntimeSettings()
    {
        // Arrange - All current settings are runtime-modifiable and don't require restart
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Features:MessageLoggingEnabled", "false" }
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeFalse("all current settings are runtime-modifiable");
        _service.IsRestartPending.Should().BeFalse("no restart pending for runtime settings");
    }

    [Fact]
    public async Task UpdateSettingsAsync_DoesNotSetRestartPending_WhenNoRestartRequiredSettingsUpdated()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "false" } // does not require restart
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeFalse("setting does not require restart");
        _service.IsRestartPending.Should().BeFalse("restart pending flag should not be set");
    }

    [Fact]
    public async Task UpdateSettingsAsync_PartialSuccess_UpdatesValidSettingsAndReturnsErrors()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "false" }, // valid (different from default "true")
                { "Unknown:Key", "value" }, // invalid - unknown key
                { "General:DefaultTimezone", "America/New_York" } // valid
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("there is an error");
        result.Errors.Should().HaveCount(1);
        result.UpdatedKeys.Should().HaveCount(2, "two valid settings were updated");
        result.UpdatedKeys.Should().Contain("General:BotEnabled");
        result.UpdatedKeys.Should().Contain("General:DefaultTimezone");
    }

    [Fact]
    public async Task UpdateSettingsAsync_SetsLastModifiedFields()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "false" }
            }
        };

        ApplicationSetting? capturedSetting = null;
        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationSetting, CancellationToken>((s, _) => capturedSetting = s)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        capturedSetting.Should().NotBeNull();
        capturedSetting!.LastModifiedBy.Should().Be(userId, "user ID should be set");
        capturedSetting.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5), "timestamp should be recent");
    }

    [Fact]
    public async Task UpdateSettingsAsync_PassesCancellationToken()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:BotEnabled", "false" }
            }
        };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateSettingsAsync(updates, userId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region ResetCategoryAsync Tests

    [Fact]
    public async Task ResetCategoryAsync_DeletesAllSettingsInCategory()
    {
        // Arrange
        const string userId = "user123";
        var category = SettingCategory.General;

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResetCategoryAsync(category, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.UpdatedKeys.Should().NotBeEmpty("category has settings");

        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository delete should be called once");
    }

    [Fact]
    public async Task ResetCategoryAsync_DoesNotRequireRestart_ForFeaturesCategory()
    {
        // Arrange - All current settings are runtime-modifiable
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";
        var category = SettingCategory.Features;

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResetCategoryAsync(category, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeFalse("Features category has no restart-required settings");
        _service.IsRestartPending.Should().BeFalse("restart pending flag should not be set");
    }

    [Fact]
    public async Task ResetCategoryAsync_DoesNotSetRestartPending_WhenCategoryHasNoRestartRequiredSettings()
    {
        // Arrange
        const string userId = "user123";
        var category = SettingCategory.General; // no settings require restart

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResetCategoryAsync(category, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeFalse("General category has no restart-required settings");
        _service.IsRestartPending.Should().BeFalse("restart pending flag should not be set");
    }

    [Fact]
    public async Task ResetCategoryAsync_ReturnsError_OnRepositoryException()
    {
        // Arrange
        const string userId = "user123";
        var category = SettingCategory.General;

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _service.ResetCategoryAsync(category, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Failed to reset category");
        result.Errors[0].Should().Contain("Database error");
    }

    [Fact]
    public async Task ResetCategoryAsync_PassesCancellationToken()
    {
        // Arrange
        const string userId = "user123";
        var category = SettingCategory.General;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ResetCategoryAsync(category, userId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(category, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region ResetAllAsync Tests

    [Fact]
    public async Task ResetAllAsync_DeletesAllSettings()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResetAllAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.UpdatedKeys.Should().NotBeEmpty("there are defined settings");

        // Verify delete was called for each category
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(SettingCategory.General, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(SettingCategory.Logging, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(SettingCategory.Features, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(SettingCategory.Advanced, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAllAsync_DoesNotRequireRestart_WhenNoRestartRequiredSettingsExist()
    {
        // Arrange - All current settings are runtime-modifiable
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResetAllAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeFalse("no settings require restart");
        _service.IsRestartPending.Should().BeFalse("restart pending flag should not be set");
    }

    [Fact]
    public async Task ResetAllAsync_ReturnsError_OnRepositoryException()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _service.ResetAllAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Failed to reset all settings");
        result.Errors[0].Should().Contain("Database error");
    }

    [Fact]
    public async Task ResetAllAsync_PassesCancellationToken()
    {
        // Arrange
        const string userId = "user123";
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ResetAllAsync(userId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), cancellationToken),
            Times.AtLeastOnce,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region IsRestartPending and ClearRestartPending Tests

    [Fact]
    public void IsRestartPending_InitiallyFalse()
    {
        // Assert
        _service.IsRestartPending.Should().BeFalse("restart pending flag should be false initially");
    }

    [Fact]
    public async Task IsRestartPending_RemainsFalse_AfterUpdatingRuntimeSettings()
    {
        // Arrange - All current settings are runtime-modifiable
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "30" }
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        _service.IsRestartPending.Should().BeFalse("all current settings are runtime-modifiable");
    }

    [Fact]
    public async Task IsRestartPending_RemainsFalse_AfterCategoryResetWithNoRestartRequiredSettings()
    {
        // Arrange - All current settings are runtime-modifiable
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(SettingCategory.Features, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ResetCategoryAsync(SettingCategory.Features, userId);

        // Assert
        _service.IsRestartPending.Should().BeFalse("category has no restart required settings");
    }

    [Fact]
    public async Task IsRestartPending_RemainsFalse_AfterResetAll()
    {
        // Arrange - All current settings are runtime-modifiable
        // Note: Settings Overhaul (#399) removed all restart-required settings
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.DeleteByCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ResetAllAsync(userId);

        // Assert
        _service.IsRestartPending.Should().BeFalse("no settings require restart");
    }

    [Fact]
    public void ClearRestartPending_ClearsTheFlag()
    {
        // Arrange - ClearRestartPending can be called even if no restart is pending
        // This tests the idempotent behavior of the method

        // Act
        _service.ClearRestartPending();

        // Assert
        _service.IsRestartPending.Should().BeFalse("flag should be cleared");
    }

    [Fact]
    public void ClearRestartPending_CanBeCalledMultipleTimes()
    {
        // Act
        _service.ClearRestartPending();
        _service.ClearRestartPending();
        _service.ClearRestartPending();

        // Assert
        _service.IsRestartPending.Should().BeFalse("calling multiple times should not cause errors");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesMinimumValue()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "0" } // min is 1
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("at least 1");
    }

    [Fact]
    public async Task UpdateSettingsAsync_AcceptsValueAtMinimum()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "1" } // min is 1
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("value is at minimum");
    }

    [Fact]
    public async Task UpdateSettingsAsync_AcceptsValueAtMaximum()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:MessageLogRetentionDays", "365" } // max is 365
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("value is at maximum");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidatesIntegerRange()
    {
        // Arrange - Test that integer settings validate against max bounds
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "Advanced:AuditLogRetentionDays", "500" } // max is 365
            }
        };

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("at most 365");
    }

    [Fact]
    public async Task UpdateSettingsAsync_AcceptsValidAllowedValue()
    {
        // Arrange
        const string userId = "user123";
        var updates = new SettingsUpdateDto
        {
            Settings = new Dictionary<string, string>
            {
                { "General:DefaultTimezone", "America/New_York" }
            }
        };

        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<ApplicationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(updates, userId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("value is in allowed values list");
    }

    #endregion
}
