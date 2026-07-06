using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class LoadingSpinnerTests : TestContext
{
    [Fact]
    public void RendersSimpleSpinner_WithDefaultSizeAndColor()
    {
        var cut = RenderComponent<LoadingSpinner>(p => p
            .Add(x => x.Model, new LoadingSpinnerViewModel()));

        var spinner = cut.Find(".animate-spin");
        spinner.ClassList.Should().Contain(new[] { "w-10", "h-10", "border-t-accent-blue" });
    }

    [Fact]
    public void DotsVariant_RendersThreeBouncingDots()
    {
        var cut = RenderComponent<LoadingSpinner>(p => p
            .Add(x => x.Model, new LoadingSpinnerViewModel
            {
                Variant = SpinnerVariant.Dots,
                Color = SpinnerColor.Orange
            }));

        var dots = cut.FindAll(".animate-bounce").ToList();
        dots.Should().HaveCount(3);
        dots.First().ClassList.Should().Contain("bg-accent-orange");
    }

    [Fact]
    public void OverlayMode_WrapsInAbsoluteContainer_WithMessages()
    {
        var cut = RenderComponent<LoadingSpinner>(p => p
            .Add(x => x.Model, new LoadingSpinnerViewModel
            {
                IsOverlay = true,
                Message = "Loading data",
                SubMessage = "Please wait"
            }));

        var root = cut.Find("div");
        root.ClassList.Should().Contain(new[] { "absolute", "inset-0" });
        cut.Markup.Should().Contain("Loading data");
        cut.Markup.Should().Contain("Please wait");
    }

    [Fact]
    public void InlineWithoutMessage_UsesInlineFlexContainer()
    {
        var cut = RenderComponent<LoadingSpinner>(p => p
            .Add(x => x.Model, new LoadingSpinnerViewModel()));

        cut.Find("div").ClassList.Should().Contain("inline-flex");
    }
}
