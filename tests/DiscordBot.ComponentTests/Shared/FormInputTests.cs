using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class FormInputTests : TestContext
{
    [Fact]
    public void RendersLabel_HelpText_AndValue()
    {
        var cut = RenderComponent<FormInput>(p => p
            .Add(x => x.Id, "name")
            .Add(x => x.Label, "Display Name")
            .Add(x => x.HelpText, "Shown to other members")
            .Add(x => x.Value, "Rusty"));

        cut.Find("label[for=name]").TextContent.Should().Contain("Display Name");
        cut.Find("#name-help").TextContent.Trim().Should().Be("Shown to other members");
        cut.Find("input").GetAttribute("value").Should().Be("Rusty");
    }

    [Fact]
    public void ErrorState_RendersValidationMessage_AndAriaInvalid()
    {
        var cut = RenderComponent<FormInput>(p => p
            .Add(x => x.Id, "email")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Email is invalid"));

        cut.Find("#email-error").TextContent.Should().Contain("Email is invalid");
        cut.Find("input").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("input").GetAttribute("aria-describedby").Should().Be("email-error");
    }

    [Fact]
    public void Change_RaisesValueChanged()
    {
        string? bound = null;
        var cut = RenderComponent<FormInput>(p => p
            .Add(x => x.Value, "old")
            .Add(x => x.ValueChanged, (string? v) => bound = v));

        cut.Find("input").Change("new value");

        bound.Should().Be("new value");
    }

    [Fact]
    public void CharacterCount_RendersCurrentAndMaxLength()
    {
        var cut = RenderComponent<FormInput>(p => p
            .Add(x => x.Value, "abc")
            .Add(x => x.MaxLength, 10)
            .Add(x => x.ShowCharacterCount, true));

        cut.Markup.Should().Contain("3/10");
    }
}
