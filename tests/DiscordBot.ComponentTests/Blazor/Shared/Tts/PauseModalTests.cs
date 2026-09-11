using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class PauseModalTests : BlazorComponentTestContext
{
    // ShowAsync() calls StateHasChanged(), which requires running on the renderer's dispatcher -
    // calling cut.Instance.ShowAsync() directly from the test's own thread throws
    // "current thread is not associated with the Dispatcher". Routing it through
    // cut.InvokeAsync (as the bUnit docs recommend for any component-state-mutating call made
    // from outside an event trigger) dispatches the synchronous part of ShowAsync correctly and
    // applies the resulting render before returning.
    private static async Task<Task<int?>> ShowAsync(Bunit.IRenderedComponent<PauseModal> cut)
    {
        // The lambda body must be a *statement* (braces), not an expression that evaluates to a
        // Task - cut.InvokeAsync(() => showTask = cut.Instance.ShowAsync()) would bind to the
        // Func<Task> overload instead of Action (the assignment's value is itself a Task<int?>,
        // which converts to Task), and InvokeAsync would then await ShowAsync()'s OWN task -
        // which only completes when the modal is closed - hanging the test forever.
        Task<int?>? showTask = null;
        await cut.InvokeAsync(() => { showTask = cut.Instance.ShowAsync(); });
        return showTask!;
    }

    [Fact]
    public void NotShown_RendersNothing()
    {
        var cut = Render<PauseModal>();

        cut.FindAll("[role='dialog']").Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAsync_RendersModalWithDefaultDuration()
    {
        var cut = Render<PauseModal>(p => p.Add(x => x.DefaultDuration, 500));

        await ShowAsync(cut);

        cut.Find("[role='dialog']").Should().NotBeNull();
        cut.Find("input[type=range]").GetAttribute("value").Should().Be("500");
    }

    [Fact]
    public async Task SliderInput_UpdatesDisplayedDurationAndPreview()
    {
        var cut = Render<PauseModal>();
        await ShowAsync(cut);

        cut.Find("input[type=range]").Input("1500");

        cut.Find(".text-4xl").TextContent.Should().Be("1500");
        cut.Markup.Should().Contain("1500ms]");
    }

    [Fact]
    public async Task QuickPreset_SelectsThatDuration()
    {
        var cut = Render<PauseModal>();
        await ShowAsync(cut);

        cut.FindAll("button").Single(b => b.TextContent.Contains("1000ms")).Click();

        cut.Find(".text-4xl").TextContent.Should().Be("1000");
    }

    [Fact]
    public async Task Insert_ResolvesShowAsyncWithDuration_AndInvokesOnInsert()
    {
        int? inserted = null;
        var cut = Render<PauseModal>(p => p.Add(x => x.OnInsert, EventCallback.Factory.Create<int>(this, v => inserted = v)));

        var showTask = await ShowAsync(cut);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Insert Pause").Click();

        var result = await showTask;
        result.Should().Be(500);
        inserted.Should().Be(500);
        cut.FindAll("[role='dialog']").Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_ResolvesShowAsyncWithNull_AndDoesNotInvokeOnInsert()
    {
        var invoked = false;
        var cut = Render<PauseModal>(p => p.Add(x => x.OnInsert, EventCallback.Factory.Create<int>(this, _ => invoked = true)));

        var showTask = await ShowAsync(cut);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        var result = await showTask;
        result.Should().BeNull();
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task Escape_ClosesModal_ResolvesWithNull()
    {
        var cut = Render<PauseModal>();
        var showTask = await ShowAsync(cut);

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        var result = await showTask;
        result.Should().BeNull();
        cut.FindAll("[role='dialog']").Should().BeEmpty();
    }

    [Fact]
    public async Task BackdropClick_ClosesModal_ResolvesWithNull()
    {
        var cut = Render<PauseModal>();
        var showTask = await ShowAsync(cut);

        cut.Find(".backdrop-blur-sm").Click();

        var result = await showTask;
        result.Should().BeNull();
    }

    [Fact]
    public async Task CustomTitleInsertAndCancelText_AreRendered()
    {
        var cut = Render<PauseModal>(p => p
            .Add(x => x.Title, "Custom Title")
            .Add(x => x.InsertText, "Add")
            .Add(x => x.CancelText, "Nevermind"));

        await ShowAsync(cut);

        cut.Markup.Should().Contain("Custom Title");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Add");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Nevermind");
    }
}
