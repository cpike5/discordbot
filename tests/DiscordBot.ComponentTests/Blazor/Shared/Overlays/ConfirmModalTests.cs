using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class ConfirmModalTests : BlazorComponentTestContext
{
    // ShowAsync() itself never awaits (it's plain sync work that returns a Task settled later by
    // a button click), so invoking it through cut.InvokeAsync marshals the StateHasChanged() call
    // onto bUnit's renderer dispatcher without blocking - calling cut.Instance.ShowAsync()
    // directly throws "the current thread is not associated with the Dispatcher". The callback
    // MUST be a block-bodied (void-returning) lambda, not an expression lambda that evaluates to
    // the Task<bool> ShowAsync() returns: bUnit's InvokeAsync overload resolution treats an
    // expression lambda whose value is a Task as Func<Task> and awaits it as part of the dispatch
    // - which deadlocks here forever, since that Task doesn't complete until a confirm/cancel
    // click happens later in the test, on the very call this would be blocking.
    private static async Task<Task<bool>> StartShowAsync(IRenderedComponent<ConfirmModal> cut)
    {
        Task<bool> resultTask = null!;
        await cut.InvokeAsync(() => { resultTask = cut.Instance.ShowAsync(); });
        return resultTask;
    }

    [Fact]
    public async Task ShowAsync_OpensDialog_WithTitleAndMessage()
    {
        var cut = Render<ConfirmModal>(p => p.Add(x => x.Id, "cm1").Add(x => x.Title, "Delete?").Add(x => x.Message, "Sure?"));

        _ = await StartShowAsync(cut);

        var dialog = cut.Find("div[role='dialog']");
        dialog.GetAttribute("aria-modal").Should().Be("true");
        cut.Find("h3").TextContent.Should().Be("Delete?");
        cut.Markup.Should().Contain("Sure?");
    }

    [Fact]
    public async Task ShowAsync_ConfirmClicked_ResolvesTrue_AndInvokesOnConfirm()
    {
        var confirmed = false;
        var cut = Render<ConfirmModal>(p => p
            .Add(x => x.Id, "cm1")
            .Add(x => x.Title, "Delete?")
            .Add(x => x.Message, "Sure?")
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true)));

        // Hold the un-awaited task while clicking Confirm - awaiting it before the click would
        // deadlock bUnit's single-threaded renderer (ShowAsync's Task only completes once the
        // confirm/cancel button handler runs, which needs this same render thread).
        var resultTask = await StartShowAsync(cut);

        cut.Find("div[role='dialog']").Should().NotBeNull();
        var confirmButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Confirm");
        confirmButton.Click();

        var result = await resultTask;

        result.Should().BeTrue();
        confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ShowAsync_CancelClicked_ResolvesFalse_AndInvokesOnCancel()
    {
        var cancelled = false;
        var cut = Render<ConfirmModal>(p => p
            .Add(x => x.Id, "cm1")
            .Add(x => x.Title, "Delete?")
            .Add(x => x.Message, "Sure?")
            .Add(x => x.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        var resultTask = await StartShowAsync(cut);
        cut.Find("div[role='dialog']").Should().NotBeNull();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        var result = await resultTask;

        result.Should().BeFalse();
        cancelled.Should().BeTrue();
    }

    [Fact]
    public async Task ShowAsync_BackdropClick_ResolvesFalse()
    {
        var cut = Render<ConfirmModal>(p => p.Add(x => x.Id, "cm1").Add(x => x.Title, "x").Add(x => x.Message, "y"));

        var resultTask = await StartShowAsync(cut);
        cut.Find("div[aria-hidden='true']").Click();

        var result = await resultTask;

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequiredText_DisablesConfirm_UntilTypedInputMatches()
    {
        var cut = Render<ConfirmModal>(p => p
            .Add(x => x.Id, "cm1")
            .Add(x => x.Title, "Delete server?")
            .Add(x => x.Message, "Type to confirm.")
            .Add(x => x.RequiredText, "my-guild")
            .Add(x => x.InputLabel, "Guild name"));

        var resultTask = await StartShowAsync(cut);

        var confirmButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Confirm");
        confirmButton.HasAttribute("disabled").Should().BeTrue();

        var input = cut.Find("input[type='text']");
        await input.InputAsync(new ChangeEventArgs { Value = "wrong" });
        confirmButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Confirm");
        confirmButton.HasAttribute("disabled").Should().BeTrue();

        await input.InputAsync(new ChangeEventArgs { Value = "my-guild" });
        confirmButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Confirm");
        confirmButton.HasAttribute("disabled").Should().BeFalse();

        confirmButton.Click();
        var result = await resultTask;
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ConfirmationVariant.Info, "bg-accent-blue/20")]
    [InlineData(ConfirmationVariant.Warning, "bg-warning/20")]
    [InlineData(ConfirmationVariant.Danger, "bg-error/20")]
    public async Task Variant_AppliesExpectedIconBackground(ConfirmationVariant variant, string expectedClass)
    {
        var cut = Render<ConfirmModal>(p => p.Add(x => x.Id, "cm1").Add(x => x.Title, "x").Add(x => x.Message, "y").Add(x => x.Variant, variant));
        _ = await StartShowAsync(cut);

        cut.Find("div.rounded-full").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public async Task Dispose_WhileShowAsyncPending_ResolvesFalse_InsteadOfHangingForever()
    {
        var cut = Render<ConfirmModal>(p => p.Add(x => x.Id, "cm1").Add(x => x.Title, "x").Add(x => x.Message, "y"));

        var resultTask = await StartShowAsync(cut);

        await DisposeComponentsAsync();

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Class_And_AdditionalAttributes_ArePassedThrough_ToUnderlyingModal()
    {
        var cut = Render<ConfirmModal>(p => p
            .Add(x => x.Id, "cm1")
            .Add(x => x.Title, "x")
            .Add(x => x.Message, "y")
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-confirm"));
        _ = await StartShowAsync(cut);

        cut.Find("div[role='document']").ClassList.Should().Contain("extra-class");
        cut.Find("div[role='dialog']").GetAttribute("data-testid").Should().Be("my-confirm");
    }

    [Fact]
    public async Task MessageContent_OverridesMessage()
    {
        var cut = Render<ConfirmModal>(p => p
            .Add(x => x.Id, "cm1")
            .Add(x => x.Title, "x")
            .Add(x => x.Message, "ignored")
            .Add(x => x.MessageContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='custom-msg'>Custom</span>"))));
        _ = await StartShowAsync(cut);

        cut.Find("[data-testid='custom-msg']").TextContent.Should().Be("Custom");
    }
}
