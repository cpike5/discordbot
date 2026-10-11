using Discord;
using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DiscordGatewayMetrics"/>.
/// </summary>
public class DiscordGatewayMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly DiscordGatewayMetrics _metrics;

    public DiscordGatewayMetricsTests()
    {
        _metrics = new DiscordGatewayMetrics(_meterFactory);
    }

    private MetricCollector<T> Collect<T>(string instrument) where T : struct
        => new(_meterFactory.GetMeter(DiscordGatewayMetrics.MeterName)!, instrument);

    [Theory]
    [InlineData("channels/123456789012345678/messages", "channels/{id}/messages")]
    [InlineData("/guilds/123456789012345678/members/987654321098765432", "guilds/{id}/members/{id}")]
    [InlineData("interactions/123456789012345678/aW50ZXJhY3Rpb246MTIzNDU2Nzg5MDEyMzQ1Njc4OnRva2Vu/callback", "interactions/{id}/{token}/callback")]
    [InlineData("channels/123/messages/456/reactions/%F0%9F%91%8D/@me", "channels/{id}/messages/{id}/reactions/{value}/@me")]
    [InlineData("guilds/123/audit-logs?limit=50", "guilds/{id}/audit-logs")]
    [InlineData("users/@me", "users/@me")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void NormalizeRoute_CollapsesVariableSegments(string? endpoint, string expected)
    {
        DiscordGatewayMetrics.NormalizeRoute(endpoint).Should().Be(expected);
    }

    [Fact]
    public void ConnectionState_IsReflectedInConnectedGauge()
    {
        using var collector = Collect<long>("discordbot.gateway.connected");

        _metrics.RecordConnected();
        collector.RecordObservableInstruments();
        collector.LastMeasurement!.Value.Should().Be(1);

        _metrics.RecordDisconnected(new TimeoutException());
        collector.RecordObservableInstruments();
        collector.LastMeasurement!.Value.Should().Be(0);
    }

    [Fact]
    public void RecordDisconnected_TagsReasonWithExceptionType()
    {
        using var collector = Collect<long>("discordbot.gateway.connection.events");

        _metrics.RecordDisconnected(new TimeoutException());

        var measurement = collector.LastMeasurement!;
        measurement.Tags["event"].Should().Be("disconnected");
        measurement.Tags["reason"].Should().Be(nameof(TimeoutException));
    }

    [Fact]
    public void UpdateLatency_SetsLatencyGauge()
    {
        using var collector = Collect<long>("discordbot.gateway.latency");

        _metrics.UpdateLatency(87);
        collector.RecordObservableInstruments();

        collector.LastMeasurement!.Value.Should().Be(87);
    }

    [Fact]
    public void RecordLogEvent_TagsSeverityLowercaseAndSource()
    {
        using var collector = Collect<long>("discordbot.discord.log.events");

        _metrics.RecordLogEvent(LogSeverity.Warning, "Gateway");

        collector.LastMeasurement!.Tags["severity"].Should().Be("warning");
        collector.LastMeasurement!.Tags["source"].Should().Be("Gateway");
    }

    [Fact]
    public void RecordRateLimitInfo_With429_CountsHit()
    {
        using var responses = Collect<long>("discordbot.discord.rest.responses");
        using var rateLimits = Collect<long>("discordbot.discord.rest.ratelimits");

        _metrics.RecordRateLimitInfo(RateLimitInfo("channels/123456789012345678/messages", retryAfter: 2, remaining: 0, isGlobal: true));

        responses.LastMeasurement!.Tags["route"].Should().Be("channels/{id}/messages");
        rateLimits.LastMeasurement!.Tags["kind"].Should().Be("hit");
        rateLimits.LastMeasurement!.Tags["scope"].Should().Be("global");
    }

    [Fact]
    public void RecordRateLimitInfo_WithExhaustedBucket_CountsExhausted()
    {
        using var rateLimits = Collect<long>("discordbot.discord.rest.ratelimits");

        _metrics.RecordRateLimitInfo(RateLimitInfo("guilds/1/members", retryAfter: null, remaining: 0, isGlobal: false));

        rateLimits.LastMeasurement!.Tags["kind"].Should().Be("exhausted");
        rateLimits.LastMeasurement!.Tags["scope"].Should().Be("bucket");
    }

    [Fact]
    public void RecordRateLimitInfo_WithRemainingRequests_CountsOnlyTheResponse()
    {
        using var responses = Collect<long>("discordbot.discord.rest.responses");
        using var rateLimits = Collect<long>("discordbot.discord.rest.ratelimits");

        _metrics.RecordRateLimitInfo(RateLimitInfo("guilds/1/members", retryAfter: null, remaining: 4, isGlobal: false));

        responses.GetMeasurementSnapshot().Should().HaveCount(1);
        rateLimits.GetMeasurementSnapshot().Should().BeEmpty();
    }

    private static IRateLimitInfo RateLimitInfo(string endpoint, int? retryAfter, int? remaining, bool isGlobal)
    {
        var info = new Mock<IRateLimitInfo>();
        info.SetupGet(i => i.Endpoint).Returns(endpoint);
        info.SetupGet(i => i.RetryAfter).Returns(retryAfter);
        info.SetupGet(i => i.Remaining).Returns(remaining);
        info.SetupGet(i => i.IsGlobal).Returns(isGlobal);
        return info.Object;
    }

    public void Dispose() => _metrics.Dispose();
}
