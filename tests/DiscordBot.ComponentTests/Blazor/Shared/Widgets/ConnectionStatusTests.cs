using Bunit;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class ConnectionStatusTests : BlazorComponentTestContext
{
    private readonly Mock<IDashboardMetricsService> _metricsService = new();

    public ConnectionStatusTests()
    {
        _metricsService
            .Setup(m => m.GetCurrentStatus(null, null))
            .Returns(new BotStatusDto { ConnectionState = "Connected" });

        Services.AddSingleton(_metricsService.Object);
    }

    [Fact]
    public void State_Explicit_RendersThatState_AndSkipsInitialServiceCall()
    {
        var cut = Render<ConnectionStatus>(p => p.Add(x => x.State, ConnectionState.Reconnecting));

        cut.Find("#connection-status").GetAttribute("data-state").Should().Be("reconnecting");
        cut.Find(".connection-text").TextContent.Should().Be("Reconnecting...");
        _metricsService.Verify(m => m.GetCurrentStatus(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void CustomText_OverridesDefaultLabel()
    {
        var cut = Render<ConnectionStatus>(p => p.Add(x => x.State, ConnectionState.Connected).Add(x => x.CustomText, "All good"));
        cut.Find(".connection-text").TextContent.Should().Be("All good");
    }

    [Fact]
    public void State_Unset_LoadsFromDashboardMetricsService()
    {
        var cut = Render<ConnectionStatus>();

        cut.Find("#connection-status").GetAttribute("data-state").Should().Be("connected");
        _metricsService.Verify(m => m.GetCurrentStatus(null, null), Times.Once);
    }

    [Fact]
    public async Task State_Unset_FollowsBotStatusBroadcastEvent()
    {
        var cut = Render<ConnectionStatus>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new BotStatusBroadcastEvent { Status = new BotStatusUpdateDto { ConnectionState = "Connecting" } });

        cut.WaitForAssertion(
            () => cut.Find("#connection-status").GetAttribute("data-state").Should().Be("connecting"),
            TimeSpan.FromSeconds(3));
    }
}
