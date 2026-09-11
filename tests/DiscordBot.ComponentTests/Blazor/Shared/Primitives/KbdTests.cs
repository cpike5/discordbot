using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class KbdTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersChildContent_WithKbdClass()
    {
        var cut = Render<Kbd>(p => p.AddChildContent("K"));

        var kbd = cut.Find("kbd");
        kbd.TextContent.Should().Be("K");
        kbd.ClassList.Should().Contain("kbd");
    }

    [Fact]
    public void Class_IsAppended()
    {
        var cut = Render<Kbd>(p => p.AddChildContent("Ctrl").Add(x => x.Class, "extra"));
        cut.Find("kbd").ClassList.Should().Contain("extra").And.Contain("kbd");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Kbd>(p => p.AddChildContent("K").AddUnmatched("data-testid", "my-kbd"));
        cut.Find("kbd[data-testid='my-kbd']").Should().NotBeNull();
    }
}
