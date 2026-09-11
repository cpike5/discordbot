using Bunit;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Layout;

public class EmptyLayoutTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersBody_WithNoChrome()
    {
        var cut = Render<EmptyLayout>(parameters => parameters
            .Add(p => p.Body, "<p data-testid=\"layout-body\">Hello from the body</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Hello from the body");
    }
}
