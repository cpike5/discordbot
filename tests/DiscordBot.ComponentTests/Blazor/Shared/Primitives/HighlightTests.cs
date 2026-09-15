using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class HighlightTests : BlazorComponentTestContext
{
    [Fact]
    public void MatchingSearchTerm_WrapsInMark()
    {
        var cut = Render<Highlight>(p => p.Add(x => x.Text, "The quick brown fox").Add(x => x.SearchTerm, "quick"));

        var mark = cut.Find("mark");
        mark.TextContent.Should().Be("quick");
        mark.ClassList.Should().Contain("search-highlight");
    }

    [Fact]
    public void NoSearchTerm_RendersPlainEncodedText_NoMark()
    {
        var cut = Render<Highlight>(p => p.Add(x => x.Text, "<script>alert(1)</script>"));

        cut.FindAll("mark").Should().BeEmpty();
        cut.FindAll("script").Should().BeEmpty();
        cut.Markup.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void MatchingText_IsHtmlEncoded_BeforeHighlighting()
    {
        var cut = Render<Highlight>(p => p.Add(x => x.Text, "<b>bold</b> search term").Add(x => x.SearchTerm, "search"));

        cut.FindAll("b").Should().BeEmpty();
        cut.Markup.Should().Contain("&lt;b&gt;");
        cut.Find("mark").TextContent.Should().Be("search");
    }

    [Fact]
    public void MaxLength_TruncatesText()
    {
        var text = new string('x', 200) + " needle";
        var cut = Render<Highlight>(p => p.Add(x => x.Text, text).Add(x => x.SearchTerm, "needle").Add(x => x.MaxLength, 20));

        cut.Markup.Should().Contain("...");
        cut.FindAll("mark").Should().BeEmpty(); // match is past the truncation point without ShowContext
    }

    [Fact]
    public void MaxLength_WithShowContext_KeepsMatchVisible()
    {
        // MaxLength must exceed TextHighlightHelper.HighlightWithContext's default contextChars
        // (20) for the excerpt window (matchIndex-contextChars .. +MaxLength) to actually reach
        // past the match's start index - a MaxLength <= 20 ends the excerpt exactly where the
        // match begins, excluding it (a real edge case in the underlying helper, not a rendering
        // bug in Highlight.razor).
        var text = new string('x', 200) + " needle";
        var cut = Render<Highlight>(p => p
            .Add(x => x.Text, text)
            .Add(x => x.SearchTerm, "needle")
            .Add(x => x.MaxLength, 60)
            .Add(x => x.ShowContext, true));

        cut.Find("mark").TextContent.Should().Be("needle");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Highlight>(p => p
            .Add(x => x.Text, "hello")
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-highlight"));

        var root = cut.Find("span");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-highlight");
    }
}
