using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.OpenRouter;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for the tool-array measurement.
/// </summary>
/// <remarks>
/// The number has to be the one on the bill, not an approximation of it, which is why the assertions
/// here compare against the real wire serialization rather than against a literal: a change to the
/// request encoding should move the measurement with it rather than quietly making it wrong.
/// </remarks>
public class PromptSurfaceTests
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """{"type":"object","properties":{"id":{"type":"string","description":"An id."}},"required":["id"]}""")
        .RootElement.Clone();

    [Fact]
    public void Measure_WithNoTools_IsEmpty()
    {
        var measurement = PromptSurface.Measure(null);

        measurement.ToolCount.Should().Be(0);
        measurement.TotalChars.Should().Be(0);
        measurement.EstimatedTokens.Should().Be(0);
    }

    [Fact]
    public void Measure_WithAnEmptyList_IsEmptyRatherThanTwoBracketCharacters()
    {
        // A surface advertising nothing sends no tools field at all, so "[]" is not what it costs.
        PromptSurface.Measure(Array.Empty<LlmToolDefinition>()).TotalChars.Should().Be(0);
    }

    [Fact]
    public void Measure_TotalChars_IsExactlyWhatTheRequestCarries()
    {
        var tools = new List<LlmToolDefinition> { Tool("alpha"), Tool("beta") };

        var expected = JsonSerializer.Serialize(
            OpenRouterMessageMapper.ToOpenRouterTools(tools),
            OpenRouterJson.Options).Length;

        PromptSurface.Measure(tools).TotalChars.Should().Be(expected);
    }

    [Fact]
    public void Measure_PerToolChars_AddUpToTheArrayLessItsOwnPunctuation()
    {
        // n-1 commas and 2 brackets belong to no tool. Stating the identity here is what keeps the
        // shares meaningful: they are a tool's cost over the array's, not over an invented total.
        var tools = new List<LlmToolDefinition> { Tool("alpha"), Tool("beta"), Tool("gamma") };

        var measurement = PromptSurface.Measure(tools);

        measurement.Tools.Sum(t => t.SchemaChars)
            .Should().Be(measurement.TotalChars - (tools.Count - 1) - 2);
    }

    [Fact]
    public void Measure_ALongerDescription_CostsMore()
    {
        var cheap = PromptSurface.MeasureOne(Tool("alpha", "Short."));
        var dear = PromptSurface.MeasureOne(Tool("alpha", new string('x', 400)));

        dear.Should().BeGreaterThan(cheap);
        (dear - cheap).Should().Be(400 - "Short.".Length);
    }

    [Fact]
    public void Measure_Shares_AreProportionalAndSumToJustUnderOne()
    {
        var tools = new List<LlmToolDefinition>
        {
            Tool("alpha", "Short."),
            Tool("beta", new string('x', 500))
        };

        var measurement = PromptSurface.Measure(tools);

        var beta = measurement.Tools.Single(t => t.Name == "beta");
        var alpha = measurement.Tools.Single(t => t.Name == "alpha");

        beta.Share.Should().BeGreaterThan(alpha.Share);
        measurement.Tools.Sum(t => t.Share).Should().BeLessThan(1).And.BeGreaterThan(0.98);
    }

    [Fact]
    public void Measure_PreservesTheOrderItWasGiven()
    {
        // The caller hands in the composed array, which is already sorted by name because that order
        // is part of the prompt-cache prefix. Re-ordering here would invite a reader to think it was
        // arbitrary.
        var measurement = PromptSurface.Measure(new[] { Tool("zulu"), Tool("alpha") });

        measurement.Tools.Select(t => t.Name).Should().Equal("zulu", "alpha");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(4000, 1000)]
    public void EstimateTokens_IsCharactersOverFour(int chars, int expected)
    {
        PromptSurface.EstimateTokens(chars).Should().Be(expected);
    }

    [Fact]
    public void Measure_AToolWithNoSchema_StillCostsItsNameAndDescription()
    {
        // InputSchema defaults to an undefined JsonElement; the mapper substitutes an empty object
        // rather than omitting the field, and the measurement has to agree with it.
        var undefined = new LlmToolDefinition { Name = "alpha", Description = "Does a thing." };

        PromptSurface.MeasureOne(undefined).Should().BeGreaterThan("alpha".Length);
    }

    private static LlmToolDefinition Tool(string name, string? description = null) => new()
    {
        Name = name,
        Description = description ?? $"The {name} tool.",
        InputSchema = Schema
    };
}
