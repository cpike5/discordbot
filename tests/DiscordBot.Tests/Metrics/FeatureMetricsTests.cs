using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="FeatureMetrics"/>.
/// </summary>
public class FeatureMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly FeatureMetrics _metrics;

    public FeatureMetricsTests()
    {
        _metrics = new FeatureMetrics(_meterFactory);
    }

    private MetricCollector<T> Collect<T>(string instrument) where T : struct
        => new(_meterFactory.GetMeter(FeatureMetrics.MeterName)!, instrument);

    [Fact]
    public void RecordAutomodDetection_TagsRuleTypeAndSeverity()
    {
        using var collector = Collect<long>("discordbot.automod.detections");

        _metrics.RecordAutomodDetection("Spam", "High");

        collector.LastMeasurement!.Tags["rule_type"].Should().Be("Spam");
        collector.LastMeasurement!.Tags["severity"].Should().Be("High");
    }

    [Fact]
    public void RecordAutomodAction_TagsActionAndStatus()
    {
        using var collector = Collect<long>("discordbot.automod.actions");

        _metrics.RecordAutomodAction("Mute", "failure");

        collector.LastMeasurement!.Tags["action"].Should().Be("Mute");
        collector.LastMeasurement!.Tags["status"].Should().Be("failure");
    }

    [Fact]
    public void RecordReminderDelivery_TagsStatus()
    {
        using var collector = Collect<long>("discordbot.reminders.deliveries");

        _metrics.RecordReminderDelivery("dm_disabled");

        collector.LastMeasurement!.Tags["status"].Should().Be("dm_disabled");
    }

    [Fact]
    public void RecordScheduledMessageExecution_TagsStatus()
    {
        using var collector = Collect<long>("discordbot.scheduled_messages.executions");

        _metrics.RecordScheduledMessageExecution("timeout");

        collector.LastMeasurement!.Tags["status"].Should().Be("timeout");
    }

    [Fact]
    public void RecordAudioPlayback_CountsAndRecordsDurationInSeconds()
    {
        using var count = Collect<long>("discordbot.audio.playbacks");
        using var duration = Collect<double>("discordbot.audio.playback.duration");

        _metrics.RecordAudioPlayback("tts", "success", TimeSpan.FromMilliseconds(2500));

        count.LastMeasurement!.Tags["source"].Should().Be("tts");
        duration.LastMeasurement!.Value.Should().BeApproximately(2.5, 1e-9);
    }

    public void Dispose() => _metrics.Dispose();
}
