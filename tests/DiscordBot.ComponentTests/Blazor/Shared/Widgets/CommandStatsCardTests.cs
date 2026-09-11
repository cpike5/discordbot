using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class CommandStatsCardTests : BlazorComponentTestContext
{
    private static readonly CommandUsageStat[] TopCommands =
    [
        new("/help", 100, 1),
        new("/play", 50, 2)
    ];

    [Fact]
    public void NoTopCommands_RendersEmptyState_NoChart()
    {
        var cut = Render<CommandStatsCard>(p => p.Add(x => x.TotalCommands, 0));

        cut.Markup.Should().Contain("No command data available");
        cut.FindAll("canvas").Should().BeEmpty();
    }

    [Fact]
    public void TopCommands_RendersChart_ThroughChartInterop()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(1);

        var cut = Render<CommandStatsCard>(p => p
            .Add(x => x.TopCommands, TopCommands)
            .Add(x => x.TotalCommands, 150));

        cut.Find("[data-total-count]").TextContent.Should().Be("150");
        cut.Markup.Should().Contain("Showing top 2 commands");

        cut.WaitForAssertion(() =>
        {
            JSInterop.Invocations["import"].Should().Contain(inv => (string)inv.Arguments[0]! == "./js/blazor/charts.js");
            createHandler.Invocations.Should().ContainSingle();
        });
    }

    [Fact]
    public void TimeRangeSelect_ChangeInvokesOnTimeRangeChanged()
    {
        int? received = -1;
        var cut = Render<CommandStatsCard>(p => p
            .Add(x => x.TopCommands, TopCommands)
            .Add(x => x.OnTimeRangeChanged, h => received = h));

        cut.Find("select#commandTimeRange").Change("168");
        received.Should().Be(168);

        cut.Find("select#commandTimeRange").Change("");
        received.Should().BeNull();
    }
}
