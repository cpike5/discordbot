using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class ActivityFeedTimelineTests : TestContext
{
    [Fact]
    public void RendersTitleAndEmptyState_WhenNoItems()
    {
        var cut = RenderComponent<ActivityFeedTimeline>(p => p
            .Add(x => x.Model, new ActivityFeedTimelineViewModel { Title = "Recent Activity" }));

        cut.Find("h2").TextContent.Should().Be("Recent Activity");
        cut.Markup.Should().Contain("No recent activity");
        cut.FindAll(".activity-item").Should().BeEmpty();
    }

    [Fact]
    public void RendersItems_WithHighlightedCommandText()
    {
        var model = new ActivityFeedTimelineViewModel
        {
            Items =
            {
                new ActivityFeedItemViewModel
                {
                    Message = "User ran /help in general",
                    CommandText = "/help",
                    Source = "Gaming Community",
                    Timestamp = DateTime.UtcNow,
                },
            },
        };

        var cut = RenderComponent<ActivityFeedTimeline>(p => p.Add(x => x.Model, model));

        cut.FindAll(".activity-item").Should().HaveCount(1);
        cut.Find("span.font-mono.text-accent-orange").TextContent.Should().Be("/help");
        cut.Markup.Should().Contain("Gaming Community");
    }

    [Fact]
    public void RendersViewAllFooter_OnlyWhenUrlSet()
    {
        var without = RenderComponent<ActivityFeedTimeline>(p => p
            .Add(x => x.Model, new ActivityFeedTimelineViewModel()));
        without.FindAll("a").Should().BeEmpty();

        var with = RenderComponent<ActivityFeedTimeline>(p => p
            .Add(x => x.Model, new ActivityFeedTimelineViewModel { ViewAllUrl = "/activity" }));
        with.Find("a").GetAttribute("href").Should().Be("/activity");
    }

    [Fact]
    public void PauseButton_TogglesAndRaisesOnPauseToggle()
    {
        var paused = false;
        var cut = RenderComponent<ActivityFeedTimeline>(p => p
            .Add(x => x.Model, new ActivityFeedTimelineViewModel())
            .Add(x => x.OnPauseToggle, (bool v) => paused = v));

        cut.Find("button").Click();

        paused.Should().BeTrue();
        cut.Find("button").GetAttribute("aria-pressed").Should().Be("true");
    }
}
