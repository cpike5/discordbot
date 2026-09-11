using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components;
using DiscordBot.Bot.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Components;

/// <summary>
/// Covers the routable replacement for Pages/Components.cshtml
/// (docs/plans/blazor-port-plan.md §5 Phase 2 step 3). Mirrors the service setup
/// Blazor/Shared/IconPathsUsageGuardTests.cs uses to render WidgetsShowcase - ComponentsPage
/// composes the same six showcase sections plus ToastHost/LoadingOverlay.
/// </summary>
public class ComponentsPageTests : BlazorComponentTestContext
{
    public ComponentsPageTests()
    {
        var metricsService = new Mock<IDashboardMetricsService>();
        metricsService
            .Setup(m => m.GetCurrentStatus(null, null))
            .Returns(new BotStatusDto { ConnectionState = "Connected", GuildCount = 1, LatencyMs = 10, Uptime = TimeSpan.FromMinutes(5) });
        var guildService = new Mock<IGuildService>();
        guildService
            .Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuildDto>());
        var versionService = new Mock<IVersionService>();
        versionService.Setup(v => v.GetVersion()).Returns("v0.0.0-test");
        var audioStatusService = new Mock<IDashboardAudioStatusService>();
        audioStatusService
            .Setup(s => s.GetCurrentAudioStatus(123456789012345678UL, null, null))
            .Returns(new AudioStatusDto { GuildId = 123456789012345678UL, IsConnected = false });

        Services.AddSingleton(metricsService.Object);
        Services.AddSingleton(guildService.Object);
        Services.AddSingleton(versionService.Object);
        Services.AddSingleton(audioStatusService.Object);
        Services.AddSingleton(Mock.Of<IDashboardNotificationQueryService>());
        Services.AddSingleton(Mock.Of<IAudioService>());
        Services.AddSingleton(Mock.Of<IPlaybackService>());

        AddAuthorizedAdmin();
    }

    [Fact]
    public void RendersAllSixShowcaseSections()
    {
        var cut = Render<ComponentsPage>();

        foreach (var testId in new[]
                 {
                     "components-section-primitives", "components-section-status-and-headers",
                     "components-section-forms", "components-section-navigation-and-overlays",
                     "components-section-widgets", "components-section-tts"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }
    }

    [Fact]
    public void RendersNavLink_ForEachSection()
    {
        var cut = Render<ComponentsPage>();

        var nav = cut.Find("[data-testid='components-nav']");
        nav.QuerySelectorAll("a").Should().HaveCount(6);
    }

    [Fact]
    public void RendersToastHostAndLoadingOverlay()
    {
        var cut = Render<ComponentsPage>();

        // At least one of each: the page's own instance plus NavigationAndOverlaysShowcase's own
        // demo ToastHost/LoadingOverlay (the section shows what they look like, same as the
        // legacy Razor Page did), so >=1 rather than an exact count.
        cut.FindComponents<DiscordBot.Bot.Blazor.Shared.ToastHost>().Should().NotBeEmpty();
        cut.FindComponents<DiscordBot.Bot.Blazor.Shared.LoadingOverlay>().Should().NotBeEmpty();
    }
}
