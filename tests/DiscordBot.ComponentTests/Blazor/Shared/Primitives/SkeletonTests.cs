using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class SkeletonTests : BlazorComponentTestContext
{
    [Fact]
    public void Default_RendersTextDefaults_WithAnimateClass_AndAriaHidden()
    {
        var cut = Render<Skeleton>();

        var div = cut.Find("div");
        div.GetAttribute("aria-hidden").Should().Be("true");
        div.ClassList.Should().Contain("w-full").And.Contain("h-4").And.Contain("rounded").And.Contain("skeleton");
    }

    [Theory]
    [InlineData(SkeletonType.Title, "h-6")]
    [InlineData(SkeletonType.Avatar, "w-10")]
    [InlineData(SkeletonType.AvatarSmall, "w-8")]
    [InlineData(SkeletonType.AvatarLarge, "w-16")]
    [InlineData(SkeletonType.Button, "w-24")]
    [InlineData(SkeletonType.Card, "h-32")]
    [InlineData(SkeletonType.Rectangle, "h-20")]
    public void Type_AppliesExpectedDefaultDimension(SkeletonType type, string expectedClass)
    {
        var cut = Render<Skeleton>(p => p.Add(x => x.Type, type));
        cut.Find("div").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void WidthAndHeight_OverrideDefaults()
    {
        var cut = Render<Skeleton>(p => p.Add(x => x.Width, "w-1/2").Add(x => x.Height, "h-10"));
        var classes = cut.Find("div").ClassList;
        classes.Should().Contain("w-1/2").And.Contain("h-10");
        classes.Should().NotContain("w-full");
    }

    [Fact]
    public void Rounded_False_OmitsRoundedClass()
    {
        var cut = Render<Skeleton>(p => p.Add(x => x.Rounded, false));
        cut.Find("div").ClassList.Should().NotContain("rounded");
    }

    [Fact]
    public void Animate_False_UsesStaticClass()
    {
        var cut = Render<Skeleton>(p => p.Add(x => x.Animate, false));
        var classes = cut.Find("div").ClassList;
        classes.Should().Contain("skeleton-static");
        classes.Should().NotContain("skeleton");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Skeleton>(p => p.Add(x => x.Class, "my-extra").AddUnmatched("data-testid", "my-skeleton"));
        var div = cut.Find("div");
        div.ClassList.Should().Contain("my-extra");
        div.GetAttribute("data-testid").Should().Be("my-skeleton");
    }
}
