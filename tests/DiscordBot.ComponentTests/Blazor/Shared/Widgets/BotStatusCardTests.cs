using Bunit;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class BotStatusCardTests : BlazorComponentTestContext
{
    private readonly Mock<IDashboardMetricsService> _metricsService = new();

    public BotStatusCardTests()
    {
        _metricsService
            .Setup(m => m.GetCurrentStatus(null, null))
            .Returns(new BotStatusDto { ConnectionState = "Connected", GuildCount = 5, LatencyMs = 20, Uptime = TimeSpan.FromHours(2) });

        Services.AddSingleton(_metricsService.Object);
    }

    [Fact]
    public void OnInitialized_LoadsCurrentStatus()
    {
        var cut = Render<BotStatusCard>();

        cut.Find("[data-connection-state]").TextContent.Should().Be("Connected");
        cut.Find("[data-guild-count]").TextContent.Should().Be("5");
        cut.Find("[data-latency]").TextContent.Should().Be("20");
    }

    [Fact]
    public async Task BotStatusUpdatedEvent_UpdatesCard_AfterDebounce()
    {
        var cut = Render<BotStatusCard>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new BotStatusUpdatedEvent
        {
            Status = new BotStatusDto { ConnectionState = "Disconnected", GuildCount = 0, LatencyMs = 0, Uptime = TimeSpan.Zero }
        });

        cut.WaitForAssertion(
            () => cut.Find("[data-connection-state]").TextContent.Should().Be("Disconnected"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Class_IsAppended()
    {
        var cut = Render<BotStatusCard>(p => p.Add(x => x.Class, "extra-class"));
        cut.Find("[data-bot-status-card]").ClassList.Should().Contain("extra-class");
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBus()
    {
        var cut = Render<BotStatusCard>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // First prove the subscription is live before disposal, so the "does not re-render"
        // check below is actually meaningful.
        await bus.PublishAsync(new BotStatusUpdatedEvent { Status = new BotStatusDto { ConnectionState = "Disconnected" } });
        cut.WaitForAssertion(
            () => cut.Find("[data-connection-state]").TextContent.Should().Be("Disconnected"),
            TimeSpan.FromSeconds(3));

        await DisposeComponentsAsync();
        var renderCountAfterDispose = cut.RenderCount;

        await bus.PublishAsync(new BotStatusUpdatedEvent { Status = new BotStatusDto { ConnectionState = "Connected" } });
        // Short window, not a full debounce-window sleep: Dispose() unsubscribes synchronously,
        // so there is nothing left to debounce - this only guards against a latent regression.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }
}
