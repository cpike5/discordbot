using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class CommandHeaderTests : TestContext
{
    [Fact]
    public void RendersTitleAndSubtitle()
    {
        var cut = RenderComponent<CommandHeader>(p => p
            .Add(x => x.Title, "Commands")
            .Add(x => x.Subtitle, "Browse registered commands"));

        cut.Find("h1").TextContent.Trim().Should().Be("Commands");
        cut.Find("[data-command-subtitle]").TextContent.Trim().Should().Be("Browse registered commands");
    }

    [Fact]
    public void OmitsSubtitle_WhenEmpty()
    {
        var cut = RenderComponent<CommandHeader>(p => p
            .Add(x => x.Title, "Commands"));

        cut.FindAll("[data-command-subtitle]").Should().BeEmpty();
    }
}
