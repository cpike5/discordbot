using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Shared.Tts;

/// <summary>
/// Unit tests for <see cref="SsmlMarkers"/>, the C# port of <c>wwwroot/js/ssml-markers.js</c>
/// (marker parsing/stripping) plus the marker-insertion helpers ported from
/// <c>_EmphasisToolbar.cshtml</c>'s <c>applyFormatting</c>.
/// </summary>
public class SsmlMarkersTests
{
    [Fact]
    public void ParseMarkers_PlainText_ReturnsSingleTextElement()
    {
        var elements = SsmlMarkers.ParseMarkers("Hello world");

        elements.Should().ContainSingle();
        elements[0].Type.Should().Be("text");
        elements[0].Text.Should().Be("Hello world");
    }

    [Fact]
    public void ParseMarkers_StrongEmphasis_ReturnsEmphasisElementWithStrongLevel()
    {
        var elements = SsmlMarkers.ParseMarkers("Say **this loudly** please");

        elements.Should().HaveCount(3);
        // BeEquivalentTo, not Be: MarkerElement is a record, and record equality compares
        // Attributes with Dictionary<,>'s default (reference) equality, so two structurally
        // identical-but-distinct dictionaries would never compare equal via Be().
        elements[0].Should().BeEquivalentTo(new SsmlMarkers.MarkerElement("text", "Say ", new Dictionary<string, string>()));
        elements[1].Type.Should().Be("emphasis");
        elements[1].Text.Should().Be("this loudly");
        elements[1].Attributes.Should().ContainKey("level").WhoseValue.Should().Be("strong");
        elements[2].Text.Should().Be(" please");
    }

    [Fact]
    public void ParseMarkers_ModerateEmphasis_DoesNotMatchStrongPattern()
    {
        var elements = SsmlMarkers.ParseMarkers("*softly*");

        elements.Should().ContainSingle();
        elements[0].Type.Should().Be("emphasis");
        elements[0].Text.Should().Be("softly");
        elements[0].Attributes["level"].Should().Be("moderate");
    }

    [Fact]
    public void ParseMarkers_SayAsCardinal_ReturnsSayAsElement()
    {
        var elements = SsmlMarkers.ParseMarkers("Room [# 42 #] please");

        var sayAs = elements.Single(e => e.Type == "say-as");
        sayAs.Text.Should().Be("42");
        sayAs.Attributes["interpret-as"].Should().Be("cardinal");
    }

    [Fact]
    public void ParseMarkers_SayAsDate_ReturnsSayAsElementWithDateInterpretation()
    {
        var elements = SsmlMarkers.ParseMarkers("[📅 January 1 📅]");

        var sayAs = elements.Single(e => e.Type == "say-as");
        sayAs.Text.Should().Be("January 1");
        sayAs.Attributes["interpret-as"].Should().Be("date");
    }

    [Fact]
    public void ParseMarkers_Break_ReturnsBreakElementWithNullTextAndDurationAttribute()
    {
        var elements = SsmlMarkers.ParseMarkers("Wait [⏸️ 500ms] now");

        var brk = elements.Single(e => e.Type == "break");
        brk.Text.Should().BeNull();
        brk.Attributes["duration"].Should().Be("500ms");
    }

    [Fact]
    public void ParseMarkers_MultipleMarkers_PreservesOrderAndInterveningText()
    {
        var elements = SsmlMarkers.ParseMarkers("**Hi** there [⏸️ 250ms] friend");

        elements.Select(e => e.Type).Should().Equal("emphasis", "text", "break", "text");
        elements[1].Text.Should().Be(" there ");
        elements[3].Text.Should().Be(" friend");
    }

    [Fact]
    public void ParseMarkers_EmptyString_ReturnsEmptyList()
    {
        SsmlMarkers.ParseMarkers(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void StripMarkers_StrongAndModerateEmphasis_ReplacedWithInnerText()
    {
        SsmlMarkers.StripMarkers("**loud** and *soft*").Should().Be("loud and soft");
    }

    [Fact]
    public void StripMarkers_SayAsMarkers_ReplacedWithInnerText()
    {
        SsmlMarkers.StripMarkers("[# 42 #] and [📅 May 5 📅]").Should().Be("42 and May 5");
    }

    [Fact]
    public void StripMarkers_BreakMarker_RemovedEntirely()
    {
        SsmlMarkers.StripMarkers("Wait [⏸️ 500ms] now").Should().Be("Wait  now");
    }

    [Fact]
    public void StripMarkers_NoMarkers_ReturnsInputUnchanged()
    {
        SsmlMarkers.StripMarkers("Plain text").Should().Be("Plain text");
    }

    [Theory]
    [InlineData("strong", "hi", "**hi**")]
    [InlineData("moderate", "hi", "*hi*")]
    [InlineData("anything-else", "hi", "*hi*")]
    public void WrapEmphasis_WrapsAccordingToLevel(string level, string text, string expected)
    {
        SsmlMarkers.WrapEmphasis(text, level).Should().Be(expected);
    }

    [Fact]
    public void PauseMarker_FormatsDurationInMilliseconds()
    {
        SsmlMarkers.PauseMarker(500).Should().Be("[⏸️ 500ms]");
    }

    [Fact]
    public void WrapSayAs_Date_UsesCalendarMarker()
    {
        SsmlMarkers.WrapSayAs("May 5", "date").Should().Be("[📅 May 5 📅]");
    }

    [Theory]
    [InlineData("cardinal")]
    [InlineData("ordinal")]
    public void WrapSayAs_NonDate_UsesCardinalMarker(string interpretAs)
    {
        SsmlMarkers.WrapSayAs("42", interpretAs).Should().Be("[# 42 #]");
    }

    [Fact]
    public void RoundTrip_WrapThenParse_RecoversOriginalTypeAndText()
    {
        var wrapped = SsmlMarkers.WrapEmphasis("urgent", "strong");
        var elements = SsmlMarkers.ParseMarkers(wrapped);

        elements.Should().ContainSingle();
        elements[0].Type.Should().Be("emphasis");
        elements[0].Text.Should().Be("urgent");
        elements[0].Attributes["level"].Should().Be("strong");
    }

    [Fact]
    public void RoundTrip_WrapThenStrip_RecoversPlainText()
    {
        var wrapped = SsmlMarkers.WrapSayAs("100", "cardinal");
        SsmlMarkers.StripMarkers(wrapped).Should().Be("100");
    }
}
