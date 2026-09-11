using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class ModalTests : BlazorComponentTestContext
{
    [Fact]
    public void IsOpen_False_RendersNothing()
    {
        var cut = Render<Modal>(p => p.Add(x => x.Id, "m1").Add(x => x.IsOpen, false));
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void IsOpen_True_RendersDialog_WithAriaAttributes()
    {
        var cut = Render<Modal>(p => p
            .Add(x => x.Id, "m1")
            .Add(x => x.Title, "Example")
            .Add(x => x.IsOpen, true)
            .AddChildContent("<p>Body</p>"));

        var dialog = cut.Find("div[role='dialog']");
        dialog.GetAttribute("aria-modal").Should().Be("true");
        dialog.GetAttribute("aria-labelledby").Should().Be("m1Title");
        cut.Find("h3#m1Title").TextContent.Should().Be("Example");
        cut.Markup.Should().Contain("Body");
    }

    [Fact]
    public void FooterContent_RendersWhenProvided()
    {
        var cut = Render<Modal>(p => p
            .Add(x => x.Id, "m1")
            .Add(x => x.IsOpen, true)
            .Add(x => x.FooterContent, (RenderFragment)(b => b.AddMarkupContent(0, "<button data-testid='footer-btn'>Close</button>"))));

        cut.Find("[data-testid='footer-btn']").Should().NotBeNull();
    }

    [Fact]
    public void BackdropClick_Closes_AndRaisesCallbacks()
    {
        var closed = false;
        bool? openChangedTo = null;
        var cut = Render<Modal>(p => p
            .Add(x => x.Id, "m1")
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsOpenChanged, EventCallback.Factory.Create<bool>(this, v => openChangedTo = v))
            .Add(x => x.OnClosed, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find("div[aria-hidden='true']").Click();

        openChangedTo.Should().BeFalse();
        closed.Should().BeTrue();
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Escape_Closes()
    {
        var closed = false;
        var cut = Render<Modal>(p => p
            .Add(x => x.Id, "m1")
            .Add(x => x.IsOpen, true)
            .Add(x => x.OnClosed, EventCallback.Factory.Create(this, () => closed = true)));

        await cut.Find("div.fixed.inset-0.flex").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        closed.Should().BeTrue();
    }

    [Theory]
    [InlineData(ModalSize.Small, "max-w-sm")]
    [InlineData(ModalSize.Medium, "max-w-md")]
    [InlineData(ModalSize.Large, "max-w-lg")]
    [InlineData(ModalSize.XLarge, "max-w-2xl")]
    public void Size_AppliesExpectedClass(ModalSize size, string expectedClass)
    {
        var cut = Render<Modal>(p => p.Add(x => x.Id, "m1").Add(x => x.IsOpen, true).Add(x => x.Size, size));
        cut.Find("div[role='document']").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Dialog_HasTabIndexNegativeOne_SoEscapeWorksWithNoFocusableChild()
    {
        var cut = Render<Modal>(p => p.Add(x => x.Id, "m1").Add(x => x.IsOpen, true));
        cut.Find("div[role='document']").GetAttribute("tabindex").Should().Be("-1");
    }

    [Fact]
    public async Task Dispose_SwallowsJSDisconnectedException_FromReleaseFocus()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/browser.js");
        moduleInterop.Setup<int>("trapFocus", _ => true).SetResult(1);
        moduleInterop.SetupVoid("releaseFocus", _ => true).SetException(new Microsoft.JSInterop.JSDisconnectedException("circuit gone"));

        Render<Modal>(p => p.Add(x => x.Id, "m1").Add(x => x.IsOpen, true));

        var act = async () => await DisposeComponentsAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Modal>(p => p
            .Add(x => x.Id, "m1")
            .Add(x => x.IsOpen, true)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-modal"));

        cut.Find("div[role='document']").ClassList.Should().Contain("extra-class");
        cut.Find("div[role='dialog']").GetAttribute("data-testid").Should().Be("my-modal");
    }
}
