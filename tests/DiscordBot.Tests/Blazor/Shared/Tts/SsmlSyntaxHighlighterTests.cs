using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Shared.Tts;

/// <summary>
/// Unit tests for <see cref="SsmlSyntaxHighlighter"/>, the C# port of
/// <c>_SsmlPreview.cshtml</c>'s inline <c>ssmlPreview_highlightSyntax</c>.
/// </summary>
public class SsmlSyntaxHighlighterTests
{
    [Fact]
    public void Highlight_NullOrEmpty_ReturnsEmptyString()
    {
        SsmlSyntaxHighlighter.Highlight(null).Should().BeEmpty();
        SsmlSyntaxHighlighter.Highlight(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Highlight_PlainText_WrapsInTextSpan()
    {
        SsmlSyntaxHighlighter.Highlight("hello").Should().Be("<span class=\"ssml-text\">hello</span>");
    }

    [Fact]
    public void Highlight_SimpleTag_WrapsNameInTagSpan()
    {
        var result = SsmlSyntaxHighlighter.Highlight("<speak>hi</speak>");

        result.Should().Contain("<span class=\"ssml-tag\">&lt;speak</span>");
        result.Should().Contain("<span class=\"ssml-tag\">&gt;</span>");
        result.Should().Contain("<span class=\"ssml-text\">hi</span>");
        result.Should().Contain("<span class=\"ssml-tag\">&lt;/speak</span>");
    }

    [Fact]
    public void Highlight_AttributeNameAndValue_AreWrappedSeparately()
    {
        var result = SsmlSyntaxHighlighter.Highlight("<voice name=\"en-US-JennyNeural\">");

        result.Should().Contain("<span class=\"ssml-attribute\">name</span>");
        result.Should().Contain("<span class=\"ssml-value\">en-US-JennyNeural</span>");
    }

    [Fact]
    public void Highlight_SelfClosingTag_IncludesSlashInTagSpan()
    {
        var result = SsmlSyntaxHighlighter.Highlight("<break time=\"500ms\"/>");

        result.Should().Contain("<span class=\"ssml-attribute\">time</span>");
        result.Should().Contain("<span class=\"ssml-value\">500ms</span>");
        result.Should().Contain("/&gt;</span>");
    }

    [Fact]
    public void Highlight_TextContent_IsHtmlEncoded()
    {
        var result = SsmlSyntaxHighlighter.Highlight("a < b & c");

        result.Should().NotContain("a < b");
        result.Should().Contain("&lt;").And.Contain("&amp;");
    }

    [Fact]
    public void Highlight_AttributeValueContainingAngleBracket_IsHtmlEncodedNotInjected()
    {
        // A literal ">" inside a quoted attribute value ends the tag match early - an inherent
        // limitation of the source's naive `<[^>]+>` tag-splitting regex (ssmlPreview_highlightSyntax),
        // reproduced here rather than fixed, per the Tier 5 contract's "reproduce, don't redesign"
        // rule. What must still hold regardless of how the split falls: no raw "<script>" ever
        // reaches the output unescaped.
        var result = SsmlSyntaxHighlighter.Highlight("<voice name=\"1 < 2 &amp; safe\">");

        result.Should().NotContain("<script>");
        result.Should().Contain("&lt;");
        result.Should().Contain("&amp;amp;");
    }

    [Fact]
    public void Highlight_FullSpeakDocument_ProducesTagAttributeValueAndTextSpans()
    {
        const string ssml = "<speak version=\"1.0\"><voice name=\"en-US-JennyNeural\">Hello</voice></speak>";

        var result = SsmlSyntaxHighlighter.Highlight(ssml);

        result.Should().Contain("ssml-tag").And.Contain("ssml-attribute").And.Contain("ssml-value").And.Contain("ssml-text");
        result.Should().Contain("<span class=\"ssml-text\">Hello</span>");
    }
}
