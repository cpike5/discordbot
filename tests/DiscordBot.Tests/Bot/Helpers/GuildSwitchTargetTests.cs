using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class GuildSwitchTargetTests
{
    private const ulong Target = 223456789012345678UL;

    [Fact]
    public void For_Soundboard_GoesToTheTargetGuildsSoundboard()
    {
        GuildSwitchTarget.For("audio", Target, "Guilds/Soundboard/{guildId:long}")
            .Should().Be($"/Guilds/Soundboard/{Target}");
    }

    [Fact]
    public void For_ASubPageOfATab_StaysOnThatSubPage()
    {
        // Text-to-Speech shares the Audio tab with the Soundboard, but has its own page
        GuildSwitchTarget.For("audio", Target, "Guilds/TextToSpeech/{guildId:long}")
            .Should().Be($"/Guilds/TextToSpeech/{Target}");
    }

    [Fact]
    public void For_ARouteWithGuildIdInTheMiddle_KeepsTheRestOfThePath()
    {
        GuildSwitchTarget.For("members", Target, "/Guilds/{guildId:long}/Members")
            .Should().Be($"/Guilds/{Target}/Members");
    }

    [Fact]
    public void For_ADetailsPageWithAnEntityId_GoesToItsSectionsPage()
    {
        // The scheduled message belongs to the other guild, so its list is the closest page
        GuildSwitchTarget.For("messages", Target, "Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}")
            .Should().Be($"/Guilds/ScheduledMessages/{Target}");
    }

    [Fact]
    public void For_ADetailsPageWithNoKnownTab_GoesToTheOverview()
    {
        GuildSwitchTarget.For(null, Target, "Guilds/FeatureRequests/Details/{guildId:long}/{id:guid}")
            .Should().Be($"/Guilds/Details/{Target}");
    }

    [Theory]
    [InlineData("no-such-tab")]
    [InlineData("")]
    [InlineData(null)]
    public void For_AnUnknownTabWithoutARoute_GoesToTheOverview(string? tab)
    {
        GuildSwitchTarget.For(tab, Target).Should().Be($"/Guilds/Details/{Target}");
    }

    [Fact]
    public void For_AKnownTabWithoutARoute_GoesToThatTabsPage()
    {
        GuildSwitchTarget.For("audio", Target).Should().Be($"/Guilds/Soundboard/{Target}");
    }

    [Fact]
    public void For_AMalformedRouteTemplate_FallsBackToTheTab()
    {
        GuildSwitchTarget.For("reminders", Target, "Guilds/{guildId")
            .Should().Be($"/Guilds/Reminders/{Target}");
    }

    [Fact]
    public void BuildOptions_MarksTheCurrentGuildAndKeepsIdsAsStrings()
    {
        const ulong current = 123456789012345678UL;
        var guilds = new List<GuildSwitcherEntryDto>
        {
            new(current, "Pixel Pirates", null),
            new(Target, "Study Hall", "https://cdn.discordapp.com/icons/x.png")
        };

        var options = GuildSwitchTarget.BuildOptions(current, "audio", "Guilds/Soundboard/{guildId:long}", guilds);

        options.Should().HaveCount(2);
        options[0].IsCurrent.Should().BeTrue();
        options[0].GuildId.Should().Be("123456789012345678");
        options[1].IsCurrent.Should().BeFalse();
        options[1].Url.Should().Be($"/Guilds/Soundboard/{Target}");
        options[1].IconUrl.Should().Be("https://cdn.discordapp.com/icons/x.png");
    }
}
