using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class SkeletonCardTests : TestContext
{
    [Fact]
    public void RendersStatsLayout_ByDefault_WithoutHeader()
    {
        var cut = RenderComponent<SkeletonCard>();

        cut.Find("div").ClassList.Should().Contain("card");
        cut.FindAll(".card-header").Should().BeEmpty();
        cut.FindAll(".card-body .skeleton").Should().HaveCount(3);
    }

    [Fact]
    public void ShowsHeaderSkeleton_WhenShowHeaderTrue()
    {
        var cut = RenderComponent<SkeletonCard>(p => p.Add(x => x.ShowHeader, true));

        cut.FindAll(".card-header .skeleton").Should().HaveCount(1);
    }

    [Fact]
    public void ActivityType_RendersThreeRows()
    {
        var cut = RenderComponent<SkeletonCard>(p => p
            .Add(x => x.Type, SkeletonCardType.Activity));

        cut.FindAll(".card-body .rounded-full").Should().HaveCount(3);
    }

    [Fact]
    public void AppendsCustomCssClass_ToCardRoot()
    {
        var cut = RenderComponent<SkeletonCard>(p => p.Add(x => x.CssClass, "h-full"));

        cut.Find("div").ClassList.Should().Contain(new[] { "card", "h-full" });
    }
}
