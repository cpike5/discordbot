using System.Text.Json;
using DiscordBot.Agents;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="ToolOutcomes.Classify"/> — the convention that makes an expected tool
/// failure countable even though house style reports it as a successful result.
/// </summary>
public class ToolOutcomesTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public void Classify_ReturnsOk_ForAnOrdinarySuccessPayload()
    {
        ToolOutcomes.Classify(Json("""{"commands":["ping"],"count":1}"""))
            .Should().Be(ToolOutcomes.Ok);
    }

    [Fact]
    public void Classify_ReturnsFailedResult_ForATopLevelErrorString()
    {
        ToolOutcomes.Classify(Json("""{"error":"No such user"}"""))
            .Should().Be(ToolOutcomes.FailedResult);
    }

    [Fact]
    public void Classify_IgnoresAnEmptyErrorString()
    {
        // A tool that always emits `error` and leaves it blank on success must not read as failing.
        ToolOutcomes.Classify(Json("""{"error":"","count":3}"""))
            .Should().Be(ToolOutcomes.Ok);
    }

    [Fact]
    public void Classify_IgnoresANonStringErrorProperty()
    {
        ToolOutcomes.Classify(Json("""{"error":null,"count":3}"""))
            .Should().Be(ToolOutcomes.Ok);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("available")]
    [InlineData("found")]
    public void Classify_ReturnsFailedResult_ForAFalseFlag(string flag)
    {
        ToolOutcomes.Classify(Json($$"""{"{{flag}}":false}"""))
            .Should().Be(ToolOutcomes.FailedResult);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("available")]
    [InlineData("found")]
    public void Classify_ReturnsOk_ForATrueFlag(string flag)
    {
        ToolOutcomes.Classify(Json($$"""{"{{flag}}":true}"""))
            .Should().Be(ToolOutcomes.Ok);
    }

    [Fact]
    public void Classify_ReturnsOk_ForANonObjectPayload()
    {
        ToolOutcomes.Classify(Json("""["a","b"]""")).Should().Be(ToolOutcomes.Ok);
        ToolOutcomes.Classify(Json("42")).Should().Be(ToolOutcomes.Ok);
    }

    [Fact]
    public void Classify_OnlyLooksAtTopLevelProperties()
    {
        // A nested `success: false` belongs to one item in a list, not to the call.
        ToolOutcomes.Classify(Json("""{"results":[{"success":false}]}"""))
            .Should().Be(ToolOutcomes.Ok);
    }
}
