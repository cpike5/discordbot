using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class GuildAvatarTests
{
    [Fact]
    public void ClassFor_SameGuild_GivesTheSameClassEveryTime()
    {
        GuildAvatar.ClassFor(123456789012345678UL).Should().Be(GuildAvatar.ClassFor(123456789012345678UL));
    }

    [Fact]
    public void ClassFor_ConsecutiveGuilds_CycleThroughFiveDistinctTokenClasses()
    {
        var classes = Enumerable.Range(0, 5).Select(i => GuildAvatar.ClassFor((ulong)i)).ToList();

        classes.Should().OnlyHaveUniqueItems();
        classes.Should().OnlyContain(c => c.StartsWith("bg-"));
        GuildAvatar.ClassFor(5).Should().Be(classes[0]);
    }

    [Fact]
    public void ClassFor_LargestSnowflake_DoesNotOverflow()
    {
        var act = () => GuildAvatar.ClassFor(ulong.MaxValue);

        act.Should().NotThrow();
    }
}
