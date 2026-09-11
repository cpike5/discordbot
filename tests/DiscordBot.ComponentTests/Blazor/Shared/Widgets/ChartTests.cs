using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class ChartTests : BlazorComponentTestContext
{
    [Fact]
    public void FirstRender_CreatesChart_ThroughChartInterop()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(1);

        var cut = Render<Chart>(p => p
            .Add(x => x.Type, "bar")
            .Add(x => x.Data, new { labels = new[] { "a" }, datasets = Array.Empty<object>() }));

        cut.Find("canvas").Should().NotBeNull();
        cut.WaitForAssertion(() => createHandler.Invocations.Should().ContainSingle());
    }

    [Fact]
    public void DataReferenceChange_CallsUpdate_NotCreateAgain()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(7);
        var updateHandler = moduleInterop.SetupVoid("update", _ => true);

        var data1 = new { labels = new[] { "a" }, datasets = Array.Empty<object>() };
        var cut = Render<Chart>(p => p.Add(x => x.Type, "bar").Add(x => x.Data, data1));
        cut.WaitForAssertion(() => createHandler.Invocations.Should().ContainSingle());

        var data2 = new { labels = new[] { "b" }, datasets = Array.Empty<object>() };
        cut.Render(p => p.Add(x => x.Type, "bar").Add(x => x.Data, data2));

        cut.WaitForAssertion(() => updateHandler.Invocations.Should().ContainSingle());
        createHandler.Invocations.Should().ContainSingle();
    }

    [Fact]
    public void SameDataReference_RerenderDoesNotCallUpdate()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(3);
        var updateHandler = moduleInterop.SetupVoid("update", _ => true);

        var data = new { labels = new[] { "a" }, datasets = Array.Empty<object>() };
        var cut = Render<Chart>(p => p.Add(x => x.Type, "bar").Add(x => x.Data, data).Add(x => x.Class, "x"));
        cut.WaitForAssertion(() => createHandler.Invocations.Should().ContainSingle());

        cut.Render(p => p.Add(x => x.Type, "bar").Add(x => x.Data, data).Add(x => x.Class, "y"));

        updateHandler.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispose_DestroysChart()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        var createHandler = moduleInterop.Setup<int>("create", _ => true);
        createHandler.SetResult(9);
        var destroyHandler = moduleInterop.SetupVoid("destroy", _ => true);

        var cut = Render<Chart>(p => p.Add(x => x.Type, "bar").Add(x => x.Data, new { }));
        cut.WaitForAssertion(() => createHandler.Invocations.Should().ContainSingle());

        await DisposeComponentsAsync();

        destroyHandler.Invocations.Should().ContainSingle();
    }

    [Fact]
    public void Height_AppliesInlineStyle()
    {
        var cut = Render<Chart>(p => p.Add(x => x.Type, "line").Add(x => x.Data, new { }).Add(x => x.Height, "250px"));
        cut.Find("div").GetAttribute("style").Should().Contain("height: 250px");
    }
}
