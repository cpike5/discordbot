using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class CardTests : BlazorComponentTestContext
{
    [Fact]
    public void PlainCard_Default_RendersTitleAndBody_WithCardClass()
    {
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "Default Card")
            .Add(x => x.Subtitle, "Subtitle")
            .AddChildContent("<p>Body</p>"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("bg-bg-secondary").And.Contain("border-border-primary");
        root.ClassList.Should().NotContain("card-enhanced");
        cut.Find("h3").TextContent.Should().Be("Default Card");
        var paragraphs = cut.FindAll("p").Select(p => p.TextContent).ToList();
        paragraphs.Should().Contain("Subtitle");
        paragraphs.Should().Contain("Body");
    }

    [Theory]
    [InlineData(CardVariant.Flat, "bg-bg-secondary")]
    [InlineData(CardVariant.Elevated, "shadow-lg")]
    public void Variant_AppliesExpectedClass_OnPlainCardPath(CardVariant variant, string expectedClass)
    {
        var cut = Render<Card>(p => p.Add(x => x.Title, "x").Add(x => x.Variant, variant));
        cut.Find("div").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(CardAccent.Blue, "accent-blue")]
    [InlineData(CardAccent.Orange, "accent-orange")]
    [InlineData(CardAccent.Success, "accent-success")]
    [InlineData(CardAccent.Info, "accent-info")]
    public void Accent_SwitchesToEnhancedMarkup(CardAccent accent, string accentClass)
    {
        var cut = Render<Card>(p => p.Add(x => x.Title, "x").Add(x => x.Accent, accent));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("card-enhanced");
        root.ClassList.Should().Contain(accentClass);
    }

    [Fact]
    public void Accent_None_DoesNotUseEnhancedMarkup()
    {
        var cut = Render<Card>(p => p.Add(x => x.Title, "x").Add(x => x.Accent, CardAccent.None));
        cut.Find("div").ClassList.Should().NotContain("card-enhanced");
    }

    [Fact]
    public void HeaderContent_OverridesTitleSubtitle()
    {
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "ignored")
            .Add(x => x.HeaderContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='custom-header'>Custom</span>"))));

        cut.Find("[data-testid='custom-header']").TextContent.Should().Be("Custom");
        cut.FindAll("h3").Should().BeEmpty();
    }

    [Fact]
    public void HeaderActions_And_FooterContent_Render()
    {
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.HeaderActions, (RenderFragment)(b => b.AddMarkupContent(0, "<button data-testid='header-action'>Edit</button>")))
            .Add(x => x.FooterContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='footer'>Footer</span>"))));

        cut.Find("[data-testid='header-action']").Should().NotBeNull();
        cut.Find("[data-testid='footer']").Should().NotBeNull();
    }

    [Fact]
    public void IsInteractive_InvokesOnClick()
    {
        var clicked = false;
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.IsInteractive, true)
            .Add(x => x.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, () => clicked = true)));

        cut.Find("div").Click();

        clicked.Should().BeTrue();
    }

    [Fact]
    public void NotInteractive_DoesNotAttachClickHandlerAtAll()
    {
        // @onclick is now only rendered when IsInteractive (item 14 regression guard) - bUnit
        // throws MissingEventHandlerException when asked to dispatch an event with no registered
        // handler, which is the clearest proof the handler was never attached to the DOM at all
        // (rather than attached-but-a-no-op, the previous behavior).
        var clicked = false;
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.IsInteractive, false)
            .Add(x => x.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, () => clicked = true)));

        var act = () => cut.Find("div").Click();

        act.Should().Throw<MissingEventHandlerException>();
        clicked.Should().BeFalse();
    }

    [Fact]
    public void IsCollapsible_TogglesExpanded_AndRaisesIsExpandedChanged()
    {
        bool? changedTo = null;
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "Collapsible")
            .Add(x => x.IsCollapsible, true)
            .Add(x => x.IsExpanded, true)
            .Add(x => x.IsExpandedChanged, EventCallback.Factory.Create<bool>(this, v => changedTo = v))
            .AddChildContent("<p>Body</p>"));

        cut.Find("button[aria-expanded='true']").Should().NotBeNull();

        cut.Find("button").Click();

        changedTo.Should().Be(false);
        cut.Find("button[aria-expanded='false']").Should().NotBeNull();
        cut.Find("div.hidden").Should().NotBeNull();
    }

    [Fact]
    public void IsCollapsible_ChevronIcon_RendersRealPathData()
    {
        // Regression guard: <Icon Path="IconPaths.ChevronDown" ...> previously lacked the "@"
        // prefix a string-typed component parameter needs to be evaluated as C# (Blazor treats an
        // unprefixed value as a literal string for string parameters), so it rendered the literal
        // text "IconPaths.ChevronDown" as the SVG `d` instead of the actual path data.
        var cut = Render<Card>(p => p.Add(x => x.Title, "x").Add(x => x.IsCollapsible, true).AddChildContent("<p>Body</p>"));

        var d = cut.Find("button svg path").GetAttribute("d");

        d.Should().StartWith("M");
        d.Should().NotContain("IconPaths");
        d.Should().Be(IconPaths.ChevronDown);
    }

    [Fact]
    public void Id_IsRenderedOnRoot()
    {
        var cut = Render<Card>(p => p.Add(x => x.Title, "x").Add(x => x.Id, "my-card"));
        cut.Find("div#my-card").Should().NotBeNull();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Card>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-card"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-card");
    }

    [Fact]
    public void NoHeaderOrFooter_RendersBodyOnly()
    {
        var cut = Render<Card>(p => p.AddChildContent("<p data-testid='body'>Just body</p>"));

        cut.FindAll("h3").Should().BeEmpty();
        cut.Find("[data-testid='body']").Should().NotBeNull();
    }
}
