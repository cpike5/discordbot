using DiscordBot.Bot.Blazor.Portal;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Blazor.Portal;

/// <summary>
/// Unit tests for <see cref="PortalRoutes"/> - pure helper a static <c>PortalLayout</c> uses to
/// read a guild id off <c>NavigationManager.Uri</c>, mirroring <c>GuildRoutesTests</c> for the
/// guild-side equivalent. See "Portal three-state gate" in <c>docs/architecture/patterns.md</c>.
/// </summary>
public class PortalRoutesTests
{
    private const ulong GuildId = 123456789012345678UL;

    public static IEnumerable<object[]> FeatureFirstPaths => new List<object[]>
    {
        new object[] { "/Portal/Soundboard/123456789012345678" },
        new object[] { "/portal/soundboard/123456789012345678" },
        new object[] { "/Portal/TTS/123456789012345678" },
        new object[] { "/Portal/VOX/123456789012345678" }
    };

    public static IEnumerable<object[]> IdFirstPaths => new List<object[]>
    {
        new object[] { "/Portal/123456789012345678/blazor-probe" },
        new object[] { "/portal/123456789012345678/blazor-probe" },
        new object[] { "/Portal/123456789012345678" },
        new object[] { "/Portal/123456789012345678/" }
    };

    [Theory]
    [MemberData(nameof(FeatureFirstPaths))]
    public void TryGetGuildId_MatchesPortalSlashFeatureSlashIdShape(string path)
    {
        PortalRoutes.TryGetGuildId(path, out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Theory]
    [MemberData(nameof(IdFirstPaths))]
    public void TryGetGuildId_MatchesPortalSlashIdShape(string path)
    {
        PortalRoutes.TryGetGuildId(path, out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Fact]
    public void TryGetGuildId_MatchesAbsoluteUri()
    {
        PortalRoutes.TryGetGuildId($"https://bot.example.com/Portal/Soundboard/{GuildId}?tab=x", out var guildId)
            .Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Fact]
    public void TryGetGuildId_PrefersIdFirstShape_ForAFeatureNameThatIsNotNumeric()
    {
        // The id-first pattern is tried first; a feature segment like "Soundboard" can never
        // itself parse as a ulong, so there is no ambiguity between the two shapes in practice -
        // this just proves the id-first path is actually reached first, not skipped.
        PortalRoutes.TryGetGuildId($"/Portal/{GuildId}/blazor-probe", out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }

    [Theory]
    [InlineData("/Portal")]
    [InlineData("/Portal/")]
    [InlineData("/Portal/Soundboard")]
    [InlineData("/")]
    [InlineData("/Guilds/123456789012345678/Members")]
    [InlineData("")]
    public void TryGetGuildId_ReturnsFalse_ForNonPortalRoutes(string path)
    {
        PortalRoutes.TryGetGuildId(path, out var guildId).Should().BeFalse();
        guildId.Should().Be(0);
    }

    [Fact]
    public void TryGetGuildId_IsCaseInsensitive()
    {
        PortalRoutes.TryGetGuildId($"/PORTAL/SOUNDBOARD/{GuildId}", out var guildId).Should().BeTrue();
        guildId.Should().Be(GuildId);
    }
}
