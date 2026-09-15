using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class EmptyStateTests : BlazorComponentTestContext
{
    [Fact]
    public void Default_RendersTitleAndDescription()
    {
        var cut = Render<EmptyState>();

        cut.Find("h3").TextContent.Should().Be("No Data");
        cut.Find("p").TextContent.Should().Be("There are no items to display.");
        cut.Find("path").GetAttribute("d").Should().Be(IconPaths.FolderOpen);
    }

    [Theory]
    [InlineData(EmptyStateType.NoData, "1.5")]
    [InlineData(EmptyStateType.NoResults, "2")]
    [InlineData(EmptyStateType.FirstTime, "2")]
    [InlineData(EmptyStateType.Error, "2")]
    [InlineData(EmptyStateType.NoPermission, "2")]
    [InlineData(EmptyStateType.Offline, "2")]
    public void Type_UsesExpectedStrokeWidth(EmptyStateType type, string expectedStrokeWidth)
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Type, type));
        cut.Find("path").GetAttribute("stroke-width").Should().Be(expectedStrokeWidth);
    }

    [Theory]
    [InlineData(EmptyStateType.NoResults)]
    [InlineData(EmptyStateType.FirstTime)]
    [InlineData(EmptyStateType.Error)]
    [InlineData(EmptyStateType.NoPermission)]
    [InlineData(EmptyStateType.Offline)]
    public void Type_UsesDifferentIconPath_ThanDefault(EmptyStateType type)
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Type, type));
        cut.Find("path").GetAttribute("d").Should().NotBe(IconPaths.FolderOpen);
    }

    [Fact]
    public void IconPath_Override_TakesPrecedenceOverType()
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Type, EmptyStateType.NoData).Add(x => x.IconPath, IconPaths.Plus));
        cut.Find("path").GetAttribute("d").Should().Be(IconPaths.Plus);
    }

    [Theory]
    [InlineData(EmptyStateSize.Compact, "w-10")]
    [InlineData(EmptyStateSize.Default, "w-16")]
    [InlineData(EmptyStateSize.Large, "w-20")]
    public void Size_AppliesExpectedIconDimension(EmptyStateSize size, string expectedClass)
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Size, size));
        cut.Find("svg").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void PrimaryActionHref_RendersAnchor()
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.PrimaryActionText, "Create").Add(x => x.PrimaryActionHref, "/create"));

        var anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Be("/create");
        anchor.TextContent.Should().Contain("Create");
    }

    [Fact]
    public void PrimaryActionHref_ActionIcon_RendersRealPathData()
    {
        // Regression guard: the primary-action <Icon Path="IconPaths.Plus" ...> (anchor variant)
        // previously lacked the "@" prefix a string-typed component parameter needs to be
        // evaluated as C# (Blazor treats an unprefixed value as a literal string for string
        // parameters), so it rendered the literal text "IconPaths.Plus" as the SVG `d`.
        var cut = Render<EmptyState>(p => p.Add(x => x.PrimaryActionText, "Create").Add(x => x.PrimaryActionHref, "/create"));

        var d = cut.Find("a svg path").GetAttribute("d");

        d.Should().StartWith("M");
        d.Should().NotContain("IconPaths");
        d.Should().Be(IconPaths.Plus);
    }

    [Fact]
    public void PrimaryAction_ButtonVariant_ActionIcon_RendersRealPathData()
    {
        // Same regression guard as above, for the <button> variant of the primary action (no
        // PrimaryActionHref) - a separate <Icon Path="IconPaths.Plus" ...> in the partial's markup.
        var cut = Render<EmptyState>(p => p.Add(x => x.PrimaryActionText, "Retry"));

        var d = cut.Find("button svg path").GetAttribute("d");

        d.Should().StartWith("M");
        d.Should().NotContain("IconPaths");
        d.Should().Be(IconPaths.Plus);
    }

    [Fact]
    public void PrimaryAction_WithoutHref_RendersButton_AndInvokesCallback()
    {
        var invoked = false;
        var cut = Render<EmptyState>(p => p
            .Add(x => x.PrimaryActionText, "Retry")
            .Add(x => x.OnPrimaryAction, EventCallback.Factory.Create<MouseEventArgs>(this, () => invoked = true)));

        var button = cut.Find("button");
        button.TextContent.Should().Contain("Retry");

        button.Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void SecondaryAction_RequiresBothTextAndHref()
    {
        var cutMissingHref = Render<EmptyState>(p => p
            .Add(x => x.PrimaryActionText, "Clear")
            .Add(x => x.SecondaryActionText, "View All"));
        cutMissingHref.FindAll("a").Should().BeEmpty();

        var cutWithBoth = Render<EmptyState>(p => p
            .Add(x => x.PrimaryActionText, "Clear")
            .Add(x => x.SecondaryActionText, "View All")
            .Add(x => x.SecondaryActionHref, "/all"));
        var secondaryLink = cutWithBoth.Find("a[href='/all']");
        secondaryLink.TextContent.Should().Contain("View All");
    }

    [Fact]
    public void NoActions_RendersNoButtonsOrLinks()
    {
        var cut = Render<EmptyState>();
        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Class, "my-extra").AddUnmatched("data-testid", "my-empty-state"));
        var root = cut.Find("div.flex.flex-col.items-center");
        root.ClassList.Should().Contain("my-extra");
        root.GetAttribute("data-testid").Should().Be("my-empty-state");
    }
}
