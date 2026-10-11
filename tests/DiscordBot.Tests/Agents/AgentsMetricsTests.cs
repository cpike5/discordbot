using DiscordBot.Agents;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentsMetrics"/>. The meter is static, so other tests running in
/// parallel can record into it too; each assertion filters on a value unique to the test.
/// </summary>
public class AgentsMetricsTests
{
    [Fact]
    public void RecordToolCall_TagsToolAndOutcome()
    {
        using var collector = new MetricCollector<long>(null, AgentsMetrics.MeterName, "discordbot.agent.tool.count");
        var tool = $"tool_{Guid.NewGuid():N}";

        AgentsMetrics.RecordToolCall(tool, ToolOutcomes.Ok, TimeSpan.FromMilliseconds(12));

        collector.GetMeasurementSnapshot()
            .Should().ContainSingle(m => (string?)m.Tags["tool"] == tool && (string?)m.Tags["outcome"] == ToolOutcomes.Ok);
    }

    [Fact]
    public void RecordToolCall_UnknownTool_CollapsesName()
    {
        using var collector = new MetricCollector<long>(null, AgentsMetrics.MeterName, "discordbot.agent.tool.count");
        var invented = $"invented_{Guid.NewGuid():N}";

        AgentsMetrics.RecordToolCall(invented, ToolOutcomes.UnknownTool, TimeSpan.Zero);

        var snapshot = collector.GetMeasurementSnapshot();
        snapshot.Should().NotContain(m => (string?)m.Tags["tool"] == invented);
        snapshot.Should().Contain(m => (string?)m.Tags["tool"] == "unknown" && (string?)m.Tags["outcome"] == ToolOutcomes.UnknownTool);
    }

    [Fact]
    public void RecordCompletion_RecordsDurationWithModelStatusAndStopReason()
    {
        using var collector = new MetricCollector<double>(null, AgentsMetrics.MeterName, "discordbot.agent.completion.duration");
        var model = $"test/model-{Guid.NewGuid():N}";

        AgentsMetrics.RecordCompletion(model, "success", "EndTurn", TimeSpan.FromMilliseconds(850));

        var measurement = collector.GetMeasurementSnapshot().Should()
            .ContainSingle(m => (string?)m.Tags["model"] == model).Subject;
        measurement.Value.Should().Be(850);
        measurement.Tags["status"].Should().Be("success");
        measurement.Tags["stop_reason"].Should().Be("EndTurn");
    }

    [Fact]
    public void RecordCompletion_WithoutModel_TagsUnknown()
    {
        using var collector = new MetricCollector<long>(null, AgentsMetrics.MeterName, "discordbot.agent.completion.count");

        AgentsMetrics.RecordCompletion(null, "exception", null, TimeSpan.Zero);

        collector.GetMeasurementSnapshot().Should().Contain(m =>
            (string?)m.Tags["model"] == "unknown" && (string?)m.Tags["status"] == "exception" && (string?)m.Tags["stop_reason"] == "none");
    }
}
