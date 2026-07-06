using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class ActivityFeedTests : TestContext
{
    [Fact]
    public void RendersEmptyMessage_WhenNoItems()
    {
        var cut = RenderComponent<ActivityFeed>(p => p
            .Add(x => x.EmptyMessage, "Nothing yet"));

        cut.Markup.Should().Contain("Nothing yet");
        cut.FindAll(".activity-item").Should().BeEmpty();
    }

    [Fact]
    public void RendersItems_CappedAtMaxItems()
    {
        var items = Enumerable.Range(1, 5)
            .Select(i => new ActivityFeedItemViewModel
            {
                Message = $"Activity {i}",
                Source = "Test Guild",
                Timestamp = DateTime.UtcNow,
            })
            .ToList();

        var cut = RenderComponent<ActivityFeed>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.MaxItems, 3));

        cut.FindAll(".activity-item").Should().HaveCount(3);
        cut.Markup.Should().Contain("Activity 1").And.Contain("Test Guild");
    }

    [Fact]
    public void PauseButton_TogglesStateAndRaisesIsPausedChanged()
    {
        var paused = false;
        var cut = RenderComponent<ActivityFeed>(p => p
            .Add(x => x.IsPausedChanged, (bool v) => paused = v));

        cut.Find("button").Click();

        paused.Should().BeTrue();
        cut.Find("button").GetAttribute("aria-pressed").Should().Be("true");
        cut.Markup.Should().Contain("Activity feed is paused");
    }
}
