using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Guilds/Edit.cshtml
/// (Phase F migration). Covers form rendering from the loaded guild + audio
/// settings, the audio-section fallback when settings fail to load, and the
/// save flow (same service calls as the page model, then the
/// /Guilds/Details/{id}?successMessage=… redirect).
/// </summary>
public class GuildEditTests : TestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IGuildAudioSettingsService> _audioSettingsService = new();

    private readonly GuildDto _guild = new()
    {
        Id = GuildId,
        Name = "Test Guild",
        IsActive = true,
        IconUrl = "https://cdn.example.com/icon.png"
    };

    private readonly GuildAudioSettings _audioSettings = new()
    {
        AudioEnabled = true,
        AutoLeaveTimeoutMinutes = 10,
        QueueEnabled = false
    };

    public GuildEditTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _guildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_guild);
        _guildService
            .Setup(s => s.UpdateGuildAsync(GuildId, It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_guild);

        _audioSettingsService
            .Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_audioSettings);
        _audioSettingsService
            .Setup(s => s.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, Action<GuildAudioSettings>, CancellationToken>((_, update, _) => update(_audioSettings))
            .ReturnsAsync(_audioSettings);

        // The page resolves these through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_audioSettingsService.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddLogging();

        // Declarative policies + the in-circuit resource-based GuildAccess recheck.
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin-user");
        auth.SetPolicies("RequireAdmin", "GuildAccess");
    }

    private IRenderedComponent<GuildEdit> RenderPage() =>
        RenderComponent<GuildEdit>(p => p.Add(c => c.Id, (long)GuildId));

    [Fact]
    public void RendersFormFromGuildAndAudioSettings()
    {
        var cut = RenderPage();

        cut.Markup.Should().Contain("General Settings");
        cut.Find("input#Input_IsActive").HasAttribute("checked").Should().BeTrue();

        // Audio section loaded from the audio settings service
        cut.Markup.Should().Contain("Audio Settings");
        cut.Find("input#Input_AudioEnabled").HasAttribute("checked").Should().BeTrue();
        cut.Find("input#Input_AutoLeaveTimeoutMinutes").GetAttribute("value").Should().Be("10");
        cut.Find("input#Input_QueueEnabled").HasAttribute("checked").Should().BeFalse();

        // More-settings + cancel links point at the routed pages
        cut.Markup.Should().Contain($"/Guilds/AudioSettings/{GuildId}");
        cut.Markup.Should().Contain($"/Guilds/Details/{GuildId}");
    }

    [Fact]
    public void HidesAudioSection_WhenAudioSettingsFailToLoad()
    {
        _audioSettingsService
            .Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPage();

        // Page still renders (AudioSettingsLoaded=false parity), just without the audio card
        cut.Markup.Should().Contain("General Settings");
        cut.Markup.Should().NotContain("Audio Settings");
    }

    [Fact]
    public void Saving_UpdatesGuildAndAudioSettings_ThenRedirectsToDetails()
    {
        var cut = RenderPage();

        cut.Find("input#Input_IsActive").Change(false);
        cut.Find("input#Input_QueueEnabled").Change(true);
        cut.Find("form").Submit();

        _guildService.Verify(
            s => s.UpdateGuildAsync(
                GuildId,
                It.Is<GuildUpdateRequestDto>(r => r.IsActive == false),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _audioSettingsService.Verify(
            s => s.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _audioSettings.QueueEnabled.Should().BeTrue();

        // Same post-save redirect the page model performed
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.Uri.Should().Contain($"/Guilds/Details/{GuildId}?successMessage=");
    }

    [Fact]
    public void Saving_WhenGuildGone_ShowsErrorWithoutRedirect()
    {
        var cut = RenderPage();

        _guildService
            .Setup(s => s.UpdateGuildAsync(GuildId, It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Guild not found. It may have been removed.");
        _audioSettingsService.Verify(
            s => s.UpdateSettingsAsync(It.IsAny<ulong>(), It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.Uri.Should().NotContain("successMessage");
    }

    [Fact]
    public void MissingGuild_RedirectsToNotFound()
    {
        _guildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        RenderPage();

        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.Uri.Should().EndWith("/Error/404");
    }
}
