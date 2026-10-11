using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Agents;

/// <summary>
/// The agent engine's own <see cref="Meter"/>: one measurement per model completion and per tool call.
/// </summary>
/// <remarks>
/// Like <see cref="AgentsActivitySource"/>, the engine is a leaf library with no DI-owned metrics
/// types, so the meter is static and the host subscribes to it by name. Spans already carry the same
/// detail, but spans are sampled; these counts are not. Token and cost totals per assistant request
/// are recorded by the host from the usage ledger, not here, so they are not double counted.
/// </remarks>
public static class AgentsMetrics
{
    /// <summary>The meter name the host subscribes to.</summary>
    public const string MeterName = "DiscordBot.Agents";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> CompletionCounter = Meter.CreateCounter<long>(
        name: "discordbot.agent.completion.count",
        unit: "{completions}",
        description: "Model completion calls made by the agent loop");

    private static readonly Histogram<double> CompletionDuration = Meter.CreateHistogram<double>(
        name: "discordbot.agent.completion.duration",
        unit: "ms",
        description: "Duration of one model completion call");

    private static readonly Counter<long> ToolCallCounter = Meter.CreateCounter<long>(
        name: "discordbot.agent.tool.count",
        unit: "{calls}",
        description: "Tool calls requested by the model, by tool and outcome");

    private static readonly Histogram<double> ToolCallDuration = Meter.CreateHistogram<double>(
        name: "discordbot.agent.tool.duration",
        unit: "ms",
        description: "Duration of one tool call");

    /// <summary>
    /// Records one completion call.
    /// </summary>
    /// <param name="model">The model slug that served the call, or the requested one when it failed.</param>
    /// <param name="status">"success", "failure" (a modeled failure) or "exception" (the client threw).</param>
    /// <param name="stopReason">The stop reason for a successful call; null otherwise.</param>
    /// <param name="elapsed">How long the call took.</param>
    public static void RecordCompletion(string? model, string status, string? stopReason, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { "model", string.IsNullOrWhiteSpace(model) ? "unknown" : model },
            { "status", status },
            { "stop_reason", stopReason ?? "none" }
        };

        CompletionCounter.Add(1, tags);
        CompletionDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    /// <summary>
    /// Records one tool call, including the ones that never ran (a refused repeat, an unknown tool).
    /// </summary>
    /// <param name="toolName">The model-facing tool name.</param>
    /// <param name="outcome">One of the <see cref="ToolOutcomes"/> values.</param>
    /// <param name="elapsed">How long the call took.</param>
    public static void RecordToolCall(string toolName, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            // A model can invent tool names; collapse those so the label set stays bounded.
            { "tool", outcome == ToolOutcomes.UnknownTool ? "unknown" : toolName },
            { "outcome", outcome }
        };

        ToolCallCounter.Add(1, tags);
        ToolCallDuration.Record(elapsed.TotalMilliseconds, tags);
    }
}
