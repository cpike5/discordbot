using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Settings;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Settings;

/// <summary>
/// Unit tests for <see cref="SettingsSectionService"/>.
/// </summary>
public class SettingsSectionServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService = new();
    private readonly Mock<ICommandModuleConfigurationService> _mockCommandModuleConfigurationService = new();
    private readonly Mock<IAuditLogQueue> _mockAuditLogQueue = new();
    private readonly SettingsSectionService _service;

    public SettingsSectionServiceTests()
    {
        _service = new SettingsSectionService(
            _mockSettingsService.Object,
            _mockCommandModuleConfigurationService.Object,
            _mockAuditLogQueue.Object,
            Mock.Of<ILogger<SettingsSectionService>>());
    }

    [Fact]
    public async Task SaveCategoryAsync_WhenUpdateSucceedsWithChanges_ReturnsSuccessAndEnqueuesAuditLog()
    {
        // Arrange
        var updateResult = new SettingsUpdateResultDto
        {
            Success = true,
            RestartRequired = true,
            UpdatedKeys = new List<string> { "General:Foo" },
            Changes = new Dictionary<string, SettingChange>
            {
                ["General:Foo"] = new SettingChange { OldValue = "old", NewValue = "new", DisplayName = "Foo" }
            }
        };
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        // Act
        var result = await _service.SaveCategoryAsync("General", new Dictionary<string, string> { ["General:Foo"] = "new" }, "user-1");

        // Assert
        result.Success.Should().BeTrue();
        result.RestartRequired.Should().BeTrue();
        result.Message.Should().Contain("1 setting(s) updated");
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.Is<AuditLogCreateDto>(d => d.Category == AuditLogCategory.Configuration)), Times.Once);
    }

    [Fact]
    public async Task SaveCategoryAsync_WhenUpdateFails_ReturnsFailureWithoutAuditLog()
    {
        // Arrange
        var updateResult = new SettingsUpdateResultDto
        {
            Success = false,
            Errors = new List<string> { "Invalid value" }
        };
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        // Act
        var result = await _service.SaveCategoryAsync("General", new Dictionary<string, string>(), "user-1");

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().ContainSingle().Which.Should().Be("Invalid value");
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Never);
    }

    [Fact]
    public async Task ResetCategoryAsync_WithInvalidCategory_ReturnsFailureWithoutCallingSettingsService()
    {
        // Act
        var result = await _service.ResetCategoryAsync("NotARealCategory", "user-1");

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        _mockSettingsService.Verify(
            s => s.ResetCategoryAsync(It.IsAny<SettingCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveCommandModulesAsync_WhenUpdateSucceeds_EnqueuesOneAuditLogPerUpdatedModule()
    {
        // Arrange
        _mockCommandModuleConfigurationService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleConfigurationDto>
            {
                new() { ModuleName = "Tts", IsEnabled = true }
            });
        _mockCommandModuleConfigurationService
            .Setup(s => s.UpdateModulesAsync(It.IsAny<CommandModuleConfigurationUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandModuleUpdateResultDto
            {
                Success = true,
                RequiresRestart = false,
                UpdatedModules = new List<string> { "Tts" }
            });

        // Act
        var result = await _service.SaveCommandModulesAsync(new Dictionary<string, bool> { ["Tts"] = false }, "user-1");

        // Assert
        result.Success.Should().BeTrue();
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Once);
    }
}
