using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class FormFieldTests : BlazorComponentTestContext
{
    [Fact]
    public void Label_RendersWithForAttribute_AndRequiredMarker()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Username")
            .Add(x => x.For, "the-input")
            .Add(x => x.IsRequired, true));

        var label = cut.Find("label");
        label.GetAttribute("for").Should().Be("the-input");
        label.TextContent.Should().Contain("Username").And.Contain("*");
    }

    [Fact]
    public void NoLabel_RendersNoLabelElement()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "x"));
        cut.FindAll("label").Should().BeEmpty();
    }

    [Fact]
    public void ChildContent_IsRendered()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "x").AddChildContent("<input id=\"x\" />"));
        cut.Find("input").Should().NotBeNull();
    }

    [Fact]
    public void HelpText_Renders_WhenValidationStateIsNone()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "f").Add(x => x.HelpText, "Some help"));
        cut.Find($"#{"f"}-help").TextContent.Should().Be("Some help");
    }

    [Fact]
    public void HelpText_DoesNotRender_WhenValidationStateIsNotNone()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.For, "f")
            .Add(x => x.HelpText, "Some help")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Bad"));

        cut.FindAll("#f-help").Should().BeEmpty();
    }

    [Theory]
    [InlineData(ValidationState.Error, "f-error", "text-error")]
    [InlineData(ValidationState.Success, "f-success", "text-success")]
    [InlineData(ValidationState.Warning, "f-warning", "text-warning")]
    public void ValidationMessage_RendersMatchingStateBlock(ValidationState state, string expectedId, string expectedClass)
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.For, "f")
            .Add(x => x.ValidationState, state)
            .Add(x => x.ValidationMessage, "A message"));

        var message = cut.Find($"#{expectedId}");
        message.TextContent.Should().Contain("A message");
        message.ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void ValidationState_None_RendersNoValidationBlock()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "f").Add(x => x.ValidationMessage, "unused"));
        cut.FindAll("p[role='alert']").Should().BeEmpty();
    }

    [Fact]
    public void FooterContent_RendersAfterValidationMessage()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.For, "f")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Bad")
            .Add(x => x.FooterContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='footer'>Footer</span>"))));

        var footer = cut.Find("[data-testid='footer']");
        footer.TextContent.Should().Be("Footer");

        // Footer must come after the validation message in document order.
        var errorIndex = cut.Markup.IndexOf("f-error", StringComparison.Ordinal);
        var footerIndex = cut.Markup.IndexOf("data-testid='footer'", StringComparison.Ordinal);
        footerIndex.Should().BeGreaterThan(errorIndex);
    }

    [Fact]
    public void Class_IsAppendedToRootDiv()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "f").Add(x => x.Class, "extra-class"));
        cut.Find("div").ClassList.Should().Contain("extra-class");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<FormField>(p => p.Add(x => x.For, "f").AddUnmatched("data-testid", "field"));
        cut.Find("div[data-testid='field']").Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, ValidationState.None, null, null)]
    [InlineData("help", ValidationState.None, null, "id-help")]
    [InlineData("help", ValidationState.Error, "msg", "id-error")] // help is suppressed once a validation message is showing
    public void ComputeDescribedByIds_MatchesFormInputCshtmlRules(string? helpText, ValidationState state, string? message, string? expected)
    {
        FormField.ComputeDescribedByIds("id", helpText, state, message).Should().Be(expected);
    }

    [Fact]
    public void ComputeDescribedByIds_ReturnsNull_WhenIdIsEmpty()
    {
        FormField.ComputeDescribedByIds(null, "help", ValidationState.None, null).Should().BeNull();
    }
}
