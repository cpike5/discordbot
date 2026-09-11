using Bunit;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class BotStatusBannerTests : BlazorComponentTestContext
{
    private readonly Mock<IDashboardMetricsService> _metricsService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IVersionService> _versionService = new();

    public BotStatusBannerTests()
    {
        _metricsService
            .Setup(m => m.GetCurrentStatus(null, null))
            .Returns(new BotStatusDto { ConnectionState = "Connected", GuildCount = 3, LatencyMs = 42, Uptime = TimeSpan.FromMinutes(90) });
        _guildService
            .Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new GuildDto { MemberCount = 100 }, new GuildDto { MemberCount = 50 } });
        _versionService.Setup(v => v.GetVersion()).Returns("v1.2.3");

        Services.AddSingleton(_metricsService.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_versionService.Object);
    }

    [Fact]
    public void OnInitialized_LoadsCurrentStatus_FromDashboardMetricsService()
    {
        var cut = Render<BotStatusBanner>();

        cut.Find("[data-status-heading]").TextContent.Should().Be("Bot is Online");
        cut.Find("[data-guild-count]").TextContent.Should().Be("3");
        cut.Find("[data-version]").TextContent.Should().Be("v1.2.3");
        cut.Find("[data-latency]").TextContent.Should().Be("42");
        cut.Markup.Should().Contain("150"); // TotalMembers = 100 + 50
    }

    [Fact]
    public async Task BotStatusUpdatedEvent_UpdatesBanner_AfterDebounce()
    {
        var cut = Render<BotStatusBanner>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new BotStatusUpdatedEvent
        {
            Status = new BotStatusDto { ConnectionState = "Disconnected", GuildCount = 0, LatencyMs = 0, Uptime = TimeSpan.Zero }
        });

        cut.WaitForAssertion(
            () => cut.Find("[data-status-heading]").TextContent.Should().Be("Bot is Offline"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task BotStatusBroadcastEvent_UpdatesBanner_AfterDebounce()
    {
        var cut = Render<BotStatusBanner>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new BotStatusBroadcastEvent
        {
            Status = new BotStatusUpdateDto { ConnectionState = "Connected", GuildCount = 9, Latency = 7, Uptime = TimeSpan.FromMinutes(5) }
        });

        cut.WaitForAssertion(
            () => cut.Find("[data-guild-count]").TextContent.Should().Be("9"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBus()
    {
        var cut = Render<BotStatusBanner>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await DisposeComponentsAsync();
        cut.IsDisposed.Should().BeTrue();
        var renderCountAfterDispose = cut.RenderCount;

        var act = async () => await bus.PublishAsync(new BotStatusUpdatedEvent
        {
            Status = new BotStatusDto { ConnectionState = "Connected" }
        });
        await act.Should().NotThrowAsync();

        await Task.Delay(TimeSpan.FromSeconds(1.5));
        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }
}
