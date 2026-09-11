using System.Text.Json;
using DiscordBot.Agents;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="ToolResults"/>. The point of these helpers is that a tool written
/// through them satisfies the <see cref="ToolOutcomes.Classify"/> convention without its author
/// having to know the convention exists, so most of these assert exactly that.
/// </summary>
public class ToolResultsTests
{
    [Fact]
    public void Json_IsASuccessfulResultCarryingThePayload()
    {
        var result = ToolResults.Json(new { total_count = 3 });

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Data!.Value.GetProperty("total_count").GetInt32().Should().Be(3);
    }

    [Fact]
    public void Json_OmitsNullMembers()
    {
        var result = ToolResults.Json(new { tag = (string?)null, id = 1 });

        result.Data!.Value.TryGetProperty("tag", out _).Should().BeFalse();
    }

    [Fact]
    public void Json_WritesMemberNamesVerbatim()
    {
        // No naming policy: the names in the code are the names on the wire.
        ToolResults.Json(new { note_id = 7 }).Data!.Value
            .TryGetProperty("note_id", out _).Should().BeTrue();
    }

    [Fact]
    public void Error_IsASuccessfulResultThatClassifiesAsAnExpectedFailure()
    {
        // This is the whole reason ToolResults exists. An expected failure has to reach the model as
        // readable content rather than as "Error: ...", and still be countable in the traces.
        var result = ToolResults.Error("Missing required parameter: content");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Data!.Value.GetProperty("error").GetString()
            .Should().Be("Missing required parameter: content");
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
    }

    [Fact]
    public void NotFound_ClassifiesAsAnExpectedFailure()
    {
        var result = ToolResults.NotFound("Note with ID 41 not found.");

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("found").GetBoolean().Should().BeFalse();
        result.Data!.Value.GetProperty("error").GetString().Should().Contain("41");
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
    }

    [Fact]
    public void Truncated_MarksThePayloadWithoutLosingIt()
    {
        var result = ToolResults.Truncated(
            new { notes = new[] { 1, 2 }, total_count = 2 },
            "Showing 2 of 400 notes; narrow with `tag`.");

        var payload = result.Data!.Value;
        payload.GetProperty("total_count").GetInt32().Should().Be(2);
        payload.GetProperty("truncated").GetBoolean().Should().BeTrue();
        payload.GetProperty("truncation_note").GetString().Should().Contain("400");
    }

    [Fact]
    public void Truncated_ReadsAsASuccess_BecauseAPartialAnswerIsStillAnAnswer()
    {
        var result = ToolResults.Truncated(new { rows = Array.Empty<int>() }, "clipped");

        result.Success.Should().BeTrue();
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.Ok);
    }

    [Fact]
    public void Truncated_WrapsAPayloadThatIsNotAnObject()
    {
        var result = ToolResults.Truncated(new[] { 1, 2, 3 }, "clipped");

        var payload = result.Data!.Value;
        payload.GetProperty("result").GetArrayLength().Should().Be(3);
        payload.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Forbidden_IsASuccessfulResultCarryingADirective()
    {
        var result = ToolResults.Forbidden("save notes");

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("forbidden").GetBoolean().Should().BeTrue();
        result.Data!.Value.GetProperty("message").GetString().Should().Contain("save notes");
    }

    [Fact]
    public void Failed_IsTheOneShapeThatActuallyFlagsAnError()
    {
        var result = ToolResults.Failed("The note store is unreachable.");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The note store is unreachable.");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void ToolJson_Element_UsesTheCompactSettings()
    {
        var element = ToolJson.Element(new { a = 1, b = (string?)null });

        JsonSerializer.Serialize(element).Should().Be("""{"a":1}""");
    }
}
