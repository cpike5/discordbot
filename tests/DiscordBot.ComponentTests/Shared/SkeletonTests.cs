using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class SkeletonTests : TestContext
{
    [Fact]
    public void RendersTextDefaults_WithAnimation()
    {
        var cut = RenderComponent<Skeleton>(p => p
            .Add(x => x.Model, new SkeletonViewModel()));

        var div = cut.Find("div");
        div.ClassList.Should().Contain(new[] { "w-full", "h-4", "rounded", "skeleton" });
        div.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void AvatarType_UsesCircleDefaults()
    {
        var cut = RenderComponent<Skeleton>(p => p
            .Add(x => x.Model, new SkeletonViewModel { Type = SkeletonType.Avatar }));

        cut.Find("div").ClassList.Should().Contain(new[] { "w-10", "h-10", "rounded-full" });
    }

    [Fact]
    public void OverridesWidthHeight_AndAppendsCssClass()
    {
        var cut = RenderComponent<Skeleton>(p => p
            .Add(x => x.Model, new SkeletonViewModel
            {
                Width = "w-3/4",
                Height = "h-8",
                CssClass = "mt-2"
            }));

        cut.Find("div").ClassList.Should().Contain(new[] { "w-3/4", "h-8", "mt-2" });
    }

    [Fact]
    public void UsesStaticClass_WhenAnimateDisabled()
    {
        var cut = RenderComponent<Skeleton>(p => p
            .Add(x => x.Model, new SkeletonViewModel { Animate = false }));

        var div = cut.Find("div");
        div.ClassList.Should().Contain("skeleton-static");
        div.ClassList.Should().NotContain("skeleton");
    }
}
