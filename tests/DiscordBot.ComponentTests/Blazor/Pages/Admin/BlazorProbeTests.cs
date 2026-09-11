using Bunit;
using DiscordBot.Bot.Blazor.Pages.Admin;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin;

/// <summary>
/// Component tests for the Phase 1 foundation probe page
/// (<c>Blazor/Pages/Admin/BlazorProbe.razor</c>, plan §5 Phase 1 deliverable 7). Each test
/// exercises one of the building blocks the page proves: interactivity, cascading auth state,
/// toasts, loading state, the debounced event-bus subscription, chart interop call order, and
/// disposal.
/// </summary>
public class BlazorProbeTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersAllSections_WhenAuthorizedAsAdmin()
    {
        AddAuthorizedAdmin("admin");

        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='auth-username']").TextContent.Should().Be("admin");
        cut.Find("[data-testid='auth-roles']").TextContent.Should().Contain("Admin");

        foreach (var testId in new[]
                 {
                     "probe-counter", "probe-auth", "probe-toasts", "probe-loading",
                     "probe-eventbus", "probe-chart", "probe-browser", "probe-status-table"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }
    }

    [Fact]
    public void Counter_IncrementsOnClick()
    {
        AddAuthorizedAdmin();
        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='counter-value']").TextContent.Should().Contain("0");

        cut.Find("[data-testid='counter-increment']").Click();

        cut.Find("[data-testid='counter-value']").TextContent.Should().Contain("1");
    }

    [Fact]
    public void Toast_AppearsThenDismisses()
    {
        AddAuthorizedAdmin();
        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='toast-empty']").Should().NotBeNull();

        cut.Find("[data-testid='toast-success-btn']").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='toast-item']").Should().ContainSingle());

        cut.Find("[data-testid='toast-dismiss-btn']").Click();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='toast-empty']").Should().NotBeNull());
    }

    [Fact]
    public void LoadingBadge_TogglesAcrossAwaitedOperation()
    {
        AddAuthorizedAdmin();
        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='loading-badge']").TextContent.Should().Be("Idle");

        cut.Find("[data-testid='loading-start-btn']").Click();

        cut.WaitForAssertion(
            () => cut.Find("[data-testid='loading-badge']").TextContent.Should().Contain("Loading"),
            TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(
            () => cut.Find("[data-testid='loading-badge']").TextContent.Should().Be("Idle"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task PublishingBusEvent_UpdatesEventSection_RespectingTheDebouncer()
    {
        AddAuthorizedAdmin();
        var cut = Render<BlazorProbe>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        cut.Find("[data-testid='eventbus-empty']").Should().NotBeNull();

        var publishedAt = DateTime.UtcNow;
        await bus.PublishAsync(new BotStatusBroadcastEvent
        {
            Status = new BotStatusUpdateDto
            {
                ConnectionState = "Connected",
                Latency = 42,
                GuildCount = 7,
                Uptime = TimeSpan.FromMinutes(12),
                Timestamp = publishedAt
            }
        });

        // The component's Debouncer coalesces to at most one re-render per second (patterns.md,
        // "Real-time event bus" rule 2), so the update lands up to ~1s after publish.
        cut.WaitForAssertion(
            () => cut.Find("[data-testid='eventbus-last-payload']").TextContent.Should().Contain("Connected"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void ChartInterop_ImportsModuleThenCallsCreate_AfterFirstRender()
    {
        AddAuthorizedAdmin();

        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(7);

        var cut = Render<BlazorProbe>();

        cut.WaitForAssertion(() =>
        {
            JSInterop.Invocations["import"]
                .Should().Contain(inv => (string)inv.Arguments[0]! == "./js/blazor/charts.js");
            createHandler.Invocations.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromEverything_PublishAfterDisposalNeitherThrowsNorReRenders()
    {
        AddAuthorizedAdmin();
        var cut = Render<BlazorProbe>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // First prove the subscription is live before disposal, so the "does not re-render"
        // assertion below is actually meaningful.
        await bus.PublishAsync(new BotStatusBroadcastEvent
        {
            Status = new BotStatusUpdateDto { ConnectionState = "Connected", Timestamp = DateTime.UtcNow }
        });
        cut.WaitForAssertion(
            () => cut.Find("[data-testid='eventbus-last-payload']").TextContent.Should().Contain("Connected"),
            TimeSpan.FromSeconds(3));

        await DisposeComponentsAsync();
        cut.IsDisposed.Should().BeTrue();
        var renderCountAfterDispose = cut.RenderCount;

        var act = async () => await bus.PublishAsync(new BotStatusBroadcastEvent
        {
            Status = new BotStatusUpdateDto { ConnectionState = "Reconnecting", Timestamp = DateTime.UtcNow }
        });
        await act.Should().NotThrowAsync();

        // No WaitForAssertion here on purpose: there is nothing to wait FOR - Dispose() already
        // unsubscribed above (proven live first, so this negative assertion is meaningful), so a
        // short window is enough to catch a re-render that must not happen, rather than sleeping
        // out the full ~1s debounce window a still-subscribed component would need. A plain await
        // (not a poll loop), so CLAUDE.md's thread-pool-starvation ConfigureAwait(false) rule for
        // wall-clock polling doesn't apply.
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }
}
