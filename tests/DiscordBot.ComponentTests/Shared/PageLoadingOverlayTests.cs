using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class PageLoadingOverlayTests : TestContext
{
    [Fact]
    public void RendersHiddenOverlay_WithIdAndMessage()
    {
        var cut = RenderComponent<PageLoadingOverlay>(p => p
            .Add(x => x.Model, new PageLoadingOverlayViewModel
            {
                Id = "myOverlay",
                Message = "Loading data...",
                SubMessage = "Please wait"
            }));

        var overlay = cut.Find("#myOverlay");
        overlay.ClassList.Should().Contain("loading-overlay");
        overlay.ClassList.Should().Contain("hidden");
        cut.Markup.Should().Contain("Loading data...");
        cut.Markup.Should().Contain("Please wait");
    }

    [Fact]
    public void RendersDotsSpinner_WhenDotsVariant()
    {
        var cut = RenderComponent<PageLoadingOverlay>(p => p
            .Add(x => x.Model, new PageLoadingOverlayViewModel { Variant = SpinnerVariant.Dots }));

        cut.FindAll("div.animate-bounce").Should().HaveCount(3);
        cut.FindAll("div.animate-spin").Should().BeEmpty();
    }

    [Fact]
    public void OmitsCancelButton_ByDefault()
    {
        var cut = RenderComponent<PageLoadingOverlay>(p => p
            .Add(x => x.Model, new PageLoadingOverlayViewModel()));

        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void CancelButton_RaisesOnCancel()
    {
        var cancelled = false;
        var cut = RenderComponent<PageLoadingOverlay>(p => p
            .Add(x => x.Model, new PageLoadingOverlayViewModel
            {
                Id = "overlay",
                ShowCancelButton = true,
                CancelText = "Stop"
            })
            .Add(x => x.OnCancel, () => cancelled = true));

        var button = cut.Find("#overlayCancelBtn");
        button.TextContent.Trim().Should().Be("Stop");
        button.Click();

        cancelled.Should().BeTrue();
    }
}
