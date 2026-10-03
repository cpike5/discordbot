using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace DiscordBot.Tests.Bot.Pages.Admin;

/// <summary>
/// Unit tests for <see cref="SettingsModel"/>'s JSON error-shape handling.
/// </summary>
/// <remarks>
/// Regression coverage for the bug where a failure result's <c>errors</c> field was always
/// emitted (even as an empty array), which the client-side JS in settings.js treated as truthy
/// and used instead of falling back to <c>message</c>.
/// </remarks>
public class SettingsModelTests
{
    private readonly Mock<ISettingsSectionService> _mockSettingsSectionService = new();
    private readonly Mock<IAppearanceSettingsService> _mockAppearanceSettingsService = new();
    private readonly Mock<IBotControlService> _mockBotControlService = new();
    private readonly Mock<ILogger<SettingsModel>> _mockLogger = new();
    private readonly SettingsModel _settingsModel;

    public SettingsModelTests()
    {
        _settingsModel = new SettingsModel(
            _mockSettingsSectionService.Object,
            _mockAppearanceSettingsService.Object,
            _mockBotControlService.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _settingsModel.PageContext = new PageContext(actionContext);
    }

    private static JsonElement SerializeResultValue(object? value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_FailureWithoutErrors_OmitsErrorsProperty()
    {
        // Arrange
        _mockSettingsSectionService
            .Setup(s => s.SaveCategoryAsync("General", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsSectionResult
            {
                Success = false,
                Message = "Something went wrong.",
                StatusCode = 500
            });

        // Act
        var result = await _settingsModel.OnPostSaveCategoryAsync("General");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(500);

        var element = SerializeResultValue(jsonResult.Value);
        element.GetProperty("success").GetBoolean().Should().BeFalse();
        element.GetProperty("message").GetString().Should().Be("Something went wrong.");
        element.TryGetProperty("errors", out _).Should().BeFalse(
            "an empty errors collection must not be serialized, or client-side JS treats it as truthy and blanks the message");
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_FailureWithErrors_IncludesErrorsProperty()
    {
        // Arrange
        _mockSettingsSectionService
            .Setup(s => s.SaveCategoryAsync("General", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsSectionResult
            {
                Success = false,
                Message = "Validation failed.",
                StatusCode = 400,
                Errors = new List<string> { "Field X is required.", "Field Y is invalid." }
            });

        // Act
        var result = await _settingsModel.OnPostSaveCategoryAsync("General");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);

        var element = SerializeResultValue(jsonResult.Value);
        element.GetProperty("success").GetBoolean().Should().BeFalse();
        element.TryGetProperty("errors", out var errorsElement).Should().BeTrue("validation errors should be surfaced to the client");
        errorsElement.EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(
            new[] { "Field X is required.", "Field Y is invalid." });
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_Success_ReturnsSuccessShapeWithoutErrors()
    {
        // Arrange
        _mockSettingsSectionService
            .Setup(s => s.SaveCategoryAsync("General", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsSectionResult
            {
                Success = true,
                Message = "Saved.",
                RestartRequired = false
            });

        // Act
        var result = await _settingsModel.OnPostSaveCategoryAsync("General");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var element = SerializeResultValue(jsonResult.Value);
        element.GetProperty("success").GetBoolean().Should().BeTrue();
        element.TryGetProperty("errors", out _).Should().BeFalse();
    }

    // ---- Appearance is SuperAdmin-only, even through the generic handlers

    [Theory]
    [InlineData("Appearance")]
    [InlineData("appearance")]
    [InlineData(" Appearance ")]
    public async Task OnPostSaveCategoryAsync_ForAppearance_IsForbiddenForANonSuperAdmin(string category)
    {
        _mockAppearanceSettingsService.Setup(a => a.IsSuperAdminAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(false);

        var result = await _settingsModel.OnPostSaveCategoryAsync(category);

        result.Should().BeOfType<ForbidResult>();
        _mockSettingsSectionService.Verify(
            s => s.SaveCategoryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_ForTheNumericSpellingOfAppearance_IsAlsoForbidden()
    {
        var numeric = ((int)DiscordBot.Core.Enums.SettingCategory.Appearance).ToString();

        var result = await _settingsModel.OnPostSaveCategoryAsync(numeric);

        result.Should().BeOfType<ForbidResult>("the service parses numbers as categories too");
        _mockSettingsSectionService.Verify(
            s => s.SaveCategoryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_ForAppearance_IsAllowedForASuperAdmin()
    {
        _mockAppearanceSettingsService.Setup(a => a.IsSuperAdminAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(true);
        _mockSettingsSectionService
            .Setup(s => s.SaveCategoryAsync("Appearance", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsSectionResult { Success = true, Message = "Saved." });

        var result = await _settingsModel.OnPostSaveCategoryAsync("Appearance");

        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task OnPostResetCategoryAsync_ForAppearance_IsForbiddenForANonSuperAdmin()
    {
        var result = await _settingsModel.OnPostResetCategoryAsync("Appearance");

        result.Should().BeOfType<ForbidResult>();
        _mockSettingsSectionService.Verify(
            s => s.ResetCategoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostSaveCategoryAsync_ForOtherCategories_NeverAsksWhoTheCallerIs()
    {
        _mockSettingsSectionService
            .Setup(s => s.SaveCategoryAsync("General", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsSectionResult { Success = true, Message = "Saved." });

        await _settingsModel.OnPostSaveCategoryAsync("General");

        _mockAppearanceSettingsService.Verify(a => a.IsSuperAdminAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public void SettingsPage_HasNoUnscopedSaveAllHandler()
    {
        typeof(SettingsModel).GetMethod("OnPostSaveAllAsync").Should().BeNull(
            "a save-all would write keys from every tab, including the SuperAdmin-only ones");
        typeof(ISettingsSectionService).GetMethod("SaveAllAsync").Should().BeNull();
    }
}
