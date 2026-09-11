using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class BadgeTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersText_WithDefaultClasses()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "Default"));

        var span = cut.Find("span");
        span.TextContent.Trim().Should().Be("Default");
        span.ClassList.Should().Contain("badge");
        span.ClassList.Should().Contain("badge-gray");
    }

    [Theory]
    [InlineData(BadgeVariant.Default, "badge-gray")]
    [InlineData(BadgeVariant.Orange, "badge-orange")]
    [InlineData(BadgeVariant.Blue, "badge-blue")]
    [InlineData(BadgeVariant.Success, "badge-success")]
    [InlineData(BadgeVariant.Warning, "badge-warning")]
    [InlineData(BadgeVariant.Error, "badge-error")]
    [InlineData(BadgeVariant.Info, "badge-info")]
    public void Variant_AppliesExpectedClass(BadgeVariant variant, string expectedClass)
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.Variant, variant));
        cut.Find("span").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(BadgeSize.Small, "badge-sm")]
    [InlineData(BadgeSize.Large, "badge-lg")]
    public void Size_AppliesExpectedClass(BadgeSize size, string expectedClass)
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.Size, size));
        cut.Find("span").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Size_Medium_HasNoSizeClass()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.Size, BadgeSize.Medium));
        cut.Find("span").ClassList.Should().NotContain("badge-sm").And.NotContain("badge-lg");
    }

    [Theory]
    [InlineData(BadgeStyle.Outline, "badge-outline")]
    [InlineData(BadgeStyle.Subtle, "badge-subtle")]
    public void Style_AppliesExpectedClass(BadgeStyle style, string expectedClass)
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.Style, style));
        cut.Find("span").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void IconLeft_RendersSolid20x20Svg_NotThroughIconComponent()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.IconLeft, "M10 10"));

        var svg = cut.Find("span > svg");
        svg.GetAttribute("viewBox").Should().Be("0 0 20 20");
        svg.GetAttribute("fill").Should().Be("currentColor");
        svg.QuerySelector("path")!.GetAttribute("fill-rule").Should().Be("evenodd");
    }

    [Fact]
    public void IsRemovable_RendersRemoveButton_AndInvokesOnRemove()
    {
        var removed = false;
        var cut = Render<Badge>(p => p
            .Add(x => x.Text, "Dismiss me")
            .Add(x => x.IsRemovable, true)
            .Add(x => x.OnRemove, EventCallback.Factory.Create(this, () => removed = true)));

        var removeButton = cut.Find("button.badge-remove");
        removeButton.GetAttribute("aria-label").Should().Be("Remove");

        removeButton.Click();

        removed.Should().BeTrue();
    }

    [Fact]
    public void IsRemovable_WithoutOnRemove_DoesNotThrow()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.IsRemovable, true));

        var act = () => cut.Find("button.badge-remove").Click();

        act.Should().NotThrow();
    }

    [Fact]
    public void ChildContent_OverridesText()
    {
        var cut = Render<Badge>(p => p
            .Add(x => x.Text, "ignored")
            .AddChildContent("<em>Fancy</em>"));

        cut.Find("span em").TextContent.Should().Be("Fancy");
    }

    [Fact]
    public void Class_IsAppended()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").Add(x => x.Class, "extra"));
        cut.Find("span").ClassList.Should().Contain("extra");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Badge>(p => p.Add(x => x.Text, "x").AddUnmatched("data-testid", "my-badge"));
        cut.Find("span[data-testid='my-badge']").Should().NotBeNull();
    }
}
