using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.OpenRouter;

namespace DiscordBot.Agents;

/// <summary>
/// Measures what a tool array costs: the bytes each tool contributes to the request, and the whole
/// array's size in characters and estimated tokens.
/// </summary>
/// <remarks>
/// <para>
/// The tool array serializes at position 0 of every request, so it is paid for on every question
/// whether or not the question needs any of it. Nothing measured it until this, which meant the
/// array's growth was discovered on the bill. A number per tool turns "should we add this one?"
/// into a decision.
/// </para>
/// <para>
/// The measurement is the real wire encoding — <see cref="OpenRouterMessageMapper.ToOpenRouterTools"/>
/// through <see cref="OpenRouterJson.Options"/> — rather than an approximation of it, because the
/// point is to be comparable with the bill. Tokens are characters ÷ 4, which is close enough for an
/// order-of-magnitude answer and needs no tokenizer dependency; treat it as a ratio, not a count.
/// </para>
/// </remarks>
public static class PromptSurface
{
    /// <summary>Characters per token, for the estimate. English prose and JSON both land near 4.</summary>
    private const int CharsPerToken = 4;

    /// <summary>
    /// Measures <paramref name="tools"/> as they would be sent.
    /// </summary>
    /// <param name="tools">
    /// The tools a surface advertises — the composed, skill-aware set, not everything a registry
    /// holds. A tool hidden behind a skill is not in the per-request prefix and must not be counted
    /// as though it were.
    /// </param>
    /// <returns>The measurement; empty when there are no tools.</returns>
    public static PromptSurfaceMeasurement Measure(IEnumerable<LlmToolDefinition>? tools)
    {
        var definitions = tools?.ToList() ?? new List<LlmToolDefinition>();

        if (definitions.Count == 0)
        {
            return PromptSurfaceMeasurement.Empty;
        }

        var totalChars = Serialize(definitions).Length;

        var measured = definitions
            .Select(definition => new PromptSurfaceTool(
                definition.Name,
                MeasureOne(definition),
                0))
            .ToList();

        var withShares = measured
            .Select(tool => tool with { Share = totalChars == 0 ? 0 : (double)tool.SchemaChars / totalChars })
            .ToList();

        return new PromptSurfaceMeasurement(withShares, totalChars, EstimateTokens(totalChars));
    }

    /// <summary>
    /// The characters one tool contributes to the array, on its own.
    /// </summary>
    /// <param name="definition">The tool definition to measure.</param>
    public static int MeasureOne(LlmToolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // The array's own brackets and separators are excluded: they belong to no single tool, which
        // is why the per-tool shares sum to slightly under 1.
        var element = Serialize(new List<LlmToolDefinition> { definition });

        return element.Length - "[]".Length;
    }

    /// <summary>Characters ÷ 4, rounded to nearest — the estimate, stated once.</summary>
    /// <param name="chars">Character count to convert.</param>
    public static int EstimateTokens(int chars) =>
        (int)Math.Round(chars / (double)CharsPerToken, MidpointRounding.AwayFromZero);

    private static string Serialize(List<LlmToolDefinition> definitions) =>
        JsonSerializer.Serialize(
            OpenRouterMessageMapper.ToOpenRouterTools(definitions),
            OpenRouterJson.Options);
}

/// <summary>What one tool costs in the request's tool array.</summary>
/// <param name="Name">The model-facing tool name.</param>
/// <param name="SchemaChars">Characters this tool's name, description and schema serialize to.</param>
/// <param name="Share">
/// This tool's fraction of the whole array. Shares sum to slightly under 1 — the array's own
/// brackets and commas are part of the total and belong to no tool.
/// </param>
public sealed record PromptSurfaceTool(string Name, int SchemaChars, double Share);

/// <summary>What a surface's advertised tool array costs.</summary>
/// <param name="Tools">Per-tool costs, in the order measured.</param>
/// <param name="TotalChars">Characters the whole array serializes to, brackets and commas included.</param>
/// <param name="EstimatedTokens">
/// <paramref name="TotalChars"/> ÷ 4. An estimate, and deliberately a cheap one: the useful question
/// is whether a tool is 2% or 20% of the prefix, and that ratio survives a rough tokenizer.
/// </param>
public sealed record PromptSurfaceMeasurement(
    IReadOnlyList<PromptSurfaceTool> Tools,
    int TotalChars,
    int EstimatedTokens)
{
    /// <summary>A surface advertising nothing.</summary>
    public static PromptSurfaceMeasurement Empty { get; } =
        new(Array.Empty<PromptSurfaceTool>(), 0, 0);

    /// <summary>How many tools the surface advertises.</summary>
    public int ToolCount => Tools.Count;
}
