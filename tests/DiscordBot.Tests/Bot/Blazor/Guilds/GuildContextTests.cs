using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Blazor.Guilds;

/// <summary>
/// Unit tests for <see cref="GuildContext"/>'s helper methods - <c>Breadcrumb</c> must reproduce
/// <c>GuildPageModelBase.BuildBasicBreadcrumb</c>/<c>BuildPageBreadcrumb</c> exactly (a Blazor
/// page has no base-class breadcrumb builder to inherit, so parity is asserted directly against
/// the Razor Pages implementation here rather than by eye).
/// </summary>
public class GuildContextTests
{
    private const ulong GuildId = 123456789012345678UL;

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: false,
        IsGuildAdmin: false,
        CanEdit: false,
        AudioEnabled: false,
        RatWatchEnabled: false,
        Tabs: GuildNavigationConfig.GetTabs());

    /// <summary>Exposes the protected breadcrumb builders on <see cref="GuildPageModelBase"/> for direct comparison.</summary>
    private sealed class ExposedGuildPageModelBase : GuildPageModelBase
    {
        public new DiscordBot.Bot.ViewModels.Components.GuildBreadcrumbViewModel BuildBasicBreadcrumb(ulong guildId, string guildName)
            => base.BuildBasicBreadcrumb(guildId, guildName);

        public new DiscordBot.Bot.ViewModels.Components.GuildBreadcrumbViewModel BuildPageBreadcrumb(ulong guildId, string guildName, string pageName)
            => base.BuildPageBreadcrumb(guildId, guildName, pageName);
    }

    [Fact]
    public void Breadcrumb_WithNoPageName_MatchesBuildBasicBreadcrumb()
    {
        var legacy = new ExposedGuildPageModelBase().BuildBasicBreadcrumb(GuildId, "Test Guild").Items;

        var actual = Context().Breadcrumb();

        actual.Should().BeEquivalentTo(legacy, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Breadcrumb_WithPageName_MatchesBuildPageBreadcrumb()
    {
        var legacy = new ExposedGuildPageModelBase().BuildPageBreadcrumb(GuildId, "Test Guild", "Members").Items;

        var actual = Context().Breadcrumb("Members");

        actual.Should().BeEquivalentTo(legacy, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Breadcrumb_WithNoPageName_HasThreeItems_LastIsCurrentAndUnlinked()
    {
        var items = Context().Breadcrumb();

        items.Should().HaveCount(3);
        items[0].Should().BeEquivalentTo(new { Label = "Home", Url = "/", IsCurrent = false });
        items[1].Should().BeEquivalentTo(new { Label = "Servers", Url = "/Guilds", IsCurrent = false });
        items[2].Label.Should().Be("Test Guild");
        items[2].Url.Should().Be($"/Guilds/Details/{GuildId}");
        items[2].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Breadcrumb_WithPageName_HasFourItems_GuildNameIsLinked_PageNameIsCurrent()
    {
        var items = Context().Breadcrumb("Members");

        items.Should().HaveCount(4);
        items[2].Label.Should().Be("Test Guild");
        items[2].Url.Should().Be($"/Guilds/Details/{GuildId}");
        items[2].IsCurrent.Should().BeFalse();
        items[3].Label.Should().Be("Members");
        items[3].Url.Should().BeNull();
        items[3].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void TabUrl_ReturnsUrlForKnownTab()
    {
        Context().TabUrl("members").Should().Be($"/Guilds/{GuildId}/Members");
    }

    [Fact]
    public void TabUrl_IsCaseInsensitive()
    {
        Context().TabUrl("MEMBERS").Should().Be($"/Guilds/{GuildId}/Members");
    }

    [Fact]
    public void TabUrl_ReturnsEmptyString_ForUnknownTab()
    {
        Context().TabUrl("not-a-real-tab").Should().BeEmpty();
    }

    [Fact]
    public void TabUrl_OverviewTab_PointsAtDetailsPage()
    {
        Context().TabUrl("overview").Should().Be($"/Guilds/Details/{GuildId}");
    }
}
