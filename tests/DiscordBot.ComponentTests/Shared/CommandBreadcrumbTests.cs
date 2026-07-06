using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class CommandBreadcrumbTests : TestContext
{
    [Fact]
    public void RendersHomeAndCommandsLinks()
    {
        var cut = RenderComponent<CommandBreadcrumb>();

        var hrefs = cut.FindAll("a").Select(a => a.GetAttribute("href")).ToList();
        hrefs.Should().Equal("/", "/Commands");
    }

    [Fact]
    public void RendersActiveTabDisplayName()
    {
        var cut = RenderComponent<CommandBreadcrumb>(p => p
            .Add(x => x.ActiveTab, "execution-logs"));

        cut.Find("[data-command-breadcrumb-active]").TextContent.Trim().Should().Be("Execution Logs");
    }

    [Fact]
    public void UnknownTab_FallsBackToCommandList()
    {
        var cut = RenderComponent<CommandBreadcrumb>(p => p
            .Add(x => x.ActiveTab, "nope"));

        cut.Find("[data-command-breadcrumb-active]").TextContent.Trim().Should().Be("Command List");
    }
}
