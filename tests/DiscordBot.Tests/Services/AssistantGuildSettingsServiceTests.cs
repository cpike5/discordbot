using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AssistantGuildSettingsService"/>.
/// Tests cover settings creation, enable/disable, channel restrictions, and rate limiting.
/// </summary>
public class AssistantGuildSettingsServiceTests
{
    private readonly Mock<ILogger<AssistantGuildSettingsService>> _mockLogger;
    private readonly Mock<IAssistantGuildSettingsRepository> _mockRepository;
    private readonly AssistantOptions _assistantOptions;
    private readonly AssistantGuildSettingsService _service;

    private const ulong TestGuildId = 123456789UL;
    private const ulong TestChannelId1 = 987654321UL;
    private const ulong TestChannelId2 = 111222333UL;
    private const int DefaultRateLimit = 5;
    private const int GuildRateLimit = 10;

    public AssistantGuildSettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<AssistantGuildSettingsService>>();
        _mockRepository = new Mock<IAssistantGuildSettingsRepository>();

        _assistantOptions = new AssistantOptions
        {
            GloballyEnabled = true,
            EnabledByDefaultForNewGuilds = false,
            DefaultRateLimit = DefaultRateLimit
        };

        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_assistantOptions);

        _service = new AssistantGuildSettingsService(
            _mockLogger.Object,
            _mockRepository.Object,
            mockOptions.Object);
    }

    #region GetOrCreateSettingsAsync Tests

    [Fact]
    public async Task GetOrCreateSettingsAsync_ReturnsExistingSettings_WhenFound()
    {
        // Arrange
        var existingSettings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSettings);

        // Act
        var result = await _service.GetOrCreateSettingsAsync(TestGuildId);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(TestGuildId);
        result.IsEnabled.Should().BeTrue();
        result.RateLimitOverride.Should().BeNull();

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateSettingsAsync_CreatesNewSettings_WhenNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings settings, CancellationToken _) => settings);

        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await _service.GetOrCreateSettingsAsync(TestGuildId);

        var afterCall = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(TestGuildId);
        result.IsEnabled.Should().BeFalse(); // Should use EnabledByDefaultForNewGuilds which is false
        result.AllowedChannelIds.Should().Be("[]");
        result.RateLimitOverride.Should().BeNull();
        result.CreatedAt.Should().BeOnOrAfter(beforeCall).And.BeOnOrBefore(afterCall);
        result.UpdatedAt.Should().BeOnOrAfter(beforeCall).And.BeOnOrBefore(afterCall);

        _mockRepository.Verify(
            r => r.AddAsync(
                It.Is<AssistantGuildSettings>(s =>
                    s.GuildId == TestGuildId &&
                    s.IsEnabled == false &&
                    s.AllowedChannelIds == "[]" &&
                    s.RateLimitOverride == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSettingsAsync_UsesEnabledByDefaultForNewGuilds_WhenCreating()
    {
        // Arrange
        _assistantOptions.EnabledByDefaultForNewGuilds = true;
        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_assistantOptions);

        var service = new AssistantGuildSettingsService(
            _mockLogger.Object,
            _mockRepository.Object,
            mockOptions.Object);

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings settings, CancellationToken _) => settings);

        // Act
        var result = await service.GetOrCreateSettingsAsync(TestGuildId);

        // Assert
        result.IsEnabled.Should().BeTrue();

        _mockRepository.Verify(
            r => r.AddAsync(
                It.Is<AssistantGuildSettings>(s => s.IsEnabled == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSettingsAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, cts.Token))
            .ReturnsAsync((AssistantGuildSettings?)null);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), cts.Token))
            .ReturnsAsync((AssistantGuildSettings settings, CancellationToken _) => settings);

        // Act
        await _service.GetOrCreateSettingsAsync(TestGuildId, cts.Token);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(TestGuildId, cts.Token),
            Times.Once);
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesTimestampAndCallsRepository()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = GuildRateLimit,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _service.UpdateSettingsAsync(settings);

        var afterUpdate = DateTime.UtcNow;

        // Assert
        settings.UpdatedAt.Should().BeOnOrAfter(beforeUpdate).And.BeOnOrBefore(afterUpdate);

        _mockRepository.Verify(
            r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PreservesOtherProperties()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[987654321]",
            RateLimitOverride = GuildRateLimit,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        await _service.UpdateSettingsAsync(settings);

        // Assert
        settings.GuildId.Should().Be(TestGuildId);
        settings.IsEnabled.Should().BeTrue();
        settings.AllowedChannelIds.Should().Be("[987654321]");
        settings.RateLimitOverride.Should().Be(GuildRateLimit);
        settings.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _service.UpdateSettingsAsync(settings, cts.Token);

        // Assert
        _mockRepository.Verify(
            r => r.UpdateAsync(settings, cts.Token),
            Times.Once);
    }

    #endregion

    #region EnableAsync Tests

    [Fact]
    public async Task EnableAsync_EnablesDisabledSettings()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.EnableAsync(TestGuildId);

        // Assert
        settings.IsEnabled.Should().BeTrue();

        _mockRepository.Verify(
            r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnableAsync_CreatesDefaultSettingsIfNotFound()
    {
        // Arrange
        // Note: EnabledByDefaultForNewGuilds is false, so new settings will have IsEnabled = false
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null)
            .Verifiable(Times.Once);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings settings, CancellationToken _) => settings)
            .Verifiable(Times.Once);

        // Act
        await _service.EnableAsync(TestGuildId);

        // Assert - GetOrCreateSettingsAsync creates with IsEnabled=false, then EnableAsync sets to true and updates
        _mockRepository.Verify();
        _mockRepository.Verify(
            r => r.UpdateAsync(
                It.Is<AssistantGuildSettings>(s => s.IsEnabled == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnableAsync_DoesNotUpdateIfAlreadyEnabled()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.EnableAsync(TestGuildId);

        // Assert
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnableAsync_UpdatesTimestamp()
    {
        // Arrange
        var oldTimestamp = DateTime.UtcNow.AddHours(-1);
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = oldTimestamp
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var beforeEnable = DateTime.UtcNow;

        // Act
        await _service.EnableAsync(TestGuildId);

        var afterEnable = DateTime.UtcNow;

        // Assert
        settings.UpdatedAt.Should().BeOnOrAfter(beforeEnable).And.BeOnOrBefore(afterEnable);
        settings.UpdatedAt.Should().BeAfter(oldTimestamp);
    }

    #endregion

    #region DisableAsync Tests

    [Fact]
    public async Task DisableAsync_DisablesEnabledSettings()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.DisableAsync(TestGuildId);

        // Assert
        settings.IsEnabled.Should().BeFalse();

        _mockRepository.Verify(
            r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisableAsync_DoesNothingIfSettingsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        await _service.DisableAsync(TestGuildId);

        // Assert
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableAsync_DoesNothingIfAlreadyDisabled()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.DisableAsync(TestGuildId);

        // Assert
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableAsync_UpdatesTimestamp()
    {
        // Arrange
        var oldTimestamp = DateTime.UtcNow.AddHours(-1);
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = oldTimestamp
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var beforeDisable = DateTime.UtcNow;

        // Act
        await _service.DisableAsync(TestGuildId);

        var afterDisable = DateTime.UtcNow;

        // Assert
        settings.UpdatedAt.Should().BeOnOrAfter(beforeDisable).And.BeOnOrBefore(afterDisable);
        settings.UpdatedAt.Should().BeAfter(oldTimestamp);
    }

    #endregion

    #region IsEnabledAsync Tests

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalse_WhenGloballyDisabled()
    {
        // Arrange
        _assistantOptions.GloballyEnabled = false;
        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_assistantOptions);

        var service = new AssistantGuildSettingsService(
            _mockLogger.Object,
            _mockRepository.Object,
            mockOptions.Object);

        // Act
        var result = await service.IsEnabledAsync(TestGuildId);

        // Assert
        result.Should().BeFalse();

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not query repository when globally disabled");
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalse_WhenSettingsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        var result = await _service.IsEnabledAsync(TestGuildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalse_WhenGuildDisabled()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsEnabledAsync(TestGuildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsTrue_WhenGloballyAndGuildEnabled()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsEnabledAsync(TestGuildId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsChannelAllowedAsync Tests

    [Fact]
    public async Task IsChannelAllowedAsync_ReturnsFalse_WhenSettingsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        var result = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelAllowedAsync_ReturnsTrue_WhenNoChannelRestrictions()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]", // Empty list means all channels allowed
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelAllowedAsync_ReturnsTrue_WhenChannelInAllowedList()
    {
        // Arrange
        var allowedChannels = new List<ulong> { TestChannelId1, TestChannelId2 };
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[987654321, 111222333]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelAllowedAsync_ReturnsFalse_WhenChannelNotInAllowedList()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[987654321]", // Only TestChannelId1 is allowed
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelAllowedAsync_HandlesInvalidJson_ReturnsTrue()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "invalid json", // GetAllowedChannelIdsList will return empty list
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1);

        // Assert
        result.Should().BeTrue(); // Empty allowed list means all channels allowed
    }

    [Fact]
    public async Task IsChannelAllowedAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, cts.Token))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1, cts.Token);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(TestGuildId, cts.Token),
            Times.Once);
    }

    #endregion

    #region GetRateLimitAsync Tests

    [Fact]
    public async Task GetRateLimitAsync_ReturnsDefault_WhenNoOverride()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = null, // No override
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetRateLimitAsync(TestGuildId);

        // Assert
        result.Should().Be(DefaultRateLimit);
    }

    [Fact]
    public async Task GetRateLimitAsync_ReturnsOverride_WhenSet()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = GuildRateLimit,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetRateLimitAsync(TestGuildId);

        // Assert
        result.Should().Be(GuildRateLimit);
    }

    [Fact]
    public async Task GetRateLimitAsync_ReturnsDefault_WhenSettingsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        var result = await _service.GetRateLimitAsync(TestGuildId);

        // Assert
        result.Should().Be(DefaultRateLimit);
    }

    [Fact]
    public async Task GetRateLimitAsync_ReturnsZeroOverride_WhenExplicitlySet()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = true,
            AllowedChannelIds = "[]",
            RateLimitOverride = 0, // Explicit zero limit
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetRateLimitAsync(TestGuildId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetRateLimitAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, cts.Token))
            .ReturnsAsync((AssistantGuildSettings?)null);

        // Act
        await _service.GetRateLimitAsync(TestGuildId, cts.Token);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(TestGuildId, cts.Token),
            Times.Once);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_assistantOptions);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new AssistantGuildSettingsService(null!, _mockRepository.Object, mockOptions.Object));
        ex.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        // Arrange
        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_assistantOptions);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new AssistantGuildSettingsService(_mockLogger.Object, null!, mockOptions.Object));
        ex.ParamName.Should().Be("repository");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new AssistantGuildSettingsService(_mockLogger.Object, _mockRepository.Object, null!));
        ex.ParamName.Should().Be("assistantOptions");
    }

    #endregion

    #region Integration Scenario Tests

    [Fact]
    public async Task FullWorkflow_CreateEnableAndConfigureGuildSettings()
    {
        // Arrange - use a captured settings object for the entire workflow
        AssistantGuildSettings? capturedSettings = null;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedSettings);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantGuildSettings settings, CancellationToken _) =>
            {
                capturedSettings = settings;
                return settings;
            });

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Step 1: Create settings
        var createdSettings = await _service.GetOrCreateSettingsAsync(TestGuildId);
        createdSettings.IsEnabled.Should().BeFalse();

        // Act - Step 2: Enable settings
        await _service.EnableAsync(TestGuildId);
        createdSettings.IsEnabled.Should().BeTrue();

        // Act - Step 3: Check if enabled
        var isEnabled = await _service.IsEnabledAsync(TestGuildId);
        isEnabled.Should().BeTrue();

        // Act - Step 4: Check channel allowed (no restrictions)
        var isChannelAllowed = await _service.IsChannelAllowedAsync(TestGuildId, TestChannelId1);
        isChannelAllowed.Should().BeTrue();

        // Act - Step 5: Get rate limit
        var rateLimit = await _service.GetRateLimitAsync(TestGuildId);
        rateLimit.Should().Be(DefaultRateLimit);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAfterEnable_RestoresDisabledState()
    {
        // Arrange
        var settings = new AssistantGuildSettings
        {
            GuildId = TestGuildId,
            IsEnabled = false,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .SetupSequence(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .ReturnsAsync(new AssistantGuildSettings
            {
                GuildId = TestGuildId,
                IsEnabled = true,
                AllowedChannelIds = "[]",
                RateLimitOverride = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ReturnsAsync(new AssistantGuildSettings
            {
                GuildId = TestGuildId,
                IsEnabled = true,
                AllowedChannelIds = "[]",
                RateLimitOverride = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        // Act
        var initialState = await _service.IsEnabledAsync(TestGuildId);
        await _service.EnableAsync(TestGuildId);
        var enabledState = await _service.IsEnabledAsync(TestGuildId);

        // Prepare for disable
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantGuildSettings
            {
                GuildId = TestGuildId,
                IsEnabled = true,
                AllowedChannelIds = "[]",
                RateLimitOverride = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await _service.DisableAsync(TestGuildId);

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantGuildSettings
            {
                GuildId = TestGuildId,
                IsEnabled = false,
                AllowedChannelIds = "[]",
                RateLimitOverride = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var finalState = await _service.IsEnabledAsync(TestGuildId);

        // Assert
        initialState.Should().BeFalse();
        enabledState.Should().BeTrue();
        finalState.Should().BeFalse();
    }

    #endregion
}
