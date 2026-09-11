using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class PreviewPopoverTests : BlazorComponentTestContext
{
    private static readonly UserPreviewViewModel FakeUser = new()
    {
        UserId = 1,
        Username = "exampleuser",
        ProfileUrl = "/profile"
    };

    private static RenderFragment<UserPreviewViewModel> UserTemplate => model => builder =>
    {
        builder.OpenComponent<UserPreviewPopoverContent>(0);
        builder.AddComponentParameter(1, nameof(UserPreviewPopoverContent.Model), model);
        builder.CloseComponent();
    };

    private IRenderedComponent<PreviewPopover<UserPreviewViewModel>> RenderUserPopover(Func<Task<UserPreviewViewModel?>> loader)
        => Render<PreviewPopover<UserPreviewViewModel>>(p => p
            .Add(x => x.Kind, PreviewKind.User)
            .Add(x => x.Id, "1")
            .Add(x => x.Loader, loader)
            .Add(x => x.ContentTemplate, UserTemplate)
            .Add(x => x.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='trigger'>@exampleuser</span>"))));

    [Fact]
    public void ClosedByDefault_RendersOnlyTrigger()
    {
        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(FakeUser));

        cut.Find("[data-testid='trigger']").Should().NotBeNull();
        cut.FindAll("div[role='dialog']").Should().BeEmpty();
    }

    [Fact]
    public void Click_Opens_ThenLoadsAndRendersContent()
    {
        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(FakeUser));

        cut.Find("[tabindex='0']").Click();

        cut.Find("div[role='dialog']").GetAttribute("aria-label").Should().Be("User preview");
        cut.Find(".preview-username").TextContent.Should().Be("exampleuser");
    }

    [Fact]
    public void FocusIn_ShowsLoadingSkeleton_ThenContent_WhenLoaderCompletes()
    {
        var tcs = new TaskCompletionSource<UserPreviewViewModel?>();
        var cut = RenderUserPopover(() => tcs.Task);

        cut.Find("[tabindex='0']").FocusIn();

        cut.WaitForAssertion(() => cut.Find(".preview-popup-loading").Should().NotBeNull());

        tcs.SetResult(FakeUser);

        cut.WaitForAssertion(() => cut.Find(".preview-username").TextContent.Should().Be("exampleuser"));
    }

    [Fact]
    public void LoaderThrows_RendersErrorState()
    {
        var tcs = new TaskCompletionSource<UserPreviewViewModel?>();
        var cut = RenderUserPopover(() => tcs.Task);

        cut.Find("[tabindex='0']").FocusIn();
        tcs.SetException(new InvalidOperationException("boom"));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".preview-popup-error").Should().NotBeNull();
            cut.Markup.Should().Contain("User Not Found");
        });
    }

    [Fact]
    public void LoaderReturnsNull_RendersErrorState()
    {
        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(null));

        cut.Find("[tabindex='0']").Click();

        cut.Find(".preview-popup-error").Should().NotBeNull();
    }

    [Fact]
    public void Escape_ClosesPopover()
    {
        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(FakeUser));
        cut.Find("[tabindex='0']").Click();
        cut.Find("div[role='dialog']").Should().NotBeNull();

        cut.Find("[tabindex='0']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("div[role='dialog']").Should().BeEmpty();
    }

    [Fact]
    public void GuildKind_UsesWiderWidthClass_AndGuildAriaLabel()
    {
        var cut = Render<PreviewPopover<GuildPreviewViewModel>>(p => p
            .Add(x => x.Kind, PreviewKind.Guild)
            .Add(x => x.Id, "9")
            .Add(x => x.Loader, () => Task.FromResult<GuildPreviewViewModel?>(new GuildPreviewViewModel { GuildId = 9, Name = "G", DetailsUrl = "#", SettingsUrl = "#" }))
            .Add(x => x.ContentTemplate, (RenderFragment<GuildPreviewViewModel>)(model => builder =>
            {
                builder.OpenComponent<GuildPreviewPopoverContent>(0);
                builder.AddComponentParameter(1, nameof(GuildPreviewPopoverContent.Model), model);
                builder.CloseComponent();
            }))
            .Add(x => x.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Guild</span>"))));

        cut.Find("[tabindex='0']").Click();

        var dialog = cut.Find("div[role='dialog']");
        dialog.GetAttribute("aria-label").Should().Be("Guild preview");
        dialog.ClassList.Should().Contain("w-80");
    }

    [Fact]
    public async Task Dispose_SwallowsJSDisconnectedException_FromOffClickOutside()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/browser.js");
        moduleInterop.Setup<int>("onClickOutside", _ => true).SetResult(1);
        moduleInterop.SetupVoid("offClickOutside", _ => true).SetException(new Microsoft.JSInterop.JSDisconnectedException("circuit gone"));

        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(FakeUser));
        cut.Find("[tabindex='0']").Click(); // opens, registering the click-outside handler

        var act = async () => await DisposeComponentsAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DataAttributes_ReflectKindAndId()
    {
        var cut = RenderUserPopover(() => Task.FromResult<UserPreviewViewModel?>(FakeUser));

        var trigger = cut.Find("[tabindex='0']");
        trigger.GetAttribute("data-preview-type").Should().Be("user");
        trigger.GetAttribute("data-preview-id").Should().Be("1");
    }
}
