using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Settings;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Bot.Services.Settings;

/// <summary>
/// Unit tests for <see cref="SettingsSectionService"/>.
/// </summary>
public class SettingsSectionServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService = new();
    private readonly Mock<ICommandModuleConfigurationService> _mockCommandModuleConfigurationService = new();
    private readonly Mock<IAuditLogQueue> _mockAuditLogQueue = new();
    private readonly Mock<ILlmModelRepository> _mockLlmModelRepository = new();
    private readonly Mock<ILlmModelResolver> _mockLlmModelResolver = new();
    private readonly SettingsSectionService _service;

    public SettingsSectionServiceTests()
    {
        _mockLlmModelRepository
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmModel>());

        _mockLlmModelResolver
            .Setup(r => r.ResolveAsync(It.IsAny<LlmMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMode mode, CancellationToken _) => new LlmResolvedModel
            {
                Slug = "anthropic/claude-sonnet-4",
                Source = LlmModelResolutionSource.Configuration,
                ConfiguredSlug = "anthropic/claude-sonnet-4"
            });

        _service = new SettingsSectionService(
            _mockSettingsService.Object,
            _mockCommandModuleConfigurationService.Object,
            _mockAuditLogQueue.Object,
            _mockLlmModelRepository.Object,
            _mockLlmModelResolver.Object,
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

    #region AI Models - LoadViewModelAsync

    [Fact]
    public async Task LoadViewModelAsync_PopulatesAiModelsSettings_WithEnabledSlugsAsAllowedValues()
    {
        SetupEmptyCoreCategories();
        _mockSettingsService
            .Setup(s => s.GetSettingsByCategoryAsync(SettingCategory.AiModels, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SettingDto>
            {
                new()
                {
                    Key = "Assistant:Sampling:Model",
                    Value = "anthropic/claude-sonnet-4",
                    Category = SettingCategory.AiModels,
                    DataType = SettingDataType.String,
                    DisplayName = "Guild assistant model"
                }
            });
        _mockLlmModelRepository
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmModel>
            {
                new() { Id = "openai/gpt-5" },
                new() { Id = "anthropic/claude-sonnet-4" }
            });

        var viewModel = await _service.LoadViewModelAsync("AiModels");

        var setting = viewModel.AiModelsSettings.Single(s => s.Key == "Assistant:Sampling:Model");
        setting.AllowedValues.Should().BeEquivalentTo(new[] { "", "anthropic/claude-sonnet-4", "openai/gpt-5" });
        setting.AllowedValues!.First().Should().Be("", "the \"use configured value\" option must be first");
    }

    [Fact]
    public async Task LoadViewModelAsync_IncludesCurrentValue_WhenNotAmongEnabledSlugs()
    {
        SetupEmptyCoreCategories();
        _mockSettingsService
            .Setup(s => s.GetSettingsByCategoryAsync(SettingCategory.AiModels, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SettingDto>
            {
                new()
                {
                    Key = "DmAssistant:Model",
                    Value = "anthropic/claude-opus-4",
                    Category = SettingCategory.AiModels,
                    DataType = SettingDataType.String,
                    DisplayName = "DM assistant model"
                }
            });
        _mockLlmModelRepository
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmModel> { new() { Id = "anthropic/claude-sonnet-4" } });

        var viewModel = await _service.LoadViewModelAsync("AiModels");

        var setting = viewModel.AiModelsSettings.Single(s => s.Key == "DmAssistant:Model");
        setting.AllowedValues.Should().Contain("anthropic/claude-opus-4", "the current value must remain visible in the select even when disabled");
        setting.AllowedValues.Should().Contain("anthropic/claude-sonnet-4");
    }

    private void SetupEmptyCoreCategories()
    {
        _mockSettingsService
            .Setup(s => s.GetSettingsByCategoryAsync(SettingCategory.General, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SettingDto>());
        _mockSettingsService
            .Setup(s => s.GetSettingsByCategoryAsync(SettingCategory.Features, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SettingDto>());
        _mockSettingsService
            .Setup(s => s.GetSettingsByCategoryAsync(SettingCategory.Advanced, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SettingDto>());
        _mockCommandModuleConfigurationService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleConfigurationDto>());
    }

    #endregion

    #region AI Models - save-time validation

    [Fact]
    public async Task SaveCategoryAsync_RejectsAiModelSelection_WhenSlugIsNotInCatalog()
    {
        _mockLlmModelRepository
            .Setup(r => r.GetByIdAsync("openai/unknown-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var result = await _service.SaveCategoryAsync(
            "AiModels",
            new Dictionary<string, string> { ["Assistant:Sampling:Model"] = "openai/unknown-model" },
            "user-1");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("openai/unknown-model") && e.Contains("not in the model catalog"));
        _mockSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveCategoryAsync_RejectsAiModelSelection_WhenModelIsDisabled()
    {
        _mockLlmModelRepository
            .Setup(r => r.GetByIdAsync("anthropic/claude-haiku-4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel { Id = "anthropic/claude-haiku-4", IsEnabled = false, SupportsTools = true });

        var result = await _service.SaveCategoryAsync(
            "AiModels",
            new Dictionary<string, string> { ["DmAssistant:Model"] = "anthropic/claude-haiku-4" },
            "user-1");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("not enabled"));
        _mockSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveCategoryAsync_RejectsAiModelSelection_WhenModelDoesNotSupportTools()
    {
        _mockLlmModelRepository
            .Setup(r => r.GetByIdAsync("some/non-tool-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel { Id = "some/non-tool-model", IsEnabled = true, SupportsTools = false });

        var result = await _service.SaveCategoryAsync(
            "AiModels",
            new Dictionary<string, string> { ["FeatureRequests:RequirementsGatheringModel"] = "some/non-tool-model" },
            "user-1");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("does not support tool calling"));
        _mockSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveCategoryAsync_AcceptsAiModelSelection_WhenModelIsEnabledAndToolCapable()
    {
        _mockLlmModelRepository
            .Setup(r => r.GetByIdAsync("anthropic/claude-sonnet-4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel { Id = "anthropic/claude-sonnet-4", IsEnabled = true, SupportsTools = true });

        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsUpdateResultDto
            {
                Success = true,
                UpdatedKeys = new List<string> { "Assistant:Sampling:Model" },
                Changes = new Dictionary<string, SettingChange>
                {
                    ["Assistant:Sampling:Model"] = new SettingChange { OldValue = "old", NewValue = "anthropic/claude-sonnet-4", DisplayName = "Guild assistant model" }
                }
            });

        var result = await _service.SaveCategoryAsync(
            "AiModels",
            new Dictionary<string, string> { ["Assistant:Sampling:Model"] = "anthropic/claude-sonnet-4" },
            "user-1");

        result.Success.Should().BeTrue();
        _mockSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveCategoryAsync_AcceptsBlankAiModelSelection_AsUseConfiguredValue()
    {
        // "" means "use the configured value" (SettingDefinitions' AiModels entries default to
        // it), so it must reach UpdateSettingsAsync without the catalog validation rejecting it -
        // this is how ResetCategoryAsync("AiModels") behaves once the setting rows are deleted and
        // the definition default (now "") is read back.
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsUpdateResultDto
            {
                Success = true,
                UpdatedKeys = new List<string> { "Assistant:Sampling:Model" },
                Changes = new Dictionary<string, SettingChange>
                {
                    ["Assistant:Sampling:Model"] = new SettingChange { OldValue = "anthropic/claude-sonnet-4", NewValue = "", DisplayName = "Guild assistant model" }
                }
            });

        var result = await _service.SaveCategoryAsync(
            "AiModels",
            new Dictionary<string, string> { ["Assistant:Sampling:Model"] = "" },
            "user-1");

        result.Success.Should().BeTrue();
        _mockLlmModelRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never, "a blank slug means \"use configured value\" and is never validated against the catalog");
        _mockSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveCategoryAsync_SkipsAiModelValidation_ForNonModeKeys()
    {
        _mockSettingsService
            .Setup(s => s.UpdateSettingsAsync(It.IsAny<SettingsUpdateDto>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsUpdateResultDto { Success = true, UpdatedKeys = new List<string> { "General:Foo" } });

        var result = await _service.SaveCategoryAsync(
            "General",
            new Dictionary<string, string> { ["General:Foo"] = "bar" },
            "user-1");

        result.Success.Should().BeTrue();
        _mockLlmModelRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
