using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for TtsSettingsService.
/// Tests cover settings retrieval, updates, and rate limiting.
/// </summary>
public class TtsSettingsServiceTests
{
    private readonly Mock<IGuildTtsSettingsRepository> _mockSettingsRepository;
    private readonly Mock<ITtsMessageRepository> _mockMessageRepository;
    private readonly Mock<ILogger<TtsSettingsService>> _mockLogger;
    private readonly TtsSettingsService _service;

    public TtsSettingsServiceTests()
    {
        _mockSettingsRepository = new Mock<IGuildTtsSettingsRepository>();
        _mockMessageRepository = new Mock<ITtsMessageRepository>();
        _mockLogger = new Mock<ILogger<TtsSettingsService>>();

        _service = new TtsSettingsService(
            _mockSettingsRepository.Object,
            _mockMessageRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static GuildTtsSettings CreateTestSettings(
        ulong guildId = 123456789UL,
        bool ttsEnabled = true,
        int rateLimitPerMinute = 5,
        int maxMessageLength = 500)
    {
        return new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = ttsEnabled,
            DefaultVoice = "default",
            DefaultSpeed = 1.0,
            DefaultPitch = 1.0,
            DefaultVolume = 0.8,
            MaxMessageLength = maxMessageLength,
            RateLimitPerMinute = rateLimitPerMinute,
            AutoPlayOnSend = false,
            AnnounceJoinsLeaves = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region GetOrCreateSettingsAsync Tests

    [Fact]
    public async Task GetOrCreateSettingsAsync_ReturnsSettingsViaRepository()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetOrCreateSettingsAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guildId);
        result.TtsEnabled.Should().BeTrue();
        result.MaxMessageLength.Should().Be(500);

        _mockSettingsRepository.Verify(
            r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSettingsAsync_WithDisabledTts_ReturnsDisabledSettings()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId, ttsEnabled: false);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetOrCreateSettingsAsync(guildId);

        // Assert
        result.TtsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateSettingsAsync_ReturnsValidDefaultVoice()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            DefaultVoice = "en-US-JennyNeural", // Valid default voice
            DefaultSpeed = 1.0,
            DefaultPitch = 1.0,
            DefaultVolume = 0.8,
            MaxMessageLength = 500,
            RateLimitPerMinute = 5,
            AutoPlayOnSend = false,
            AnnounceJoinsLeaves = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetOrCreateSettingsAsync(guildId);

        // Assert
        result.DefaultVoice.Should().NotBeNull();
        result.DefaultVoice.Should().NotBeEmpty();
        result.DefaultVoice.Should().Be("en-US-JennyNeural");
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesSettingsAndSaves()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildTtsSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        settings.TtsEnabled = false;
        settings.MaxMessageLength = 1000;
        await _service.UpdateSettingsAsync(settings);

        // Assert
        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.Is<GuildTtsSettings>(s =>
                s.GuildId == guildId &&
                s.TtsEnabled == false &&
                s.MaxMessageLength == 1000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesTimestamp()
    {
        // Arrange
        var settings = CreateTestSettings();
        var originalUpdatedAt = settings.UpdatedAt;

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildTtsSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _service.UpdateSettingsAsync(settings);

        var afterUpdate = DateTime.UtcNow;

        // Assert
        settings.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        settings.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
    }

    #endregion

    #region IsTtsEnabledAsync Tests

    [Fact]
    public async Task IsTtsEnabledAsync_WhenSettingsExistAndEnabled_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId, ttsEnabled: true);

        _mockSettingsRepository.Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsTtsEnabledAsync(guildId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTtsEnabledAsync_WhenSettingsExistAndDisabled_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId, ttsEnabled: false);

        _mockSettingsRepository.Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsTtsEnabledAsync(guildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTtsEnabledAsync_WhenNoSettingsExist_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockSettingsRepository.Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildTtsSettings?)null);

        // Act
        var result = await _service.IsTtsEnabledAsync(guildId);

        // Assert
        result.Should().BeTrue(); // Default is enabled
    }

    #endregion

    #region IsUserRateLimitedAsync Tests

    [Fact]
    public async Task IsUserRateLimitedAsync_WhenUnderLimit_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong userId = 987654321UL;
        var settings = CreateTestSettings(guildId, rateLimitPerMinute: 5);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockMessageRepository.Setup(r => r.GetUserMessageCountAsync(
                guildId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // Under the limit of 5

        // Act
        var result = await _service.IsUserRateLimitedAsync(guildId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserRateLimitedAsync_WhenAtLimit_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong userId = 987654321UL;
        var settings = CreateTestSettings(guildId, rateLimitPerMinute: 5);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockMessageRepository.Setup(r => r.GetUserMessageCountAsync(
                guildId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // At the limit

        // Act
        var result = await _service.IsUserRateLimitedAsync(guildId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserRateLimitedAsync_WhenOverLimit_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong userId = 987654321UL;
        var settings = CreateTestSettings(guildId, rateLimitPerMinute: 5);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockMessageRepository.Setup(r => r.GetUserMessageCountAsync(
                guildId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10); // Over the limit

        // Act
        var result = await _service.IsUserRateLimitedAsync(guildId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserRateLimitedAsync_ChecksCorrectTimeWindow()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong userId = 987654321UL;
        var settings = CreateTestSettings(guildId);
        DateTime capturedSince = DateTime.MinValue;

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockMessageRepository.Setup(r => r.GetUserMessageCountAsync(
                guildId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, ulong, DateTime, CancellationToken>((_, _, since, _) => capturedSince = since)
            .ReturnsAsync(0);

        var beforeCall = DateTime.UtcNow.AddMinutes(-1);

        // Act
        await _service.IsUserRateLimitedAsync(guildId, userId);

        var afterCall = DateTime.UtcNow.AddMinutes(-1);

        // Assert - Should check the last minute
        capturedSince.Should().BeOnOrAfter(beforeCall);
        capturedSince.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public async Task IsUserRateLimitedAsync_WithZeroLimit_AlwaysRateLimited()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong userId = 987654321UL;
        var settings = CreateTestSettings(guildId, rateLimitPerMinute: 0);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockMessageRepository.Setup(r => r.GetUserMessageCountAsync(
                guildId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.IsUserRateLimitedAsync(guildId, userId);

        // Assert
        result.Should().BeTrue(); // 0 >= 0 is true
    }

    #endregion
}
