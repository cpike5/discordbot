using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class ConfirmModalTests : TestContext
{
    [Fact]
    public void RendersNothing_UntilShown()
    {
        var cut = RenderComponent<ConfirmModal>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAsync_RendersTitleAndMessage()
    {
        var cut = RenderComponent<ConfirmModal>();

        await cut.InvokeAsync(() => _ = cut.Instance.ShowAsync(new ConfirmModal.ConfirmRequest
        {
            Title = "Delete Tag",
            Message = "This cannot be undone.",
            ConfirmText = "Delete",
            CancelText = "Keep",
            Variant = ConfirmationVariant.Danger
        }));

        cut.Find("[role=alertdialog]").TextContent.Should().Contain("Delete Tag");
        cut.Find("[role=alertdialog]").TextContent.Should().Contain("This cannot be undone.");
    }

    [Fact]
    public async Task Confirm_CompletesTrue_AndHides()
    {
        var cut = RenderComponent<ConfirmModal>();
        Task<bool> result = Task.FromResult(false);

        await cut.InvokeAsync(() =>
            result = cut.Instance.ShowAsync(new ConfirmModal.ConfirmRequest { Title = "T", Message = "M", ConfirmText = "Yes" }));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Yes").Click();

        (await result).Should().BeTrue();
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_CompletesFalse()
    {
        var cut = RenderComponent<ConfirmModal>();
        Task<bool> result = Task.FromResult(true);

        await cut.InvokeAsync(() =>
            result = cut.Instance.ShowAsync(new ConfirmModal.ConfirmRequest { Title = "T", Message = "M", CancelText = "No" }));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "No").Click();

        (await result).Should().BeFalse();
    }

    [Fact]
    public async Task BackdropClick_CompletesFalse()
    {
        var cut = RenderComponent<ConfirmModal>();
        Task<bool> result = Task.FromResult(true);

        await cut.InvokeAsync(() =>
            result = cut.Instance.ShowAsync(new ConfirmModal.ConfirmRequest { Title = "T", Message = "M" }));

        cut.Find("div.bg-black\\/70").Click();

        (await result).Should().BeFalse();
    }
}
