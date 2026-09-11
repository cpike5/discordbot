using Bunit;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class LoadingOverlayTests : BlazorComponentTestContext
{
    [Fact]
    public void NotLoading_RendersNothing()
    {
        var cut = Render<LoadingOverlay>();
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void LoadingState_Begin_ShowsOverlay_WithMessage()
    {
        var loadingState = Services.GetRequiredService<ILoadingState>();
        var cut = Render<LoadingOverlay>();

        using var scope = loadingState.Begin("Fetching data...");

        cut.WaitForAssertion(() =>
        {
            cut.Find("div.loading-overlay").Should().NotBeNull();
            cut.Markup.Should().Contain("Fetching data...");
        });
    }

    [Fact]
    public void LoadingState_Dispose_HidesOverlay_WhenNoOtherScopesOpen()
    {
        var loadingState = Services.GetRequiredService<ILoadingState>();
        var cut = Render<LoadingOverlay>();

        var scope = loadingState.Begin("x");
        cut.WaitForAssertion(() => cut.Find("div.loading-overlay").Should().NotBeNull());

        scope.Dispose();

        cut.WaitForAssertion(() => cut.Markup.Trim().Should().BeEmpty());
    }

    [Fact]
    public void ShowCancelButton_OnlyWhenOnCancelHasDelegate()
    {
        var loadingState = Services.GetRequiredService<ILoadingState>();
        var cut = Render<LoadingOverlay>(p => p.Add(x => x.CancelText, "Stop"));

        using var scope = loadingState.Begin();

        cut.WaitForAssertion(() => cut.FindAll("button[aria-label='Stop']").Should().BeEmpty());
    }

    [Fact]
    public void OnCancel_RendersButton_AndInvokesCallback()
    {
        var loadingState = Services.GetRequiredService<ILoadingState>();
        var cancelled = false;
        var cut = Render<LoadingOverlay>(p => p
            .Add(x => x.CancelText, "Stop")
            .Add(x => x.OnCancel, Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, () => cancelled = true)));

        using var scope = loadingState.Begin();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Stop']").Should().NotBeNull());

        cut.Find("button[aria-label='Stop']").Click();

        cancelled.Should().BeTrue();
    }
}
