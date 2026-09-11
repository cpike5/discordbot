using Bunit;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Components.Enums;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class ActivityFeedTests : BlazorComponentTestContext
{
    [Fact]
    public void EmptyItems_RendersEmptyState()
    {
        var cut = Render<ActivityFeed>();
        cut.Markup.Should().Contain("No recent activity");
    }

    [Fact]
    public void Items_RendersInitialList()
    {
        var items = new List<ActivityFeedItemViewModel>
        {
            new() { Type = ActivityItemType.Info, Message = "Seed item", Source = "Guild A", Timestamp = DateTime.UtcNow }
        };

        var cut = Render<ActivityFeed>(p => p.Add(x => x.Items, items));

        cut.Markup.Should().Contain("Seed item");
    }

    [Fact]
    public void Message_ContainingMarkup_RendersEscapedNotAsHtml()
    {
        var items = new List<ActivityFeedItemViewModel>
        {
            new()
            {
                Type = ActivityItemType.Info,
                Message = "<img src=x onerror=alert(1)> ran /cmd",
                CommandText = "/cmd",
                Source = "Guild A",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = Render<ActivityFeed>(p => p.Add(x => x.Items, items));

        cut.Markup.Should().NotContain("<img");
        cut.Markup.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
        cut.Markup.Should().Contain("<span class=\"font-mono text-accent-orange\">/cmd</span>");
    }

    [Fact]
    public async Task CommandExecutedEvent_UnscopedGuildId_PrependsItem()
    {
        var cut = Render<ActivityFeed>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "help", Username = "alice", GuildName = "Guild A", Success = true, Timestamp = DateTime.UtcNow }
        });

        cut.WaitForAssertion(
            () => cut.Markup.Should().Contain("/help"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CommandExecutedEvent_WithGuildIdSet_FiltersToThatGuild()
    {
        var cut = Render<ActivityFeed>(p => p.Add(x => x.GuildId, 111UL));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // The GuildId filter check runs synchronously inside the subscriber, so by the time
        // PublishAsync's await returns, "ignored" is either enqueued or (as expected here) it
        // never will be - no wait needed to know that.
        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "ignored", GuildId = 222, Success = true, Timestamp = DateTime.UtcNow }
        });
        cut.Markup.Should().NotContain("/ignored");

        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "matched", GuildId = 111, Success = true, Timestamp = DateTime.UtcNow }
        });
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("/matched"), TimeSpan.FromSeconds(3));

        // The debounced render above proves enough time has passed for "ignored" to have shown
        // up too, had the filter not rejected it.
        cut.Markup.Should().NotContain("/ignored");
    }

    [Fact]
    public async Task GuildActivityEvent_GuildScoped_OnlyMatchingGuildAppends()
    {
        var cut = Render<ActivityFeed>(p => p.Add(x => x.GuildId, 111UL));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new GuildActivityEvent
        {
            GuildId = 111,
            Update = new GuildActivityUpdateDto { GuildId = 111, GuildName = "Guild A", EventType = "MemberJoined", Timestamp = DateTime.UtcNow }
        });

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("New member joined the server"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task IsPaused_QueuesEventsInstead_ThenFlushesOnResume()
    {
        bool? lastChanged = null;
        var cut = Render<ActivityFeed>(p => p
            .Add(x => x.IsPaused, true)
            .Add(x => x.IsPausedChanged, v => lastChanged = v));

        var bus = Services.GetRequiredService<IDashboardEventBus>();
        // Enqueue's paused branch adds to _pending synchronously with no debounce involved, so
        // by the time this await returns the item is either queued (as expected) or already
        // rendered - no wait needed either way.
        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "queued", Success = true, Timestamp = DateTime.UtcNow }
        });
        cut.Markup.Should().NotContain("/queued");

        cut.Find("button[aria-pressed='true']").Click();

        lastChanged.Should().Be(false);
        cut.Markup.Should().Contain("/queued");
    }

    [Fact]
    public void ShowRefreshButton_ClickInvokesOnRefresh()
    {
        var refreshed = false;
        var cut = Render<ActivityFeed>(p => p
            .Add(x => x.ShowRefreshButton, true)
            .Add(x => x.OnRefresh, () => refreshed = true));

        cut.Find("button[aria-label='Refresh activity feed']").Click();

        refreshed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBus()
    {
        var cut = Render<ActivityFeed>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // First prove the subscription is live before disposal, so the "does not re-render"
        // check below is actually meaningful.
        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "before-dispose", Success = true, Timestamp = DateTime.UtcNow }
        });
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("/before-dispose"), TimeSpan.FromSeconds(3));

        await DisposeComponentsAsync();
        var renderCountAfterDispose = cut.RenderCount;

        await bus.PublishAsync(new CommandExecutedEvent
        {
            Update = new CommandExecutedUpdateDto { CommandName = "after-dispose", Success = true, Timestamp = DateTime.UtcNow }
        });
        // Short window, not a full debounce-window sleep: Dispose() unsubscribes synchronously,
        // so there is nothing left to debounce - this only guards against a latent regression.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }
}
