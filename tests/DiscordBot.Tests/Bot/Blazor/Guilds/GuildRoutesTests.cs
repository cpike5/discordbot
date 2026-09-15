using DiscordBot.Bot.Blazor.Guilds;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Blazor.Guilds;

/// <summary>
/// Unit tests for <see cref="GuildRoutes"/> - pure helpers used by a static <c>GuildLayout</c> to
/// read a guild id and the active nav tab off <c>NavigationManager.Uri</c>. See "GuildContext" in
/// <c>docs/architecture/patterns.md</c>.
/// </summary>
public class GuildRoutesTests
{
    private const ulong GuildId = 123456789012345678UL;

    public static IEnumerable<object[]> DirectIdPaths => new List<object[]>
    {
        new object[] { "/Guilds/123456789012345678/Members" },
        new object[] { "/guilds/123456789012345678/members" },
        new object[] { "/Guilds/123456789012345678/Currency" },
        new object[] { "/Guilds/123456789012345678/FeatureRequests" },
        new object[] { "/Guilds/123456789012345678" },
        new object[] { "/Guilds/123456789012345678/" }
    };

    public static IEnumerable<object[]> NamedPagePaths => new List<object[]>
    {
        new object[] { "/Guilds/Details/123456789012345678" },
        new object[] { "/guilds/details/123456789012345678" },
        new object[] { "/Guilds/ModerationSettings/123456789012345678" },
        new object[] { "/Guilds/ScheduledMessages/123456789012345678" },
        new object[] { "/Guilds/Soundboard/123456789012345678" },
        new object[] { "/Guilds/RatWatch/123456789012345678" },
        new object[] { "/Guilds/Reminders/123456789012345678" },
        new object[] { "/Guilds/Welcome/123456789012345678" },
        new object[] { "/Guilds/AssistantSettings/123456789012345678" }
    };

    [Theory]
    [MemberData(nameof(DirectIdPaths))]
    public void TryGetGuildId_MatchesGuildsSlashIdShape(string path)
    {
        GuildRoutes.TryGetGuildId(path, out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Theory]
    [MemberData(nameof(NamedPagePaths))]
    public void TryGetGuildId_MatchesGuildsSlashPageSlashIdShape(string path)
    {
        GuildRoutes.TryGetGuildId(path, out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Fact]
    public void TryGetGuildId_MatchesAbsoluteUri()
    {
        GuildRoutes.TryGetGuildId("https://bot.example.com/Guilds/Details/123456789012345678?tab=x", out var guildId)
            .Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Fact]
    public void TryGetGuildId_MatchesSubPathUnderDirectIdShape()
    {
        // e.g. a currency sub-page nested under the {guildId}-first shape.
        GuildRoutes.TryGetGuildId($"/Guilds/{GuildId}/Currency/Details/5", out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Theory]
    [InlineData("/Guilds")]
    [InlineData("/Guilds/")]
    [InlineData("/Guilds/Index")]
    [InlineData("/")]
    [InlineData("/Admin/Users")]
    [InlineData("")]
    public void TryGetGuildId_ReturnsFalse_ForNonGuildRoutes(string path)
    {
        GuildRoutes.TryGetGuildId(path, out var guildId).Should().BeFalse();
        guildId.Should().Be(0);
    }

    [Fact]
    public void ResolveActiveTabId_ResolvesOverview_OnlyForTheDetailsUrl()
    {
        GuildRoutes.ResolveActiveTabId($"/Guilds/Details/{GuildId}", GuildId).Should().Be("overview");
    }

    [Fact]
    public void ResolveActiveTabId_ReturnsNull_ForBareGuildRootPath()
    {
        // No tab is registered at a bare "/Guilds/{id}" - only "/Guilds/Details/{id}" is Overview.
        GuildRoutes.ResolveActiveTabId($"/Guilds/{GuildId}", GuildId).Should().BeNull();
    }

    [Fact]
    public void ResolveActiveTabId_ResolvesMembers_ForGuildsSlashIdSlashMembers()
    {
        GuildRoutes.ResolveActiveTabId($"/Guilds/{GuildId}/Members", GuildId).Should().Be("members");
    }

    [Fact]
    public void ResolveActiveTabId_ResolvesCurrency_ForGuildsSlashIdSlashCurrency()
    {
        GuildRoutes.ResolveActiveTabId($"/Guilds/{GuildId}/Currency", GuildId).Should().Be("currency");
    }

    [Fact]
    public void ResolveActiveTabId_UsesLongestPrefixMatch_ForASubPageUnderATab()
    {
        // /Guilds/{id}/Currency/Details/5 is a sub-page under the currency tab.
        GuildRoutes.ResolveActiveTabId($"/Guilds/{GuildId}/Currency/Details/5", GuildId).Should().Be("currency");
    }

    [Fact]
    public void ResolveActiveTabId_IsCaseInsensitive()
    {
        GuildRoutes.ResolveActiveTabId($"/guilds/details/{GuildId}", GuildId).Should().Be("overview");
    }

    [Fact]
    public void ResolveActiveTabId_ReturnsNull_ForANonGuildPath()
    {
        GuildRoutes.ResolveActiveTabId("/Admin/Users", GuildId).Should().BeNull();
    }

    [Fact]
    public void ResolveActiveTabId_DoesNotFalsePositive_OnASimilarButLongerGuildId()
    {
        // A path for a different (longer, so string-prefix-hazardous) guild id must not match
        // this guild's tabs even though the shorter id is numerically a prefix of the longer one.
        var similarGuildId = ulong.Parse(GuildId + "9");
        GuildRoutes.ResolveActiveTabId($"/Guilds/Details/{similarGuildId}", GuildId).Should().BeNull();
    }
}
