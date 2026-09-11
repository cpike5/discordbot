using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class TextAreaTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersLabelRowsAndValue()
    {
        var cut = Render<TextArea>(p => p
            .Add(x => x.Id, "msg")
            .Add(x => x.Label, "Message")
            .Add(x => x.Rows, 6)
            .Add(x => x.Value, "hello"));

        cut.Find("label").TextContent.Should().Be("Message");
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("rows").Should().Be("6");
        textarea.TextContent.Should().Be("hello");
    }

    [Fact]
    public void DefaultRows_IsFour()
    {
        var cut = Render<TextArea>(p => p.Add(x => x.Id, "x"));
        cut.Find("textarea").GetAttribute("rows").Should().Be("4");
    }

    [Fact]
    public void ValueChanged_FiresOnInput()
    {
        string? captured = null;
        var cut = Render<TextArea>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find("textarea").Input("multi\nline");

        captured.Should().Be("multi\nline");
    }

    [Fact]
    public void ShowCharacterCount_RendersLiveCount()
    {
        var cut = Render<TextArea>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.Value, "abc")
            .Add(x => x.MaxLength, 10)
            .Add(x => x.ShowCharacterCount, true));

        cut.Markup.Should().Contain("3/10");
    }

    [Fact]
    public void ValidationState_Error_AppliesBorderClass()
    {
        var cut = Render<TextArea>(p => p
            .Add(x => x.Id, "x")
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Bad"));

        cut.Find("textarea").ClassList.Should().Contain("border-error");
        cut.Markup.Should().Contain("Bad");
    }

    [Fact]
    public void IsDisabled_SetsDisabledAttribute()
    {
        var cut = Render<TextArea>(p => p.Add(x => x.Id, "x").Add(x => x.IsDisabled, true));
        cut.Find("textarea").HasAttribute("disabled").Should().BeTrue();
    }
}
