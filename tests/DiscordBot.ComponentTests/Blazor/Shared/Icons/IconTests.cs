using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Icons;

public class IconTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersPath_AndDefaultsToHidden()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Path, IconPaths.XMark));

        var svg = cut.Find("svg");
        svg.GetAttribute("aria-hidden").Should().Be("true");
        cut.Find("path").GetAttribute("d").Should().Be(IconPaths.XMark);
        cut.FindAll("title").Should().BeEmpty();
    }

    [Theory]
    [InlineData(IconSize.XS, "w-3 h-3")]
    [InlineData(IconSize.SM, "w-4 h-4")]
    [InlineData(IconSize.MD, "w-5 h-5")]
    [InlineData(IconSize.LG, "w-6 h-6")]
    [InlineData(IconSize.XL, "w-8 h-8")]
    public void Size_MapsToExpectedClasses(IconSize size, string expectedClass)
    {
        var cut = Render<Icon>(p => p
            .Add(x => x.Path, IconPaths.ChevronDown)
            .Add(x => x.Size, size));

        cut.Find("svg").ClassList.Should().Contain(expectedClass.Split(' '));
    }

    [Fact]
    public void Size_Unset_HasNoSizeClass_ClassFullyControlsSizing()
    {
        var cut = Render<Icon>(p => p
            .Add(x => x.Path, IconPaths.FolderOpen)
            .Add(x => x.Class, "w-16 h-16 text-text-tertiary"));

        var classes = cut.Find("svg").ClassList;
        classes.Should().Contain("w-16");
        classes.Should().Contain("h-16");
        classes.Should().Contain("text-text-tertiary");
        classes.Should().NotContain("w-5"); // no default MD size leaking in
    }

    [Fact]
    public void Title_RendersTitleElement_AndUnhides()
    {
        var cut = Render<Icon>(p => p
            .Add(x => x.Path, IconPaths.InformationCircle)
            .Add(x => x.Title, "Information"));

        cut.Find("svg").GetAttribute("aria-hidden").Should().Be("false");
        cut.Find("title").TextContent.Should().Be("Information");
    }

    [Fact]
    public void StrokeWidth_Default_IsTwo()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Path, IconPaths.FolderOpen));
        cut.Find("path").GetAttribute("stroke-width").Should().Be("2");
    }

    [Fact]
    public void StrokeWidth_AcceptsFractionalOverride()
    {
        var cut = Render<Icon>(p => p
            .Add(x => x.Path, IconPaths.FolderOpen)
            .Add(x => x.StrokeWidth, "1.5"));

        cut.Find("path").GetAttribute("stroke-width").Should().Be("1.5");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Icon>(p => p
            .Add(x => x.Path, IconPaths.XMark)
            .AddUnmatched("data-testid", "my-icon"));

        cut.Find("svg[data-testid='my-icon']").Should().NotBeNull();
    }
}
