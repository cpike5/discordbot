using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Performance;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.Performance;

/// <summary>
/// With <c>Discord:OfflineMode</c> on, the bot never connects on purpose, so the
/// <c>bot_disconnected</c> metric has no value and the alert monitor skips it.
/// </summary>
public class MetricValueCollectorOfflineTests
{
    private static MetricValueCollector Create(bool offlineMode, GatewayConnectionState state)
    {
        var connection = new Mock<IConnectionStateService>();
        connection.Setup(c => c.GetCurrentState()).Returns(state);

        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILatencyHistoryService>());
        services.AddSingleton(Mock.Of<ICommandPerformanceAggregator>());
        services.AddSingleton(Mock.Of<IApiRequestTracker>());
        services.AddSingleton(Mock.Of<IDatabaseMetricsCollector>());
        services.AddSingleton(connection.Object);
        services.AddSingleton(Mock.Of<IBackgroundServiceHealthRegistry>());

        return new MetricValueCollector(
            services.BuildServiceProvider(),
            NullLogger<MetricValueCollector>.Instance,
            Options.Create(new BotConfiguration { OfflineMode = offlineMode }));
    }

    [Fact]
    public async Task BotDisconnected_OfflineMode_HasNoValue()
    {
        var collector = Create(offlineMode: true, GatewayConnectionState.Disconnected);

        (await collector.GetCurrentMetricValueAsync("bot_disconnected")).Should().BeNull();
    }

    [Fact]
    public async Task BotDisconnected_Online_ReportsTheDisconnect()
    {
        var collector = Create(offlineMode: false, GatewayConnectionState.Disconnected);

        (await collector.GetCurrentMetricValueAsync("bot_disconnected")).Should().Be(1.0);
    }

    [Fact]
    public async Task BotDisconnected_OnlineAndConnected_ReportsZero()
    {
        var collector = Create(offlineMode: false, GatewayConnectionState.Connected);

        (await collector.GetCurrentMetricValueAsync("bot_disconnected")).Should().Be(0.0);
    }
}
