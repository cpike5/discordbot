using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class ButtonTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersButton_WithTextAndDefaultVariant()
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "Save"));

        var button = cut.Find("button");
        button.TextContent.Trim().Should().Be("Save");
        button.GetAttribute("type").Should().Be("button");
        button.ClassList.Should().Contain("bg-accent-orange");
    }

    [Theory]
    [InlineData(ButtonVariant.Primary, "bg-accent-orange")]
    [InlineData(ButtonVariant.Secondary, "border-border-primary")]
    [InlineData(ButtonVariant.Accent, "bg-accent-blue")]
    [InlineData(ButtonVariant.Danger, "bg-error")]
    [InlineData(ButtonVariant.Ghost, "text-text-secondary")]
    public void Variant_AppliesExpectedClass(ButtonVariant variant, string expectedClass)
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "x").Add(x => x.Variant, variant));
        cut.Find("button").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(ButtonSize.Small, "px-3")]
    [InlineData(ButtonSize.Medium, "px-5")]
    [InlineData(ButtonSize.Large, "px-6")]
    public void Size_AppliesExpectedClass(ButtonSize size, string expectedClass)
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "x").Add(x => x.Size, size));
        cut.Find("button").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void IsDisabled_DisablesButton()
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "x").Add(x => x.IsDisabled, true));
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void IsLoading_DisablesButton_ShowsSpinner_AndSrOnlyText()
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "x").Add(x => x.IsLoading, true));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button").GetAttribute("aria-busy").Should().Be("true");
        cut.Find("svg.animate-spin").Should().NotBeNull();
        cut.Find(".sr-only").TextContent.Should().Be("Loading");
    }

    [Fact]
    public void IsLoading_WithLoadingText_UsesLoadingText()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "x")
            .Add(x => x.IsLoading, true)
            .Add(x => x.LoadingText, "Saving..."));

        cut.Find(".sr-only").TextContent.Should().Be("Saving...");
    }

    [Fact]
    public void IconLeft_And_IconRight_RenderIcons()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Save")
            .Add(x => x.IconLeft, IconPaths.CheckCircle)
            .Add(x => x.IconRight, IconPaths.ChevronDown));

        cut.FindAll("svg path").Should().HaveCount(2);
    }

    [Fact]
    public void IconRight_Hidden_WhileLoading()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Save")
            .Add(x => x.IconRight, IconPaths.ChevronDown)
            .Add(x => x.IsLoading, true));

        // Only the spinner svg should be present, not the icon-right svg.
        cut.FindAll("svg").Should().ContainSingle();
    }

    [Fact]
    public void IsIconOnly_HidesText_AndUsesAriaLabel()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Settings")
            .Add(x => x.IsIconOnly, true)
            .Add(x => x.IconLeft, IconPaths.ChevronDown));

        var button = cut.Find("button");
        button.QuerySelector("span").Should().BeNull(); // text span is omitted entirely when icon-only
        button.GetAttribute("aria-label").Should().Be("Settings");
    }

    [Fact]
    public void AriaLabel_Explicit_OverridesTextFallback()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Settings")
            .Add(x => x.IsIconOnly, true)
            .Add(x => x.AriaLabel, "Open settings"));

        cut.Find("button").GetAttribute("aria-label").Should().Be("Open settings");
    }

    [Fact]
    public void OnClick_Invoked_WhenClicked()
    {
        var clicked = false;
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Save")
            .Add(x => x.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, () => clicked = true)));

        cut.Find("button").Click();

        clicked.Should().BeTrue();
    }

    [Fact]
    public void Href_RendersAnchor_NotButton()
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "Go").Add(x => x.Href, "/dashboard"));

        cut.FindAll("button").Should().BeEmpty();
        var anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Be("/dashboard");
    }

    [Fact]
    public void Href_WithIsDisabled_OmitsHrefAndMarksAriaDisabled()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "Go")
            .Add(x => x.Href, "/dashboard")
            .Add(x => x.IsDisabled, true));

        var anchor = cut.Find("a");
        anchor.HasAttribute("href").Should().BeFalse();
        anchor.GetAttribute("aria-disabled").Should().Be("true");
    }

    [Fact]
    public void Class_IsAppended()
    {
        var cut = Render<Button>(p => p.Add(x => x.Text, "x").Add(x => x.Class, "my-extra-class"));
        cut.Find("button").ClassList.Should().Contain("my-extra-class");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "x")
            .AddUnmatched("data-testid", "save-button"));

        cut.Find("button[data-testid='save-button']").Should().NotBeNull();
    }

    [Fact]
    public void ChildContent_OverridesText()
    {
        var cut = Render<Button>(p => p
            .Add(x => x.Text, "ignored")
            .AddChildContent("<strong>Bold Text</strong>"));

        cut.Find("button strong").TextContent.Should().Be("Bold Text");
    }
}
