using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for SoundService.
/// Tests cover CRUD operations, validation, and usage tracking.
/// </summary>
public class SoundServiceTests
{
    private readonly Mock<ISoundRepository> _mockSoundRepository;
    private readonly Mock<IGuildAudioSettingsRepository> _mockSettingsRepository;
    private readonly Mock<ISoundPlayLogRepository> _mockPlayLogRepository;
    private readonly Mock<ILogger<SoundService>> _mockLogger;
    private readonly IOptions<SoundboardOptions> _options;
    private readonly SoundService _service;

    public SoundServiceTests()
    {
        _mockSoundRepository = new Mock<ISoundRepository>();
        _mockSettingsRepository = new Mock<IGuildAudioSettingsRepository>();
        _mockPlayLogRepository = new Mock<ISoundPlayLogRepository>();
        _mockLogger = new Mock<ILogger<SoundService>>();
        _options = Options.Create(new SoundboardOptions
        {
            BasePath = "sounds",
            DefaultMaxDurationSeconds = 30,
            DefaultMaxFileSizeBytes = 5_242_880,
            DefaultMaxSoundsPerGuild = 50,
            DefaultMaxStorageBytes = 104_857_600
        });

        _service = new SoundService(
            _mockSoundRepository.Object,
            _mockSettingsRepository.Object,
            _mockPlayLogRepository.Object,
            _mockLogger.Object,
            _options);
    }

    #region Helper Methods

    private static Sound CreateTestSound(Guid? id = null, ulong guildId = 123456789UL, string name = "test-sound")
    {
        return new Sound
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            Name = name,
            FileName = $"{name}.mp3",
            FileSizeBytes = 1000,
            DurationSeconds = 5.0,
            UploadedById = 111111UL,
            UploadedAt = DateTime.UtcNow,
            PlayCount = 0
        };
    }

    private static GuildAudioSettings CreateTestSettings(
        ulong guildId = 123456789UL,
        bool audioEnabled = true,
        int maxSounds = 50,
        long maxStorageBytes = 104_857_600)
    {
        return new GuildAudioSettings
        {
            GuildId = guildId,
            AudioEnabled = audioEnabled,
            MaxSoundsPerGuild = maxSounds,
            MaxStorageBytes = maxStorageBytes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenSoundExists_ReturnsSound()
    {
        // Arrange
        var sound = CreateTestSound();
        _mockSoundRepository.Setup(r => r.GetByIdAndGuildAsync(sound.Id, sound.GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sound);

        // Act
        var result = await _service.GetByIdAsync(sound.Id, sound.GuildId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(sound.Id);
        result.Name.Should().Be(sound.Name);
        result.GuildId.Should().Be(sound.GuildId);

        _mockSoundRepository.Verify(
            r => r.GetByIdAndGuildAsync(sound.Id, sound.GuildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSoundNotFound_ReturnsNull()
    {
        // Arrange
        var soundId = Guid.NewGuid();
        ulong guildId = 123456789UL;

        _mockSoundRepository.Setup(r => r.GetByIdAndGuildAsync(soundId, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        // Act
        var result = await _service.GetByIdAsync(soundId, guildId);

        // Assert
        result.Should().BeNull();

        _mockSoundRepository.Verify(
            r => r.GetByIdAndGuildAsync(soundId, guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetAllByGuildAsync Tests

    [Fact]
    public async Task GetAllByGuildAsync_ReturnsAllSoundsForGuild()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var sounds = new List<Sound>
        {
            CreateTestSound(name: "sound1"),
            CreateTestSound(name: "sound2"),
            CreateTestSound(name: "sound3")
        };

        _mockSoundRepository.Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sounds.AsReadOnly());

        // Act
        var result = await _service.GetAllByGuildAsync(guildId);

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainEquivalentOf(sounds[0]);
        result.Should().ContainEquivalentOf(sounds[1]);
        result.Should().ContainEquivalentOf(sounds[2]);

        _mockSoundRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllByGuildAsync_WhenNoSounds_ReturnsEmpty()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockSoundRepository.Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Sound>().AsReadOnly());

        // Act
        var result = await _service.GetAllByGuildAsync(guildId);

        // Assert
        result.Should().HaveCount(0);
    }

    #endregion

    #region GetByNameAsync Tests

    [Fact]
    public async Task GetByNameAsync_WhenSoundExists_ReturnsSoundByCaseInsensitiveName()
    {
        // Arrange
        var sound = CreateTestSound(name: "MySound");
        ulong guildId = sound.GuildId;

        _mockSoundRepository.Setup(r => r.GetByNameAndGuildAsync("mysound", guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sound);

        // Act
        var result = await _service.GetByNameAsync("mysound", guildId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("MySound");

        _mockSoundRepository.Verify(
            r => r.GetByNameAndGuildAsync("mysound", guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByNameAsync_WhenSoundNotFound_ReturnsNull()
    {
        // Arrange
        ulong guildId = 123456789UL;

        _mockSoundRepository.Setup(r => r.GetByNameAndGuildAsync("nonexistent", guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        // Act
        var result = await _service.GetByNameAsync("nonexistent", guildId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateSoundAsync Tests

    [Fact]
    public async Task CreateSoundAsync_WithValidData_CreatesSound()
    {
        // Arrange
        var sound = CreateTestSound();
        ulong guildId = sound.GuildId;

        _mockSoundRepository.Setup(r => r.GetByNameAndGuildAsync(sound.Name, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        _mockSoundRepository.Setup(r => r.AddAsync(It.IsAny<Sound>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound s, CancellationToken _) => s);

        // Act
        var result = await _service.CreateSoundAsync(sound);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(sound.Id);
        result.Name.Should().Be(sound.Name);
        result.GuildId.Should().Be(guildId);
        result.PlayCount.Should().Be(0);

        _mockSoundRepository.Verify(
            r => r.GetByNameAndGuildAsync(sound.Name, guildId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSoundRepository.Verify(
            r => r.AddAsync(It.Is<Sound>(s =>
                s.Id == sound.Id &&
                s.Name == sound.Name &&
                s.PlayCount == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSoundAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var newSound = CreateTestSound(name: "duplicate");
        var existingSound = CreateTestSound(name: "duplicate");
        ulong guildId = newSound.GuildId;

        _mockSoundRepository.Setup(r => r.GetByNameAndGuildAsync(newSound.Name, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSound);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateSoundAsync(newSound));

        _mockSoundRepository.Verify(
            r => r.AddAsync(It.IsAny<Sound>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not attempt to add sound when duplicate name exists");
    }

    [Fact]
    public async Task CreateSoundAsync_SetsUploadedAtToUtcNow()
    {
        // Arrange
        var sound = CreateTestSound();
        sound.UploadedAt = default; // Reset to default
        ulong guildId = sound.GuildId;

        _mockSoundRepository.Setup(r => r.GetByNameAndGuildAsync(sound.Name, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        _mockSoundRepository.Setup(r => r.AddAsync(It.IsAny<Sound>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound s, CancellationToken _) => s);

        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await _service.CreateSoundAsync(sound);

        var afterCall = DateTime.UtcNow;

        // Assert
        result.UploadedAt.Should().BeOnOrAfter(beforeCall);
        result.UploadedAt.Should().BeOnOrBefore(afterCall);
    }

    #endregion

    #region DeleteSoundAsync Tests

    [Fact]
    public async Task DeleteSoundAsync_WhenSoundExists_DeletesSound()
    {
        // Arrange
        var sound = CreateTestSound();
        var soundId = sound.Id;
        var guildId = sound.GuildId;

        _mockSoundRepository.Setup(r => r.GetByIdAndGuildAsync(soundId, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sound);

        _mockSoundRepository.Setup(r => r.DeleteAsync(It.IsAny<Sound>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteSoundAsync(soundId, guildId);

        // Assert
        result.Should().BeTrue();

        _mockSoundRepository.Verify(
            r => r.DeleteAsync(It.Is<Sound>(s => s.Id == soundId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteSoundAsync_WhenSoundNotFound_ReturnsFalse()
    {
        // Arrange
        var soundId = Guid.NewGuid();
        var guildId = 123456789UL;

        _mockSoundRepository.Setup(r => r.GetByIdAndGuildAsync(soundId, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        // Act
        var result = await _service.DeleteSoundAsync(soundId, guildId);

        // Assert
        result.Should().BeFalse();

        _mockSoundRepository.Verify(
            r => r.DeleteAsync(It.IsAny<Sound>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region IncrementPlayCountAsync Tests

    [Fact]
    public async Task IncrementPlayCountAsync_IncrementsPlayCount()
    {
        // Arrange
        var soundId = Guid.NewGuid();

        _mockSoundRepository.Setup(r => r.IncrementPlayCountAsync(soundId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.IncrementPlayCountAsync(soundId);

        // Assert
        _mockSoundRepository.Verify(
            r => r.IncrementPlayCountAsync(soundId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ValidateStorageLimitAsync Tests

    [Fact]
    public async Task ValidateStorageLimitAsync_WhenWithinLimit_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        long currentUsage = 10_000_000;
        long additionalBytes = 5_000_000;
        long maxStorageBytes = 104_857_600;

        var settings = CreateTestSettings(guildId: guildId, maxStorageBytes: maxStorageBytes);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetTotalStorageUsedAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUsage);

        // Act
        var result = await _service.ValidateStorageLimitAsync(guildId, additionalBytes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateStorageLimitAsync_WhenExceedsLimit_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        long currentUsage = 100_000_000;
        long additionalBytes = 10_000_000;
        long maxStorageBytes = 104_857_600;

        var settings = CreateTestSettings(guildId: guildId, maxStorageBytes: maxStorageBytes);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetTotalStorageUsedAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUsage);

        // Act
        var result = await _service.ValidateStorageLimitAsync(guildId, additionalBytes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateStorageLimitAsync_WhenExactlyAtLimit_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        long currentUsage = 104_857_600;
        long additionalBytes = 1;
        long maxStorageBytes = 104_857_600;

        var settings = CreateTestSettings(guildId: guildId, maxStorageBytes: maxStorageBytes);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetTotalStorageUsedAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUsage);

        // Act
        var result = await _service.ValidateStorageLimitAsync(guildId, additionalBytes);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ValidateSoundCountLimitAsync Tests

    [Fact]
    public async Task ValidateSoundCountLimitAsync_WhenUnderLimit_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        int currentCount = 40;
        int maxSounds = 50;

        var settings = CreateTestSettings(guildId: guildId, maxSounds: maxSounds);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetSoundCountAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        // Act
        var result = await _service.ValidateSoundCountLimitAsync(guildId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSoundCountLimitAsync_WhenAtLimit_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        int currentCount = 50;
        int maxSounds = 50;

        var settings = CreateTestSettings(guildId: guildId, maxSounds: maxSounds);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetSoundCountAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        // Act
        var result = await _service.ValidateSoundCountLimitAsync(guildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSoundCountLimitAsync_WhenOverLimit_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        int currentCount = 55;
        int maxSounds = 50;

        var settings = CreateTestSettings(guildId: guildId, maxSounds: maxSounds);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSoundRepository.Setup(r => r.GetSoundCountAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        // Act
        var result = await _service.ValidateSoundCountLimitAsync(guildId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetStorageUsedAsync Tests

    [Fact]
    public async Task GetStorageUsedAsync_ReturnsStorageUsedForGuild()
    {
        // Arrange
        ulong guildId = 123456789UL;
        long storageUsed = 50_000_000;

        _mockSoundRepository.Setup(r => r.GetTotalStorageUsedAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageUsed);

        // Act
        var result = await _service.GetStorageUsedAsync(guildId);

        // Assert
        result.Should().Be(storageUsed);
    }

    #endregion

    #region GetSoundCountAsync Tests

    [Fact]
    public async Task GetSoundCountAsync_ReturnsSoundCountForGuild()
    {
        // Arrange
        ulong guildId = 123456789UL;
        int soundCount = 25;

        _mockSoundRepository.Setup(r => r.GetSoundCountAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(soundCount);

        // Act
        var result = await _service.GetSoundCountAsync(guildId);

        // Assert
        result.Should().Be(soundCount);
    }

    #endregion
}
