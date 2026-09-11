using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class LoadingSpinnerTests : BlazorComponentTestContext
{
    [Fact]
    public void Default_RendersSimpleSpinner_Inline()
    {
        var cut = Render<LoadingSpinner>();

        var root = cut.Find("div");
        root.ClassList.Should().Contain("inline-flex");
        root.QuerySelector(".animate-spin").Should().NotBeNull();
    }

    [Fact]
    public void Variant_Dots_RendersThreeDots()
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Variant, SpinnerVariant.Dots));
        cut.FindAll(".animate-bounce").Should().HaveCount(3);
    }

    [Fact]
    public void Variant_Pulse_RendersPingAndPulse()
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Variant, SpinnerVariant.Pulse));
        cut.Find(".animate-ping").Should().NotBeNull();
        cut.Find(".animate-pulse").Should().NotBeNull();
    }

    [Theory]
    [InlineData(SpinnerSize.Small, "w-6")]
    [InlineData(SpinnerSize.Medium, "w-10")]
    [InlineData(SpinnerSize.Large, "w-16")]
    public void Size_AppliesExpectedOuterClass(SpinnerSize size, string expectedClass)
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Size, size));
        cut.Find(".animate-spin").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(SpinnerColor.Blue, "border-t-accent-blue")]
    [InlineData(SpinnerColor.Orange, "border-t-accent-orange")]
    [InlineData(SpinnerColor.White, "border-t-white")]
    public void Color_AppliesExpectedClass_SimpleVariant(SpinnerColor color, string expectedClass)
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Color, color));
        cut.Find(".animate-spin").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Message_And_SubMessage_Render()
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Message, "Loading data...").Add(x => x.SubMessage, "Please wait"));

        cut.Find("p").TextContent.Should().Be("Loading data...");
        cut.FindAll("p").Should().HaveCount(2);
        cut.Find("div").ClassList.Should().Contain("flex-col"); // switches from inline-flex when Message present
    }

    [Fact]
    public void IsOverlay_RendersAbsoluteOverlayWrapper()
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.IsOverlay, true));
        var root = cut.Find("div");
        root.ClassList.Should().Contain("absolute").And.Contain("inset-0");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<LoadingSpinner>(p => p.Add(x => x.Class, "my-extra").AddUnmatched("data-testid", "my-spinner"));
        var root = cut.Find("div");
        root.ClassList.Should().Contain("my-extra");
        root.GetAttribute("data-testid").Should().Be("my-spinner");
    }
}
