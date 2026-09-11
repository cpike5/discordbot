using System.Text.Json;
using DiscordBot.Agents;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="ToolResultLimiter"/> — the shape of the truncation envelope, which is
/// prompt surface the model reads, and the pass-through guarantee for results within the cap.
/// </summary>
public class ToolResultLimiterTests
{
    [Fact]
    public void Cap_WithResultWithinTheCap_ReturnsItUnchanged()
    {
        var element = Parse("""{"tools":["a","b"],"count":2}""");

        var capped = ToolResultLimiter.Cap(element, 8000);

        capped.GetRawText().Should().Be(element.GetRawText());
    }

    [Fact]
    public void Cap_WithResultExactlyAtTheCap_ReturnsItUnchanged()
    {
        var element = Parse("""{"a":"bc"}""");

        var capped = ToolResultLimiter.Cap(element, element.GetRawText().Length);

        capped.GetRawText().Should().Be(element.GetRawText());
    }

    [Fact]
    public void Cap_WithOversizedResult_ReturnsTheTruncationEnvelope()
    {
        var raw = $$"""{"text":"{{new string('x', 200)}}"}""";
        var element = Parse(raw);

        var capped = ToolResultLimiter.Cap(element, 50);

        capped.GetProperty("truncated").GetBoolean().Should().BeTrue();
        capped.GetProperty("shown_chars").GetInt32().Should().Be(50);
        capped.GetProperty("total_chars").GetInt32().Should().Be(raw.Length);
        capped.GetProperty("content").GetString().Should().Be(raw[..50]);
        capped.GetProperty("message").GetString().Should().Be(ToolResultLimiter.TruncationMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cap_WithCapDisabled_ReturnsTheResultUnchanged(int maxChars)
    {
        var raw = $$"""{"text":"{{new string('x', 200)}}"}""";

        var capped = ToolResultLimiter.Cap(Parse(raw), maxChars);

        capped.GetRawText().Should().Be(raw);
    }

    [Fact]
    public void Cap_WithUndefinedElement_ReturnsItUnchanged()
    {
        // An uninitialized JsonElement has no raw text to measure, and serializing one throws.
        var capped = ToolResultLimiter.Cap(default, 10);

        capped.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void Cap_WhenTheCutWouldSplitASurrogatePair_StepsBackOneCharacter()
    {
        // "𝄞" is two UTF-16 chars; cutting between them would leave an unpaired half in the
        // envelope, which is not valid text to serialize.
        var raw = $$"""{"t":"{{new string('x', 20)}}𝄞"}""";
        var cutInsideThePair = raw.IndexOf('\uD834') + 1;

        var capped = ToolResultLimiter.Cap(Parse(raw), cutInsideThePair);

        capped.GetProperty("shown_chars").GetInt32().Should().Be(cutInsideThePair - 1);
        capped.GetProperty("content").GetString().Should().Be(raw[..(cutInsideThePair - 1)]);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
