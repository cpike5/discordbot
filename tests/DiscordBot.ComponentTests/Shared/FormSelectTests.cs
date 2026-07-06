using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class FormSelectTests : TestContext
{
    private static readonly List<SelectOption> Options =
    [
        new SelectOption { Value = "a", Text = "Alpha" },
        new SelectOption { Value = "b", Text = "Beta" },
    ];

    [Fact]
    public void RendersOptions_WithSelectedValueMarked()
    {
        var cut = RenderComponent<FormSelect>(p => p
            .Add(x => x.Id, "pick")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, "b"));

        var options = cut.FindAll("option");
        options.Select(o => o.TextContent).Should().Contain(new[] { "Alpha", "Beta" });
        options.Single(o => o.GetAttribute("value") == "b").HasAttribute("selected").Should().BeTrue();
    }

    [Fact]
    public void Change_RaisesValueChanged()
    {
        string? bound = null;
        var cut = RenderComponent<FormSelect>(p => p
            .Add(x => x.Options, Options)
            .Add(x => x.ValueChanged, (string? v) => bound = v));

        cut.Find("select").Change("b");

        bound.Should().Be("b");
    }

    [Fact]
    public void ErrorState_RendersValidationMessage_AndHidesHelpText()
    {
        var cut = RenderComponent<FormSelect>(p => p
            .Add(x => x.Options, Options)
            .Add(x => x.HelpText, "pick one")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Selection required"));

        cut.Markup.Should().Contain("Selection required");
        cut.Markup.Should().NotContain("pick one");
    }
}
