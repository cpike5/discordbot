using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Metrics for assistant requests: volume, latency, token usage and cost, per assistant mode and model.
/// </summary>
/// <remarks>
/// Recorded from the usage ledger (<see cref="Services.LLM.LlmUsageRecorder"/>), which every
/// assistant surface writes one row to per request, so the numbers match the portal's usage pages.
/// Per-completion and per-tool counts come from the engine's own meter (<c>DiscordBot.Agents</c>).
/// </remarks>
public sealed class LlmMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Llm";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _tokens;
    private readonly Counter<double> _cost;
    private readonly Counter<long> _completions;
    private readonly Counter<long> _toolCalls;
    private readonly Counter<long> _usageRecordsDropped;
    private Func<int>? _usageQueueDepth;

    public LlmMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _requests = _meter.CreateCounter<long>(
            name: "discordbot.llm.request.count",
            unit: "{requests}",
            description: "Assistant requests, by mode, model and status");

        _requestDuration = _meter.CreateHistogram<double>(
            name: "discordbot.llm.request.duration",
            unit: "ms",
            description: "End-to-end assistant request latency, including tool calls");

        _tokens = _meter.CreateCounter<long>(
            name: "discordbot.llm.tokens",
            unit: "{tokens}",
            description: "Tokens consumed, by type (input, output, cached, cache_write)");

        _cost = _meter.CreateCounter<double>(
            name: "discordbot.llm.cost.usd",
            unit: "{USD}",
            description: "Assistant spend in US dollars");

        _completions = _meter.CreateCounter<long>(
            name: "discordbot.llm.completions",
            unit: "{completions}",
            description: "Model completion calls made on behalf of assistant requests");

        _toolCalls = _meter.CreateCounter<long>(
            name: "discordbot.llm.tool_calls",
            unit: "{calls}",
            description: "Tool calls made on behalf of assistant requests");

        _usageRecordsDropped = _meter.CreateCounter<long>(
            name: "discordbot.llm.usage_records.dropped",
            unit: "{records}",
            description: "Usage ledger rows dropped because the write queue was full");

        _meter.CreateObservableGauge(
            name: "discordbot.llm.usage_queue.depth",
            observeValue: () => _usageQueueDepth?.Invoke() ?? 0,
            unit: "{records}",
            description: "Usage ledger rows waiting to be written");
    }

    /// <summary>Supplies the usage ledger's queue depth for the depth gauge.</summary>
    public void ObserveUsageQueue(Func<int> depth) => _usageQueueDepth = depth;

    /// <summary>Records one assistant request from its usage ledger row.</summary>
    public void RecordUsage(LlmUsageRecord record)
    {
        var mode = record.Mode.ToString();
        var model = string.IsNullOrWhiteSpace(record.Model) ? "unknown" : record.Model;

        var requestTags = new TagList
        {
            { "mode", mode },
            { "model", model },
            { "status", record.Success ? "success" : "failure" }
        };
        _requests.Add(1, requestTags);
        _requestDuration.Record(record.LatencyMs, requestTags);

        AddTokens(mode, model, "input", record.InputTokens);
        AddTokens(mode, model, "output", record.OutputTokens);
        AddTokens(mode, model, "cached", record.CachedTokens);
        AddTokens(mode, model, "cache_write", record.CacheWriteTokens);

        var modelTags = new TagList { { "mode", mode }, { "model", model } };
        if (record.CostUsd > 0)
        {
            _cost.Add((double)record.CostUsd, modelTags);
        }

        if (record.LlmCalls > 0)
        {
            _completions.Add(record.LlmCalls, modelTags);
        }

        if (record.ToolCalls > 0)
        {
            _toolCalls.Add(record.ToolCalls, modelTags);
        }
    }

    /// <summary>Records a usage ledger row dropped for capacity.</summary>
    public void RecordUsageRecordDropped() => _usageRecordsDropped.Add(1);

    private void AddTokens(string mode, string model, string type, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _tokens.Add(count, new TagList
        {
            { "mode", mode },
            { "model", model },
            { "type", type }
        });
    }

    public void Dispose() => _meter.Dispose();
}
