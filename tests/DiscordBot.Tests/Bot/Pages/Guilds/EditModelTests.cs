using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// Tests for the guild edit page. The audio fields only mean something when the form was drawn
/// with the real audio settings; otherwise saving must leave the stored settings alone.
/// </summary>
public class EditModelTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IGuildAudioSettingsService> _audio = new();
    private readonly EditModel _model;

    public EditModelTests()
    {
        _guilds
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });
        _guilds
            .Setup(s => s.UpdateGuildAsync(GuildId, It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });

        _model = new EditModel(_guilds.Object, _audio.Object, Mock.Of<ILogger<EditModel>>());

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        _model.PageContext = new PageContext(actionContext);
        _model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGet_WhenAudioSettingsLoad_ShowsThemAndMarksTheFormAsLoaded()
    {
        _audio
            .Setup(a => a.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings { AudioEnabled = false, AutoLeaveTimeoutMinutes = 12, QueueEnabled = false });

        await _model.OnGetAsync(GuildId, CancellationToken.None);

        _model.AudioSettingsLoaded.Should().BeTrue();
        _model.Input.AudioSettingsLoaded.Should().BeTrue();
        _model.Input.AutoLeaveTimeoutMinutes.Should().Be(12);
        _model.Input.AudioEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task OnGet_WhenAudioSettingsFail_StillRendersAndSaysTheyAreNotLoaded()
    {
        _audio
            .Setup(a => a.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        var result = await _model.OnGetAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.AudioSettingsLoaded.Should().BeFalse();
        _model.Input.AudioSettingsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task OnPost_WhenAudioWasNotLoaded_DoesNotOverwriteTheStoredAudioSettings()
    {
        _model.Input = new EditModel.InputModel
        {
            IsActive = true,
            AudioSettingsLoaded = false,
            // The defaults the model binder fills in when the audio section was not on the form
            AudioEnabled = true,
            AutoLeaveTimeoutMinutes = 5,
            QueueEnabled = true
        };

        var result = await _model.OnPostAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        _audio.Verify(
            a => a.UpdateSettingsAsync(It.IsAny<ulong>(), It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "saving the default values would overwrite the real settings");
    }

    [Fact]
    public async Task OnPost_WhenAudioWasLoaded_SavesTheAudioSettings()
    {
        _model.Input = new EditModel.InputModel
        {
            IsActive = true,
            AudioSettingsLoaded = true,
            AudioEnabled = false,
            AutoLeaveTimeoutMinutes = 30,
            QueueEnabled = false
        };

        await _model.OnPostAsync(GuildId, CancellationToken.None);

        _audio.Verify(
            a => a.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPost_WithInvalidInput_KeepsTheChromeAndMarksTheFormDirty()
    {
        _model.Input = new EditModel.InputModel { AudioSettingsLoaded = true, AutoLeaveTimeoutMinutes = 9999 };
        _model.ModelState.AddModelError("Input.AutoLeaveTimeoutMinutes", "Enter a whole number of minutes from 0 to 1440.");

        var result = await _model.OnPostAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.DirtyOnLoad.Should().BeTrue();
        _model.Header.GuildId.Should().Be(GuildId);
        _model.Navigation.Tabs.Should().NotBeEmpty();
        _model.Input.AutoLeaveTimeoutMinutes.Should().Be(9999, "what the user typed is shown again");
    }
}
