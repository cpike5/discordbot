using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class SsmlPreviewTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersHighlightedSsmlInsideCodeBlock()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.Ssml, "<speak>hi</speak>"));

        var code = cut.Find(".ssml-preview__code");
        code.InnerHtml.Should().Contain("ssml-tag");
        code.TextContent.Should().Contain("speak");
    }

    [Fact]
    public void StartCollapsed_True_ContentHasCollapsedClass()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.StartCollapsed, true));

        cut.Find(".ssml-preview__content").ClassList.Should().Contain("collapsed");
        cut.Find(".ssml-preview__header").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void ClickHeader_TogglesCollapsedState()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.StartCollapsed, true));

        cut.Find(".ssml-preview__header").Click();

        cut.Find(".ssml-preview__content").ClassList.Should().NotContain("collapsed");
        cut.Find(".ssml-preview__header").GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void CharacterCount_ShowsOverheadPercentage()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.Ssml, "0123456789").Add(x => x.PlainLength, 5));

        cut.Find(".ssml-preview__char-count").TextContent.Should().Be("10 chars (5 plain text, +100% markup)");
    }

    [Fact]
    public void EmptySsml_CharacterCountShowsZeroChars()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.Ssml, string.Empty));

        cut.Find(".ssml-preview__char-count").TextContent.Should().Be("0 chars");
    }

    [Fact]
    public void ShowCharacterCount_False_HidesCountBadge()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.ShowCharacterCount, false));

        cut.FindAll(".ssml-preview__char-count").Should().BeEmpty();
    }

    [Fact]
    public void CopyButton_CopiesToClipboard_AndInvokesOnCopy()
    {
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<bool>("copyToClipboard", _ => true).SetResult(true);

        var copied = false;
        var cut = Render<SsmlPreview>(p => p
            .Add(x => x.Ssml, "<speak>hi</speak>")
            .Add(x => x.OnCopy, EventCallback.Factory.Create(this, () => copied = true)));

        cut.Find(".ssml-preview__copy-btn").Click();

        copied.Should().BeTrue();
    }

    [Fact]
    public void CopyButton_FailedClipboardWrite_DoesNotInvokeOnCopy()
    {
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<bool>("copyToClipboard", _ => true).SetResult(false);

        var copied = false;
        var cut = Render<SsmlPreview>(p => p
            .Add(x => x.Ssml, "<speak>hi</speak>")
            .Add(x => x.OnCopy, EventCallback.Factory.Create(this, () => copied = true)));

        cut.Find(".ssml-preview__copy-btn").Click();

        copied.Should().BeFalse();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<SsmlPreview>(p => p.Add(x => x.Class, "extra-class").AddUnmatched("data-testid", "sp"));

        var root = cut.Find(".ssml-preview");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("sp");
    }
}
