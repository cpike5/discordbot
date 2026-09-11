using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class AlertTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTitleAndMessage_WithRoleAndAriaLive()
    {
        var cut = Render<Alert>(p => p
            .Add(x => x.Title, "Information")
            .Add(x => x.Message, "Some info."));

        var root = cut.Find("div[role='alert']");
        root.GetAttribute("aria-live").Should().Be("polite");
        cut.Find("h3").TextContent.Should().Be("Information");
        cut.Find("p").TextContent.Should().Be("Some info.");
    }

    [Theory]
    [InlineData(AlertVariant.Info, "bg-info/10", "text-info")]
    [InlineData(AlertVariant.Success, "bg-success/10", "text-success")]
    [InlineData(AlertVariant.Warning, "bg-warning/10", "text-warning")]
    [InlineData(AlertVariant.Error, "bg-error/10", "text-error")]
    public void Variant_AppliesExpectedClasses(AlertVariant variant, string bgClass, string textClass)
    {
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x").Add(x => x.Variant, variant));

        var root = cut.Find("div[role='alert']");
        root.ClassList.Should().Contain(bgClass);
        root.ClassList.Should().Contain(textClass);
    }

    [Fact]
    public void ShowIcon_False_OmitsIcon()
    {
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x").Add(x => x.ShowIcon, false));
        cut.FindAll("svg").Should().BeEmpty();
    }

    [Fact]
    public void ShowIcon_True_RendersIcon_Default()
    {
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x"));
        cut.FindAll("svg").Should().ContainSingle();
    }

    [Fact]
    public void IsDismissible_DismissButtonIcon_RendersRealPathData()
    {
        // Regression guard: the dismiss button's <Icon Path="IconPaths.XMark" ...> previously
        // lacked the "@" prefix a string-typed component parameter needs to be evaluated as C#
        // (see docs/lessons-learned - Blazor treats an unprefixed value as a literal string for
        // string parameters), so it rendered the literal text "IconPaths.XMark" as the SVG `d`.
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x").Add(x => x.IsDismissible, true));

        var d = cut.Find("button[aria-label='Dismiss'] svg path").GetAttribute("d");

        d.Should().StartWith("M");
        d.Should().NotContain("IconPaths");
        d.Should().Be(IconPaths.XMark);
    }

    [Fact]
    public void IsDismissible_RendersDismissButton()
    {
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x").Add(x => x.IsDismissible, true));
        cut.Find("button[aria-label='Dismiss']").Should().NotBeNull();
        cut.FindAll("svg").Should().HaveCount(2); // alert icon + dismiss icon
    }

    [Fact]
    public void Dismiss_WithoutOnDismissDelegate_SelfHides()
    {
        var cut = Render<Alert>(p => p.Add(x => x.Message, "x").Add(x => x.IsDismissible, true));

        cut.Find("button[aria-label='Dismiss']").Click();

        cut.FindAll("div[role='alert']").Should().BeEmpty();
    }

    [Fact]
    public void Dismiss_WithOnDismissDelegate_InvokesCallback_AndDoesNotSelfHide()
    {
        var dismissed = false;
        var cut = Render<Alert>(p => p
            .Add(x => x.Message, "x")
            .Add(x => x.IsDismissible, true)
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissed = true)));

        cut.Find("button[aria-label='Dismiss']").Click();

        dismissed.Should().BeTrue();
        // The component defers to the caller rather than hiding itself - still present because
        // the test never removes it (the caller in this test doesn't re-render with it gone).
        cut.FindAll("div[role='alert']").Should().ContainSingle();
    }

    [Fact]
    public void ChildContent_OverridesMessage()
    {
        var cut = Render<Alert>(p => p
            .Add(x => x.Message, "ignored")
            .AddChildContent("<span data-testid='custom'>Custom body</span>"));

        cut.Find("[data-testid='custom']").TextContent.Should().Be("Custom body");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Alert>(p => p
            .Add(x => x.Message, "x")
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-alert"));

        var root = cut.Find("div[role='alert']");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-alert");
    }
}
