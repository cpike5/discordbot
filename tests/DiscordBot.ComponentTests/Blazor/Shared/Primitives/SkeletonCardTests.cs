using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class SkeletonCardTests : BlazorComponentTestContext
{
    [Fact]
    public void Default_RendersStatsLayout_WithCardClass_NoHeader()
    {
        var cut = Render<SkeletonCard>();

        cut.Find("div.card").Should().NotBeNull();
        cut.FindAll(".card-header").Should().BeEmpty();
        cut.Find(".card-body").Should().NotBeNull();
    }

    [Fact]
    public void ShowHeader_RendersCardHeader()
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.ShowHeader, true));
        cut.Find(".card-header").Should().NotBeNull();
    }

    [Theory]
    [InlineData(SkeletonCardType.Stats)]
    [InlineData(SkeletonCardType.Server)]
    [InlineData(SkeletonCardType.Activity)]
    [InlineData(SkeletonCardType.Table)]
    public void Type_RendersWithoutError(SkeletonCardType type)
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.Type, type));
        cut.Find(".card-body").QuerySelectorAll(".skeleton").Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Server_RendersThreeStatColumns()
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.Type, SkeletonCardType.Server));
        cut.FindAll(".grid-cols-3 > div").Should().HaveCount(3);
    }

    [Fact]
    public void Activity_RendersThreeRows()
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.Type, SkeletonCardType.Activity));
        cut.FindAll(".card-body > div > div").Should().HaveCount(3);
    }

    [Fact]
    public void Table_RendersFiveRows()
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.Type, SkeletonCardType.Table));
        cut.FindAll(".card-body > div > div").Should().HaveCount(5);
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<SkeletonCard>(p => p.Add(x => x.Class, "my-extra").AddUnmatched("data-testid", "my-skeleton-card"));
        var div = cut.Find("div.card");
        div.ClassList.Should().Contain("my-extra");
        div.GetAttribute("data-testid").Should().Be("my-skeleton-card");
    }
}
