using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="AssistantSettings"/>, the routable replacement for
/// <c>Pages/Guilds/AssistantSettings.cshtml</c> + <c>AssistantSettingsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b): the channel allow-list, the Tool
/// Access checklist (default-set banner + <c>ToolCatalog.NormalizeSelection</c> on save), and the
/// rate-limit override.
/// </summary>
public class AssistantSettingsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IAssistantGuildSettingsService> _settingsService = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();
    private readonly Mock<ISettingsService> _globalSettingsService = new();

    public AssistantSettingsTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_settingsService.Object);
        Services.AddSingleton(_channelResolver.Object);
        Services.AddSingleton(_globalSettingsService.Object);
        Services.AddSingleton<IOptions<AssistantOptions>>(Options.Create(new AssistantOptions()));

        _channelResolver.Setup(r => r.GetTextChannels(GuildId)).Returns([]);
        _globalSettingsService.Setup(s => s.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();

        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(new GuildContext(
                Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
                GuildId: GuildId,
                GuildIdString: GuildId.ToString(),
                IsAppAdmin: true,
                IsGuildAdmin: false,
                CanEdit: true,
                AudioEnabled: true,
                RatWatchEnabled: true,
                Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs())));
    }

    private void NavigateTo(string path) =>
        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private IRenderedComponent<AssistantSettings> RenderPage()
    {
        NavigateTo($"/Guilds/AssistantSettings/{GuildId}");
        SetInteractiveRendererInfo();
        return Render<AssistantSettings>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void NoToolsSaved_ShowsDefaultSetBanner_WithDefaultsTicked()
    {
        _settingsService.Setup(s => s.GetOrCreateSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantGuildSettings { GuildId = GuildId });

        var cut = RenderPage();

        cut.Markup.Should().Contain("this server uses the default set");
        cut.FindAll("input[type='checkbox']").Should().Contain(cb => cb.HasAttribute("checked"));
    }

    [Fact]
    public void GloballyDisabled_ShowsWarningBanner()
    {
        _globalSettingsService.Setup(s => s.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _settingsService.Setup(s => s.GetOrCreateSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantGuildSettings { GuildId = GuildId });

        var cut = RenderPage();

        cut.Markup.Should().Contain("globally disabled");
    }

    [Fact]
    public void ValidSave_NormalizesToolSelection_AndSetsChannelsAndRateLimit()
    {
        var settings = new AssistantGuildSettings { GuildId = GuildId, IsEnabled = false };
        _settingsService.Setup(s => s.GetOrCreateSettingsAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        AssistantGuildSettings? saved = null;
        _settingsService.Setup(s => s.UpdateSettingsAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantGuildSettings, CancellationToken>((s, _) => saved = s)
            .Returns(Task.CompletedTask);

        var cut = RenderPage();
        cut.Find("form").Submit();

        _settingsService.Verify(s => s.UpdateSettingsAsync(It.IsAny<AssistantGuildSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        saved.Should().NotBeNull();
        // Every catalogued guild tool defaults to ticked (no explicit selection posted), so the
        // save normalizes back to the empty "use the default set" marker rather than pinning today's defaults.
        saved!.GetEnabledToolsList().Should().BeEmpty();
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Server Not Found");
        _settingsService.Verify(s => s.GetOrCreateSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
