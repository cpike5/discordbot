using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class AlertTests : TestContext
{
    [Fact]
    public void RendersMessageAndTitle_WithVariantClasses()
    {
        var cut = RenderComponent<Alert>(p => p
            .Add(x => x.Variant, AlertVariant.Error)
            .Add(x => x.Title, "Oops")
            .Add(x => x.Message, "Something went wrong"));

        var root = cut.Find("div[role='alert']");
        root.ClassList.Should().Contain(new[] { "bg-error/10", "border-error/30", "text-error" });
        cut.Find("h3").TextContent.Trim().Should().Be("Oops");
        cut.Find("p").TextContent.Trim().Should().Be("Something went wrong");
    }

    [Fact]
    public void HidesIcon_WhenShowIconFalse()
    {
        var with = RenderComponent<Alert>(p => p.Add(x => x.Message, "Hi"));
        with.FindAll("svg").Should().NotBeEmpty();

        var without = RenderComponent<Alert>(p => p
            .Add(x => x.Message, "Hi")
            .Add(x => x.ShowIcon, false));
        without.FindAll("svg").Should().BeEmpty();
    }

    [Fact]
    public void ShowsDismissButton_OnlyWhenDismissible()
    {
        var not = RenderComponent<Alert>(p => p.Add(x => x.Message, "Hi"));
        not.FindAll("button").Should().BeEmpty();

        var dismissible = RenderComponent<Alert>(p => p
            .Add(x => x.Message, "Hi")
            .Add(x => x.IsDismissible, true));
        dismissible.Find("button[aria-label='Dismiss']").Should().NotBeNull();
    }

    [Fact]
    public void RaisesOnDismiss_WhenDismissClicked()
    {
        var dismissed = false;
        var cut = RenderComponent<Alert>(p => p
            .Add(x => x.Message, "Hi")
            .Add(x => x.IsDismissible, true)
            .Add(x => x.OnDismiss, () => dismissed = true));

        cut.Find("button[aria-label='Dismiss']").Click();

        dismissed.Should().BeTrue();
    }
}
