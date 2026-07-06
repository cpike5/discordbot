using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Guilds/Welcome.cshtml
/// (Phase F migration). Covers form rendering from the loaded configuration,
/// the C#-ported live preview (token replacement over the encoded message),
/// the channel-required manual validation, and the save flow through the same
/// IWelcomeService call the page model used.
/// </summary>
public class GuildWelcomeTests : TestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong ChannelId = 222222222222222222UL;

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IWelcomeService> _welcomeService = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    private readonly GuildDto _guild = new()
    {
        Id = GuildId,
        Name = "Test Guild",
        IsActive = true
    };

    private readonly WelcomeConfigurationDto _config = new()
    {
        GuildId = GuildId,
        IsEnabled = true,
        WelcomeChannelId = ChannelId,
        WelcomeMessage = "Welcome to {server}, {user}!",
        IncludeAvatar = true,
        UseEmbed = true,
        EmbedColor = "#5865F2"
    };

    public GuildWelcomeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _guildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_guild);

        _welcomeService
            .Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_config);
        _welcomeService
            .Setup(s => s.UpdateConfigurationAsync(GuildId, It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_config);

        _channelResolver
            .Setup(r => r.GetTextChannels(GuildId))
            .Returns(new List<ChannelInfo>
            {
                new(ChannelId, "general", 0, ChannelDisplayType.Text),
                new(333333333333333333UL, "announcements", 1, ChannelDisplayType.Announcement)
            });

        // The page resolves these through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_welcomeService.Object);
        Services.AddSingleton(_channelResolver.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddLogging();

        // Declarative policies + the in-circuit resource-based GuildAccess recheck.
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin-user");
        auth.SetPolicies("RequireAdmin", "GuildAccess");
    }

    private IRenderedComponent<GuildWelcome> RenderPage() =>
        RenderComponent<GuildWelcome>(p => p.Add(c => c.GuildId, (long)GuildId));

    [Fact]
    public void RendersFormFromConfiguration()
    {
        var cut = RenderPage();

        cut.Find("input#Input_IsEnabled").HasAttribute("checked").Should().BeTrue();
        cut.Find("input#Input_UseEmbed").HasAttribute("checked").Should().BeTrue();
        cut.Find("textarea#Input_WelcomeMessage").GetAttribute("value").Should().Contain("Welcome to {server}, {user}!");
        cut.Find("input#Input_EmbedColor").GetAttribute("value").Should().Be("#5865F2");

        // Channel dropdown from the resolver (placeholder + 2 channels)
        var options = cut.FindAll("select#Input_WelcomeChannelId option");
        options.Should().HaveCount(3);
        cut.Markup.Should().Contain("# general");
        cut.Markup.Should().Contain("📢 announcements");
    }

    [Fact]
    public void RendersDefaults_WhenNoConfigurationExists()
    {
        _welcomeService
            .Setup(s => s.GetConfigurationAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        var cut = RenderPage();

        cut.Find("input#Input_IsEnabled").HasAttribute("checked").Should().BeFalse();
        cut.Find("textarea#Input_WelcomeMessage").GetAttribute("value")
            .Should().Contain("Welcome to {server}, {user}! You are member #{memberCount}.");
        // Disabled state overlays the config form and the preview shows the disabled note
        cut.Find("#welcome-config-form").ClassList.Should().Contain("form-section-disabled");
        cut.Markup.Should().Contain("Welcome messages are disabled");
    }

    [Fact]
    public void LivePreview_ReplacesTokensAndEncodesMessage()
    {
        var cut = RenderPage();

        cut.Find("textarea#Input_WelcomeMessage")
            .Input("Hey {user}, welcome to {server}! <b>#{memberCount}</b>");

        var preview = cut.Find("#preview-message");
        preview.InnerHtml.Should().Contain("<span class=\"discord-mention\">@NewMember</span>");
        preview.InnerHtml.Should().Contain("<strong>Test Guild</strong>");
        preview.InnerHtml.Should().Contain("<strong>1,234</strong>");
        // User-provided markup is encoded, not injected
        preview.InnerHtml.Should().Contain("&lt;b&gt;");

        // Embed border follows UseEmbed + valid hex color
        cut.Find("#preview-content").GetAttribute("style").Should().Contain("border-left: 4px solid #5865F2");
    }

    [Fact]
    public void Saving_WhenEnabledWithoutChannel_ShowsErrorAndDoesNotCallService()
    {
        var cut = RenderPage();

        cut.Find("select#Input_WelcomeChannelId").Change("");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("A welcome channel must be selected when welcome messages are enabled.");
        _welcomeService.Verify(
            s => s.UpdateConfigurationAsync(It.IsAny<ulong>(), It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Saving_PersistsConfigurationAndShowsSuccess()
    {
        var cut = RenderPage();

        cut.Find("textarea#Input_WelcomeMessage").Input("Hello {user}");
        cut.Find("form").Submit();

        _welcomeService.Verify(
            s => s.UpdateConfigurationAsync(
                GuildId,
                It.Is<WelcomeConfigurationUpdateDto>(d =>
                    d.IsEnabled == true &&
                    d.WelcomeChannelId == ChannelId &&
                    d.WelcomeMessage == "Hello {user}" &&
                    d.UseEmbed == true &&
                    d.EmbedColor == "#5865F2"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        cut.Markup.Should().Contain("Welcome configuration saved successfully.");
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "blazorInterop.toast");
    }

    [Fact]
    public void Saving_WhenGuildGone_ShowsError()
    {
        _welcomeService
            .Setup(s => s.UpdateConfigurationAsync(GuildId, It.IsAny<WelcomeConfigurationUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        var cut = RenderPage();
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Guild not found. It may have been removed.");
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
