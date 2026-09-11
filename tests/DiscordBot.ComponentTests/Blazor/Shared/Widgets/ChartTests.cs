using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.JSInterop;

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

    /// <summary>
    /// Regression guard (plan §5 Phase 2 step 4): a missing vendored chart.umd.js - the real
    /// failure mode in an environment that skipped the npm build:vendor step (e.g.
    /// <c>dotnet build -p:SkipTailwind=true</c>) - throws a <see cref="JSException"/> from
    /// ChartInterop.CreateAsync. Before this fix that propagated out of OnAfterRenderAsync
    /// unhandled and crashed the whole circuit; now it must be caught, logged, and replaced with
    /// an inline fallback instead of the canvas.
    /// </summary>
    [Fact]
    public void CreateThrowsJSException_RendersFallback_InsteadOfCrashing()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/charts.js");
        moduleInterop.Setup<int>("create", _ => true).SetException(new JSException("Failed to load Chart.js from /lib/chart.js/chart.umd.js"));

        var cut = Render<Chart>(p => p
            .Add(x => x.Type, "bar")
            .Add(x => x.Data, new { labels = new[] { "a" }, datasets = Array.Empty<object>() }));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("canvas").Should().BeEmpty();
            cut.Markup.Should().Contain("Chart unavailable");
        });
    }
}
