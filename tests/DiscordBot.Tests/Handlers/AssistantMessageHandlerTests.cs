using Discord;
using DiscordBot.Bot.Handlers;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Handlers;

/// <summary>
/// The pure parts of <see cref="AssistantMessageHandler"/>: how a thread is named and how long it
/// stays open. The event flow itself is socket-object plumbing over <c>AssistantTriggerRules</c>,
/// which has its own tests.
/// </summary>
public class AssistantMessageHandlerTests
{
    private static IUser Author(string username = "someone", string? displayName = null)
    {
        if (displayName is null)
        {
            var user = new Mock<IUser>();
            user.SetupGet(u => u.Username).Returns(username);
            return user.Object;
        }

        var member = new Mock<IGuildUser>();
        member.SetupGet(u => u.Username).Returns(username);
        member.SetupGet(u => u.DisplayName).Returns(displayName);
        return member.Object;
    }

    [Fact]
    public void ThreadName_IsTheQuestionsFirstLine()
    {
        AssistantMessageHandler.ThreadName("How do I use the soundboard?\nand also tts", Author())
            .Should().Be("How do I use the soundboard?");
    }

    [Fact]
    public void ThreadName_LongQuestion_IsCutWithinDiscordsLimit()
    {
        var name = AssistantMessageHandler.ThreadName(new string('q', 300), Author());

        name.Length.Should().BeLessThanOrEqualTo(100);
        name.Should().EndWith("…");
    }

    [Fact]
    public void ThreadName_BlankQuestion_NamesTheMember_PreferringTheirServerName()
    {
        AssistantMessageHandler.ThreadName("   ", Author("chris", displayName: "Chris (mod)"))
            .Should().Be("Assistant · Chris (mod)");
        AssistantMessageHandler.ThreadName("", Author("chris"))
            .Should().Be("Assistant · chris");
    }

    [Theory]
    [InlineData(0, ThreadArchiveDuration.OneHour)]
    [InlineData(60, ThreadArchiveDuration.OneHour)]
    [InlineData(61, ThreadArchiveDuration.OneDay)]
    [InlineData(1440, ThreadArchiveDuration.OneDay)]
    [InlineData(3000, ThreadArchiveDuration.ThreeDays)]
    [InlineData(4320, ThreadArchiveDuration.ThreeDays)]
    [InlineData(4321, ThreadArchiveDuration.OneWeek)]
    [InlineData(99999, ThreadArchiveDuration.OneWeek)]
    public void ArchiveDuration_TakesTheSmallestDiscordOptionThatCovers(int minutes, ThreadArchiveDuration expected)
    {
        AssistantMessageHandler.ArchiveDuration(minutes).Should().Be(expected);
    }
}
