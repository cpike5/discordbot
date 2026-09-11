using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class RecentActivityCardTests : BlazorComponentTestContext
{
    private static readonly ActivityItem Success = new(
        Guid.NewGuid(), "/ban", "Test Guild", "mod1", DateTime.UtcNow.AddMinutes(-3), "3 min ago", true, null);

    private static readonly ActivityItem Failure = new(
        Guid.NewGuid(), "/play", "Test Guild", "user2", DateTime.UtcNow.AddMinutes(-1), "1 min ago", false, "Boom");

    [Fact]
    public void NoActivities_RendersEmptyState()
    {
        var cut = Render<RecentActivityCard>();
        cut.Markup.Should().Contain("No recent activity");
    }

    [Fact]
    public void Activities_RendersSuccessAndFailure()
    {
        // The footer "View all activity" link is an <AuthorizeView Policy="RequireModerator">,
        // present whenever Activities is non-empty - needs bUnit's fake auth services registered.
        AddAuthorization();
        var cut = Render<RecentActivityCard>(p => p.Add(x => x.Activities, new[] { Success, Failure }));

        cut.Markup.Should().Contain("/ban");
        cut.Markup.Should().Contain("/play");
        cut.Markup.Should().Contain("failed");
        cut.Markup.Should().Contain("Boom");
    }

    [Fact]
    public void RefreshButton_InvokesOnRefresh_AndShowsSpinnerWhileAwaited()
    {
        var refreshed = false;
        var cut = Render<RecentActivityCard>(p => p.Add(x => x.OnRefresh, async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            refreshed = true;
        }));

        cut.Find("button[aria-label='Refresh activity feed']").Click();

        cut.WaitForAssertion(
            () => cut.Find("button[aria-label='Refresh activity feed'] svg").ClassList.Should().Contain("animate-spin"),
            TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(
            () =>
            {
                refreshed.Should().BeTrue();
                cut.Find("button[aria-label='Refresh activity feed'] svg").ClassList.Should().NotContain("animate-spin");
            },
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void NoOnRefresh_ClickIsANoOp()
    {
        var cut = Render<RecentActivityCard>();
        var act = () => cut.Find("button[aria-label='Refresh activity feed']").Click();
        act.Should().NotThrow();
    }
}
