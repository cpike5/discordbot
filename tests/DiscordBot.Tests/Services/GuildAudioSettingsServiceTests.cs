using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for GuildAudioSettingsService.
/// Tests cover settings retrieval, updates, and role-based command restrictions.
/// </summary>
public class GuildAudioSettingsServiceTests
{
    private readonly Mock<IGuildAudioSettingsRepository> _mockSettingsRepository;
    private readonly Mock<ILogger<GuildAudioSettingsService>> _mockLogger;
    private readonly GuildAudioSettingsService _service;

    public GuildAudioSettingsServiceTests()
    {
        _mockSettingsRepository = new Mock<IGuildAudioSettingsRepository>();
        _mockLogger = new Mock<ILogger<GuildAudioSettingsService>>();

        _service = new GuildAudioSettingsService(
            _mockSettingsRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

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
            AutoLeaveTimeoutMinutes = 5,
            QueueEnabled = true,
            MaxDurationSeconds = 30,
            MaxFileSizeBytes = 5_242_880,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CommandRoleRestrictions = new()
        };
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsViaRepository()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetSettingsAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guildId);
        result.AudioEnabled.Should().BeTrue();
        result.MaxSoundsPerGuild.Should().Be(50);

        _mockSettingsRepository.Verify(
            r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_WithDisabledAudio_ReturnsDisabledSettings()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId, audioEnabled: false);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetSettingsAsync(guildId);

        // Assert
        result.AudioEnabled.Should().BeFalse();
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesSettingsAndSaves()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(guildId, s =>
        {
            s.AudioEnabled = false;
            s.MaxSoundsPerGuild = 100;
        });

        // Assert
        result.AudioEnabled.Should().BeFalse();
        result.MaxSoundsPerGuild.Should().Be(100);

        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.Is<GuildAudioSettings>(s =>
                s.GuildId == guildId &&
                s.AudioEnabled == false &&
                s.MaxSoundsPerGuild == 100),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesTimestamp()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);
        var originalUpdatedAt = settings.UpdatedAt;

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var beforeUpdate = DateTime.UtcNow;

        // Act
        var result = await _service.UpdateSettingsAsync(guildId, s =>
        {
            s.QueueEnabled = false;
        });

        var afterUpdate = DateTime.UtcNow;

        // Assert
        result.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        result.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
        result.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region AddCommandRestrictionAsync Tests

    [Fact]
    public async Task AddCommandRestrictionAsync_CreatesNewRestrictionWhenNotExists()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.AddCommandRestrictionAsync(guildId, "play", roleId);

        // Assert
        settings.CommandRoleRestrictions.Should().ContainSingle(r =>
            r.CommandName == "play" && r.AllowedRoleIds.Contains(roleId));

        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddCommandRestrictionAsync_AddsRoleToExistingRestriction()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong existingRoleId = 111111UL;
        ulong newRoleId = 222222UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { existingRoleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.AddCommandRestrictionAsync(guildId, "play", newRoleId);

        // Assert
        var playRestriction = settings.CommandRoleRestrictions.First(r => r.CommandName == "play");
        playRestriction.AllowedRoleIds.Should().Contain(existingRoleId);
        playRestriction.AllowedRoleIds.Should().Contain(newRoleId);
        playRestriction.AllowedRoleIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddCommandRestrictionAsync_IsIdempotent()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { roleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Add the same role twice
        await _service.AddCommandRestrictionAsync(guildId, "play", roleId);
        await _service.AddCommandRestrictionAsync(guildId, "play", roleId);

        // Assert - Should still have only one instance of the role
        var playRestriction = settings.CommandRoleRestrictions.First(r => r.CommandName == "play");
        playRestriction.AllowedRoleIds.Should().HaveCount(1);
        playRestriction.AllowedRoleIds.Should().Contain(roleId);
    }

    [Fact]
    public async Task AddCommandRestrictionAsync_CaseInsensitiveCommandName()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Add with different cases
        await _service.AddCommandRestrictionAsync(guildId, "PLAY", roleId);
        await _service.AddCommandRestrictionAsync(guildId, "play", roleId);

        // Assert - Should only have one restriction
        settings.CommandRoleRestrictions.Should().HaveCount(1);
    }

    #endregion

    #region RemoveCommandRestrictionAsync Tests

    [Fact]
    public async Task RemoveCommandRestrictionAsync_RemovesRoleFromRestriction()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { roleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RemoveCommandRestrictionAsync(guildId, "play", roleId);

        // Assert
        var playRestriction = settings.CommandRoleRestrictions.First(r => r.CommandName == "play");
        playRestriction.AllowedRoleIds.Should().NotContain(roleId);

        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveCommandRestrictionAsync_WhenRestrictionDoesNotExist_DoesNothing()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.RemoveCommandRestrictionAsync(guildId, "nonexistent", roleId);

        // Assert
        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveCommandRestrictionAsync_WhenRoleNotInRestriction_DoesNothing()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong existingRoleId = 111111UL;
        ulong roleToRemove = 222222UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { existingRoleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        await _service.RemoveCommandRestrictionAsync(guildId, "play", roleToRemove);

        // Assert
        var playRestriction = settings.CommandRoleRestrictions.First(r => r.CommandName == "play");
        playRestriction.AllowedRoleIds.Should().Contain(existingRoleId);
        playRestriction.AllowedRoleIds.Should().NotContain(roleToRemove);

        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveCommandRestrictionAsync_CaseInsensitiveCommandName()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { roleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository.Setup(r => r.UpdateAsync(It.IsAny<GuildAudioSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Remove with different case
        await _service.RemoveCommandRestrictionAsync(guildId, "PLAY", roleId);

        // Assert
        var playRestriction = settings.CommandRoleRestrictions.First(r => r.CommandName == "play");
        playRestriction.AllowedRoleIds.Should().NotContain(roleId);
    }

    #endregion

    #region GetAllowedRolesForCommandAsync Tests

    [Fact]
    public async Task GetAllowedRolesForCommandAsync_ReturnsRolesForCommand()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var roleIds = new List<ulong> { 111111UL, 222222UL, 333333UL };

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = roleIds.ToList()
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetAllowedRolesForCommandAsync(guildId, "play");

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(roleIds);
    }

    [Fact]
    public async Task GetAllowedRolesForCommandAsync_WhenNoRestriction_ReturnsEmpty()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetAllowedRolesForCommandAsync(guildId, "nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllowedRolesForCommandAsync_CaseInsensitiveCommandName()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var roleIds = new List<ulong> { 111111UL };

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = roleIds.ToList()
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act - Query with different case
        var result = await _service.GetAllowedRolesForCommandAsync(guildId, "PLAY");

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(111111UL);
    }

    #endregion

    #region IsCommandAllowedForRoleAsync Tests

    [Fact]
    public async Task IsCommandAllowedForRoleAsync_WhenNoRestriction_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsCommandAllowedForRoleAsync(guildId, "play", roleId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCommandAllowedForRoleAsync_WhenRoleInAllowedList_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { roleId, 222222UL }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsCommandAllowedForRoleAsync(guildId, "play", roleId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCommandAllowedForRoleAsync_WhenRoleNotInAllowedList_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;
        ulong otherRoleId = 222222UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { otherRoleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsCommandAllowedForRoleAsync(guildId, "play", roleId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCommandAllowedForRoleAsync_WhenRestrictionEmptyAllowedRoles_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() // Empty list
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.IsCommandAllowedForRoleAsync(guildId, "play", roleId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCommandAllowedForRoleAsync_CaseInsensitiveCommandName()
    {
        // Arrange
        ulong guildId = 123456789UL;
        ulong roleId = 111111UL;

        var restriction = new CommandRoleRestriction
        {
            GuildId = guildId,
            CommandName = "play",
            AllowedRoleIds = new() { roleId }
        };

        var settings = CreateTestSettings(guildId);
        settings.CommandRoleRestrictions.Add(restriction);

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act - Query with different case
        var result = await _service.IsCommandAllowedForRoleAsync(guildId, "PLAY", roleId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
