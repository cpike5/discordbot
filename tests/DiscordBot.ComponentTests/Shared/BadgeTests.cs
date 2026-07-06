using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class BadgeTests : TestContext
{
    [Fact]
    public void RendersText_WithFilledVariantClasses()
    {
        var cut = RenderComponent<Badge>(p => p
            .Add(x => x.Model, new BadgeViewModel { Text = "Active", Variant = BadgeVariant.Success }));

        var span = cut.Find("span");
        span.TextContent.Trim().Should().Be("Active");
        span.ClassList.Should().Contain(new[] { "bg-success", "text-white", "rounded-full" });
    }

    [Fact]
    public void AppliesOutlineStyle_AndSmallSizeClasses()
    {
        var cut = RenderComponent<Badge>(p => p
            .Add(x => x.Model, new BadgeViewModel
            {
                Text = "Beta",
                Variant = BadgeVariant.Blue,
                Style = BadgeStyle.Outline,
                Size = BadgeSize.Small
            }));

        var span = cut.Find("span");
        span.ClassList.Should().Contain(new[] { "border-accent-blue", "bg-transparent", "px-2", "py-0.5" });
    }

    [Fact]
    public void ShowsRemoveButton_OnlyWhenRemovable()
    {
        var without = RenderComponent<Badge>(p => p
            .Add(x => x.Model, new BadgeViewModel { Text = "Tag" }));
        without.FindAll("button").Should().BeEmpty();

        var with = RenderComponent<Badge>(p => p
            .Add(x => x.Model, new BadgeViewModel { Text = "Tag", IsRemovable = true }));
        with.Find("button[aria-label='Remove']").Should().NotBeNull();
    }

    [Fact]
    public void RaisesOnRemove_WhenRemoveButtonClicked()
    {
        var removed = false;
        var cut = RenderComponent<Badge>(p => p
            .Add(x => x.Model, new BadgeViewModel { Text = "Tag", IsRemovable = true })
            .Add(x => x.OnRemove, () => removed = true));

        cut.Find("button[aria-label='Remove']").Click();

        removed.Should().BeTrue();
    }
}
