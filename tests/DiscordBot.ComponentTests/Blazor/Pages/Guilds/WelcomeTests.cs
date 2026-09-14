using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="Welcome"/>, the routable replacement for
/// <c>Pages/Guilds/Welcome.cshtml</c> + <c>WelcomeModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4b): defaults-when-unconfigured, the manual "channel required when enabled"
/// rule, and the server-rendered live preview.
/// </summary>
public class WelcomeTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IWelcomeService> _welcomeService = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    public WelcomeTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_welcomeService.Object);
        Services.AddSingleton(_channelResolver.Object);
        _channelResolver.Setup(r => r.GetTextChannels(GuildId)).Returns([]);
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

    private IRenderedComponent<Welcome> RenderPage()
    {
        NavigateTo($"/Guilds/Welcome/{GuildId}");
        SetInteractiveRendererInfo();
        return Render<Welcome>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void NoConfiguration_UsesDefaults()
    {
        _welcomeService.Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync((WelcomeConfigurationDto?)null);

        var cut = RenderPage();

        cut.Find("#Input_IsEnabled").HasAttribute("checked").Should().BeFalse();
        cut.Markup.Should().Contain("Welcome messages are disabled");
    }

    [Fact]
    public void ExistingConfiguration_PopulatesFormAndPreview()
    {
        _welcomeService.Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(new WelcomeConfigurationDto
        {
            GuildId = GuildId,
            IsEnabled = true,
            WelcomeChannelId = 555,
            WelcomeMessage = "Welcome {user} to {server}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#123456"
        });

        var cut = RenderPage();

        cut.Find("#Input_IsEnabled").HasAttribute("checked").Should().BeTrue();
        cut.Find("#Input_WelcomeMessage").TextContent.Should().Be("Welcome {user} to {server}!");
        cut.Markup.Should().Contain("discord-mention");
        cut.Markup.Should().Contain("Test Guild");
    }

    [Fact]
    public void SaveEnabled_WithoutChannel_ShowsFieldError_DoesNotCallService()
    {
        _welcomeService.Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(new WelcomeConfigurationDto
        {
            GuildId = GuildId,
            IsEnabled = true,
            WelcomeMessage = "hi",
            EmbedColor = "#5865F2"
        });

        var cut = RenderPage();
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("A welcome channel must be selected");
        _welcomeService.Verify(s => s.UpdateConfigurationAsync(It.IsAny<ulong>(), It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SaveDisabled_CallsUpdateConfiguration_Toasts()
    {
        _welcomeService.Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(new WelcomeConfigurationDto
        {
            GuildId = GuildId,
            IsEnabled = false,
            WelcomeMessage = "hi",
            EmbedColor = "#5865F2"
        });
        _welcomeService.Setup(s => s.UpdateConfigurationAsync(GuildId, It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WelcomeConfigurationDto { GuildId = GuildId });

        var cut = RenderPage();
        cut.Find("form").Submit();

        _welcomeService.Verify(s => s.UpdateConfigurationAsync(GuildId, It.Is<WelcomeConfigurationUpdateDto>(d => d.IsEnabled == false), It.IsAny<CancellationToken>()), Times.Once);
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));

        // Stays on the page - no navigation.
        var nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        nav.Uri.Should().EndWith($"/Guilds/Welcome/{GuildId}");
    }

    [Fact]
    public void ToggleTokenHelp_HidesAndShowsTokenTable()
    {
        _welcomeService.Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync((WelcomeConfigurationDto?)null);

        var cut = RenderPage();
        cut.Markup.Should().Contain("Available Tokens");
        cut.FindAll("table").Should().NotBeEmpty();

        cut.Find("button[aria-expanded]").Click();
        cut.FindAll("table").Should().BeEmpty();
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Server Not Found");
        _welcomeService.Verify(s => s.GetConfigurationAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
