using Bunit;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class ToastHostTests : BlazorComponentTestContext
{
    [Fact]
    public void NoToasts_RendersEmptyContainer()
    {
        var cut = Render<ToastHost>();
        cut.Find("#toastContainer").Should().NotBeNull();
        cut.FindAll(".toast").Should().BeEmpty();
    }

    [Fact]
    public void Show_RendersToast_WithLevelClass_AndMessage()
    {
        var toastService = Services.GetRequiredService<IToastService>();
        var cut = Render<ToastHost>();

        toastService.Success("Saved successfully.");

        cut.WaitForAssertion(() =>
        {
            var toast = cut.Find(".toast");
            toast.ClassList.Should().Contain("toast-success");
            toast.TextContent.Should().Contain("Saved successfully.");
        });
    }

    [Fact]
    public void Show_WithTitle_RendersTitleAndMessage()
    {
        var toastService = Services.GetRequiredService<IToastService>();
        var cut = Render<ToastHost>();

        toastService.Error("Something went wrong.", "Error");

        cut.WaitForAssertion(() => cut.Find(".toast").TextContent.Should().Contain("Error").And.Contain("Something went wrong."));
    }

    [Fact]
    public void DismissButton_RemovesToast_ViaToastService()
    {
        var toastService = Services.GetRequiredService<IToastService>();
        var cut = Render<ToastHost>();
        toastService.Info("Hello");
        cut.WaitForAssertion(() => cut.FindAll(".toast").Should().ContainSingle());

        cut.Find(".toast-close").Click();

        cut.FindAll(".toast").Should().BeEmpty();
        toastService.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void MultipleToasts_EachRenderWithOwnLevel()
    {
        var toastService = Services.GetRequiredService<IToastService>();
        var cut = Render<ToastHost>();

        toastService.Success("s");
        toastService.Warning("w");

        cut.WaitForAssertion(() =>
        {
            var toasts = cut.FindAll(".toast");
            toasts.Should().HaveCount(2);
            toasts[0].ClassList.Should().Contain("toast-success");
            toasts[1].ClassList.Should().Contain("toast-warning");
        });
    }
}
