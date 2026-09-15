using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="Edit"/>, the routable replacement for
/// <c>Pages/Guilds/Edit.cshtml</c> + <c>EditModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4,
/// cluster 4b) - the first real <see cref="GuildPageBase"/>/<see cref="GuildContextGate"/>
/// consumer under a ported page.
/// </summary>
public class EditTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IGuildAudioSettingsService> _audioSettingsService = new();

    public EditTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_audioSettingsService.Object);
        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();
    }

    private static GuildContext Context(bool isActive = true) => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild", IsActive = isActive },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs());

    private void SetupOk(bool isActive = true) =>
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(isActive)));

    private void NavigateTo(string path) =>
        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private IRenderedComponent<Edit> RenderPage()
    {
        NavigateTo($"/Guilds/Edit/{GuildId}");
        SetInteractiveRendererInfo();
        return Render<Edit>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void Ok_RendersGeneralAndAudioSettings_WhenAudioSettingsLoad()
    {
        SetupOk();
        _audioSettingsService.Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings { AudioEnabled = true, AutoLeaveTimeoutMinutes = 7, QueueEnabled = false });

        var cut = RenderPage();

        cut.Find("#Input_IsActive").HasAttribute("checked").Should().BeTrue();
        cut.Find("#Input_AudioEnabled").HasAttribute("checked").Should().BeTrue();
        cut.Find("#Input_AutoLeaveTimeoutMinutes").GetAttribute("value").Should().Be("7");
        cut.Find("#Input_QueueEnabled").HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public void AudioSettingsLoadFailure_HidesAudioSection()
    {
        SetupOk();
        _audioSettingsService.Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPage();

        cut.FindAll("#Input_AudioEnabled").Should().BeEmpty();
    }

    [Fact]
    public void ValidSave_UpdatesGuildAndAudioSettings_ToastsAndNavigates()
    {
        SetupOk(isActive: false);
        _audioSettingsService.Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings());
        _guildService.Setup(s => s.UpdateGuildAsync(GuildId, It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Guild", IsActive = true });
        _audioSettingsService.Setup(s => s.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings());

        var cut = RenderPage();
        cut.Find("form").Submit();

        _guildService.Verify(s => s.UpdateGuildAsync(GuildId, It.Is<GuildUpdateRequestDto>(d => d.IsActive == false), It.IsAny<CancellationToken>()), Times.Once);
        _audioSettingsService.Verify(s => s.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()), Times.Once);

        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));

        var nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.Should().EndWith($"/Guilds/Details/{GuildId}"));
    }

    [Fact]
    public void GuildNotFound_UpdateGuildAsync_ShowsErrorAndStays()
    {
        SetupOk();
        _audioSettingsService.Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings());
        _guildService.Setup(s => s.UpdateGuildAsync(GuildId, It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        var cut = RenderPage();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Guild not found"));
        _audioSettingsService.Verify(s => s.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate_WithoutCallingGuildOrAudioServices()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Server Not Found");
        _guildService.Verify(s => s.UpdateGuildAsync(It.IsAny<ulong>(), It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _audioSettingsService.Verify(s => s.GetSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
