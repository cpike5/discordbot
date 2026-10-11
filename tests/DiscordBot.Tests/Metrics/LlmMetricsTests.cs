using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="LlmMetrics"/> and its wiring into <see cref="LlmUsageRecorder"/>.
/// </summary>
public class LlmMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly LlmMetrics _metrics;

    public LlmMetricsTests()
    {
        _metrics = new LlmMetrics(_meterFactory);
    }

    private MetricCollector<T> Collect<T>(string instrument) where T : struct
        => new(_meterFactory.GetMeter(LlmMetrics.MeterName)!, instrument);

    private static LlmUsageRecord Record(bool success = true) => new()
    {
        Mode = LlmMode.DmAssistant,
        Model = "anthropic/claude-sonnet-4",
        InputTokens = 1200,
        OutputTokens = 300,
        CachedTokens = 0,
        CacheWriteTokens = 50,
        LlmCalls = 2,
        ToolCalls = 1,
        CostUsd = 0.0123m,
        LatencyMs = 4200,
        Success = success
    };

    [Fact]
    public void RecordUsage_CountsRequestWithModeModelAndStatus()
    {
        using var requests = Collect<long>("discordbot.llm.request.count");
        using var duration = Collect<double>("discordbot.llm.request.duration");

        _metrics.RecordUsage(Record(success: false));

        var measurement = requests.LastMeasurement!;
        measurement.Tags["mode"].Should().Be("DmAssistant");
        measurement.Tags["model"].Should().Be("anthropic/claude-sonnet-4");
        measurement.Tags["status"].Should().Be("failure");
        duration.LastMeasurement!.Value.Should().Be(4200);
    }

    [Fact]
    public void RecordUsage_CountsEachNonZeroTokenType()
    {
        using var tokens = Collect<long>("discordbot.llm.tokens");

        _metrics.RecordUsage(Record());

        var byType = tokens.GetMeasurementSnapshot()
            .ToDictionary(m => (string)m.Tags["type"]!, m => m.Value);
        byType.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["input"] = 1200,
            ["output"] = 300,
            ["cache_write"] = 50
        });
    }

    [Fact]
    public void RecordUsage_RecordsCostCompletionsAndToolCalls()
    {
        using var cost = Collect<double>("discordbot.llm.cost.usd");
        using var completions = Collect<long>("discordbot.llm.completions");
        using var toolCalls = Collect<long>("discordbot.llm.tool_calls");

        _metrics.RecordUsage(Record());

        cost.LastMeasurement!.Value.Should().BeApproximately(0.0123, 1e-9);
        completions.LastMeasurement!.Value.Should().Be(2);
        toolCalls.LastMeasurement!.Value.Should().Be(1);
    }

    [Fact]
    public void Recorder_RecordsMetricsAndExposesQueueDepth()
    {
        using var requests = Collect<long>("discordbot.llm.request.count");
        using var depth = Collect<int>("discordbot.llm.usage_queue.depth");
        var recorder = new LlmUsageRecorder(
            Options.Create(new LlmOptions { UsageQueueCapacity = 10 }),
            NullLogger<LlmUsageRecorder>.Instance,
            _metrics);

        recorder.Record(Record());
        recorder.Record(Record());
        depth.RecordObservableInstruments();

        requests.GetMeasurementSnapshot().Should().HaveCount(2);
        depth.LastMeasurement!.Value.Should().Be(2);
    }

    [Fact]
    public void Recorder_CountsDroppedRecordsWhenFull()
    {
        using var dropped = Collect<long>("discordbot.llm.usage_records.dropped");
        var recorder = new LlmUsageRecorder(
            Options.Create(new LlmOptions { UsageQueueCapacity = 1 }),
            NullLogger<LlmUsageRecorder>.Instance,
            _metrics);

        recorder.Record(Record());
        recorder.Record(Record());

        dropped.GetMeasurementSnapshot().Should().HaveCount(1);
    }

    public void Dispose() => _metrics.Dispose();
}
