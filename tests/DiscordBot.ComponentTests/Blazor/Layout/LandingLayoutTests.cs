using Bunit;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// <see cref="LandingLayout"/> replaces Pages/Shared/_LayoutLanding.cshtml for the Landing page
/// specifically (_LayoutLanding itself stays, for Login - see ui-inventory.md). Same shape as
/// <see cref="EmptyLayoutTests"/>: App.razor already owns the document shell, so this layout has
/// no chrome of its own to assert beyond "renders the body".
/// </summary>
public class LandingLayoutTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersBody_WithNoChrome()
    {
        var cut = Render<LandingLayout>(parameters => parameters
            .Add(p => p.Body, "<p data-testid=\"layout-body\">Hello from the landing body</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Hello from the landing body");
    }
}
