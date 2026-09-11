using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class TextInputTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersLabelAndPlaceholder()
    {
        var cut = Render<TextInput>(p => p.Add(x => x.Id, "name").Add(x => x.Label, "Name").Add(x => x.Placeholder, "Enter name"));

        cut.Find("label").TextContent.Should().Be("Name");
        var input = cut.Find("input");
        input.GetAttribute("id").Should().Be("name");
        input.GetAttribute("placeholder").Should().Be("Enter name");
    }

    [Fact]
    public void Standalone_ValueAndValueChanged_RoundTrip_WithoutEditContext()
    {
        // Simulates what @bind-Value expands to for a plain Value/ValueChanged pair - proves
        // TextInput works with no ancestor EditForm/EditContext at all.
        string? bound = "initial";
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.Value, bound)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        cut.Find("input").GetAttribute("value").Should().Be("initial");

        cut.Find("input").Input("changed");

        bound.Should().Be("changed");
    }

    [Fact]
    public void ValueChanged_FiresOnInput()
    {
        string? captured = null;
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find("input").Input("hello");

        captured.Should().Be("hello");
    }

    [Theory]
    [InlineData(ValidationState.Error, "border-error")]
    [InlineData(ValidationState.Success, "border-success")]
    [InlineData(ValidationState.Warning, "border-warning")]
    public void ValidationState_AppliesBorderClass_AndMessage(ValidationState state, string expectedClass)
    {
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.ValidationState, state)
            .Add(x => x.ValidationMessage, "A message"));

        cut.Find("input").ClassList.Should().Contain(expectedClass);
        cut.Markup.Should().Contain("A message");
    }

    [Fact]
    public void ShowCharacterCount_RendersLiveCountFromValue()
    {
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.Value, "hello")
            .Add(x => x.MaxLength, 20)
            .Add(x => x.ShowCharacterCount, true));

        cut.Markup.Should().Contain("5/20");

        cut.Find("input").Input("hello world");

        cut.Markup.Should().Contain("11/20");
    }

    [Fact]
    public void ShowCharacterCount_False_DoesNotRenderCount()
    {
        var cut = Render<TextInput>(p => p.Add(x => x.Id, "x").Add(x => x.MaxLength, 20).Add(x => x.ShowCharacterCount, false));
        cut.Markup.Should().NotContain("/20");
    }

    [Fact]
    public void IconLeft_And_IconRight_RenderIcons()
    {
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.IconLeft, IconPaths.MagnifyingGlass)
            .Add(x => x.IconRight, IconPaths.XMark));

        cut.FindAll("svg").Should().HaveCount(2);
        cut.Find("input").ClassList.Should().Contain("pl-10").And.Contain("pr-10");
    }

    [Fact]
    public void IsRequired_SetsRequiredAndAriaRequired_AndAsterisk()
    {
        var cut = Render<TextInput>(p => p.Add(x => x.Id, "x").Add(x => x.Label, "L").Add(x => x.IsRequired, true));

        var input = cut.Find("input");
        input.HasAttribute("required").Should().BeTrue();
        input.GetAttribute("aria-required").Should().Be("true");
        cut.Find("label").TextContent.Should().Contain("*");
    }

    [Fact]
    public void IsDisabled_SetsDisabledAttribute_AndClasses()
    {
        var cut = Render<TextInput>(p => p.Add(x => x.Id, "x").Add(x => x.IsDisabled, true));

        var input = cut.Find("input");
        input.HasAttribute("disabled").Should().BeTrue();
        input.ClassList.Should().Contain("cursor-not-allowed");
    }

    [Fact]
    public void AriaDescribedBy_CombinesHelpAndError()
    {
        var cut = Render<TextInput>(p => p
            .Add(x => x.Id, "field")
            .Add(x => x.HelpText, "help")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "bad"));

        cut.Find("input").GetAttribute("aria-describedby").Should().Be("field-error");
    }

    [Fact]
    public void EditForm_DataAnnotationsValidator_ShowsValidationMessage_OnInvalidSubmit()
    {
        var model = new TextInputEditFormHost.FormModel();
        var cut = Render<TextInputEditFormHost>(p => p.Add(x => x.Model, model));

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("This field is required.");
    }

    [Fact]
    public void EditForm_DataAnnotationsValidator_AppliesInvalidClass_OnInvalidSubmit()
    {
        var model = new TextInputEditFormHost.FormModel();
        var cut = Render<TextInputEditFormHost>(p => p.Add(x => x.Model, model));

        var input = cut.Find("input");
        input.ClassList.Should().NotContain("invalid");

        cut.Find("form").Submit();

        cut.Find("input").ClassList.Should().Contain("invalid");
    }
}
