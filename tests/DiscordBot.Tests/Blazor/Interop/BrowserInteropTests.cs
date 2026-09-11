using DiscordBot.Bot.Blazor.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;

namespace DiscordBot.Tests.Blazor.Interop;

public class BrowserInteropTests
{
    private const string ModulePath = "./js/blazor/browser.js";

    private sealed class FakeCallbackTarget
    {
        [JSInvokable]
        public void OnMediaChanged(bool matches)
        {
        }

        [JSInvokable]
        public void OnClickOutside()
        {
        }
    }

    private static (Mock<IJSRuntime> JsRuntime, Mock<IJSObjectReference> Module, BrowserInterop Sut) CreateSut()
    {
        var moduleMock = new Mock<IJSObjectReference>();
        var jsRuntimeMock = new Mock<IJSRuntime>();

        jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)))
            .ReturnsAsync(moduleMock.Object);

        var sut = new BrowserInterop(jsRuntimeMock.Object);
        return (jsRuntimeMock, moduleMock, sut);
    }

    [Fact]
    public async Task StorageGetAsync_ImportsTheBrowserModule_AndInvokesStorageGetWithTheKey()
    {
        var (jsRuntime, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<string?>("storageGet", It.Is<object?[]>(a => (string)a[0]! == "theme")))
            .ReturnsAsync("dark");

        var value = await sut.StorageGetAsync("theme");

        Assert.Equal("dark", value);
        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)),
            Times.Once);
    }

    [Fact]
    public async Task StorageSetAsync_InvokesStorageSetWithTheKeyAndValue()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<bool>(
                "storageSet",
                It.Is<object?[]>(a => (string)a[0]! == "theme" && (string)a[1]! == "dark")))
            .ReturnsAsync(true);

        var ok = await sut.StorageSetAsync("theme", "dark");

        Assert.True(ok);
    }

    [Fact]
    public async Task StorageRemoveAsync_InvokesStorageRemoveWithTheKey()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<bool>("storageRemove", It.Is<object?[]>(a => (string)a[0]! == "theme")))
            .ReturnsAsync(true);

        var ok = await sut.StorageRemoveAsync("theme");

        Assert.True(ok);
    }

    [Fact]
    public async Task CopyToClipboardAsync_InvokesCopyToClipboardWithTheText()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<bool>("copyToClipboard", It.Is<object?[]>(a => (string)a[0]! == "hello")))
            .ReturnsAsync(true);

        var ok = await sut.CopyToClipboardAsync("hello");

        Assert.True(ok);
    }

    [Fact]
    public async Task FocusElementAsync_InvokesFocusElement()
    {
        var (_, module, sut) = CreateSut();

        await sut.FocusElementAsync(default);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>("focusElement", It.Is<object?[]>(a => a.Length == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ScrollIntoViewAsync_InvokesScrollIntoViewWithTheBehavior()
    {
        var (_, module, sut) = CreateSut();

        await sut.ScrollIntoViewAsync(default, "auto");

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "scrollIntoView",
                It.Is<object?[]>(a => a.Length == 2 && (string)a[1]! == "auto")),
            Times.Once);
    }

    [Fact]
    public async Task TrapFocusAsync_InvokesTrapFocus_AndReturnsTheHandle()
    {
        var (_, module, sut) = CreateSut();
        module.Setup(m => m.InvokeAsync<int>("trapFocus", It.IsAny<object?[]>())).ReturnsAsync(3);

        var handle = await sut.TrapFocusAsync(default);

        Assert.Equal(3, handle);
    }

    [Fact]
    public async Task ReleaseFocusAsync_InvokesReleaseFocusWithTheHandle()
    {
        var (_, module, sut) = CreateSut();

        await sut.ReleaseFocusAsync(3);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "releaseFocus",
                It.Is<object?[]>(a => a.Length == 1 && (int)a[0]! == 3)),
            Times.Once);
    }

    [Fact]
    public async Task SetBeforeUnloadGuardAsync_InvokesSetBeforeUnloadGuardWithTheFlag()
    {
        var (_, module, sut) = CreateSut();

        await sut.SetBeforeUnloadGuardAsync(true);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "setBeforeUnloadGuard",
                It.Is<object?[]>(a => a.Length == 1 && (bool)a[0]! == true)),
            Times.Once);
    }

    [Fact]
    public async Task MatchMediaAsync_InvokesMatchMediaWithTheQueryAndRef_AndReturnsTheResult()
    {
        var (_, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());
        module
            .Setup(m => m.InvokeAsync<MediaWatchResult>(
                "matchMedia",
                It.Is<object?[]>(a => (string)a[0]! == "(max-width: 1023px)" && ReferenceEquals(a[1], dotNetRef))))
            .ReturnsAsync(new MediaWatchResult(9, true));

        var result = await sut.MatchMediaAsync("(max-width: 1023px)", dotNetRef);

        Assert.Equal(9, result.Handle);
        Assert.True(result.Matches);
    }

    [Fact]
    public async Task UnwatchMediaAsync_InvokesUnwatchMediaWithTheHandle()
    {
        var (_, module, sut) = CreateSut();

        await sut.UnwatchMediaAsync(9);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "unwatchMedia",
                It.Is<object?[]>(a => a.Length == 1 && (int)a[0]! == 9)),
            Times.Once);
    }

    [Fact]
    public async Task GetTimeZoneAsync_InvokesGetTimeZone_AndReturnsTheZone()
    {
        var (_, module, sut) = CreateSut();
        module.Setup(m => m.InvokeAsync<string>("getTimeZone", It.IsAny<object?[]>())).ReturnsAsync("America/New_York");

        var timeZone = await sut.GetTimeZoneAsync();

        Assert.Equal("America/New_York", timeZone);
    }

    [Fact]
    public async Task OnClickOutsideAsync_InvokesOnClickOutsideWithTheElementAndRef_AndReturnsTheHandle()
    {
        var (_, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());
        module
            .Setup(m => m.InvokeAsync<int>(
                "onClickOutside",
                It.Is<object?[]>(a => a.Length == 2 && ReferenceEquals(a[1], dotNetRef))))
            .ReturnsAsync(4);

        var handle = await sut.OnClickOutsideAsync(default, dotNetRef);

        Assert.Equal(4, handle);
    }

    [Fact]
    public async Task OffClickOutsideAsync_InvokesOffClickOutsideWithTheHandle()
    {
        var (_, module, sut) = CreateSut();

        await sut.OffClickOutsideAsync(4);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "offClickOutside",
                It.Is<object?[]>(a => a.Length == 1 && (int)a[0]! == 4)),
            Times.Once);
    }

    [Fact]
    public async Task GetSelectionAsync_InvokesGetSelection_AndReturnsTheResult()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<TextSelectionResult>("getSelection", It.IsAny<object?[]>()))
            .ReturnsAsync(new TextSelectionResult(2, 5, "abc"));

        var selection = await sut.GetSelectionAsync(default);

        Assert.Equal(2, selection.Start);
        Assert.Equal(5, selection.End);
        Assert.Equal("abc", selection.Value);
    }

    [Fact]
    public async Task SetSelectionAsync_InvokesSetSelectionWithStartAndEnd()
    {
        var (_, module, sut) = CreateSut();

        await sut.SetSelectionAsync(default, 1, 4);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "setSelection",
                It.Is<object?[]>(a => a.Length == 3 && (int)a[1]! == 1 && (int)a[2]! == 4)),
            Times.Once);
    }

    [Fact]
    public async Task InsertAtSelectionAsync_InvokesInsertAtSelectionWithTheText()
    {
        var (_, module, sut) = CreateSut();

        await sut.InsertAtSelectionAsync(default, "*emphasis*");

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "insertAtSelection",
                It.Is<object?[]>(a => a.Length == 2 && (string)a[1]! == "*emphasis*")),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithNoModuleImported_DoesNothing()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var sut = new BrowserInterop(jsRuntimeMock.Object);

        await sut.DisposeAsync();

        jsRuntimeMock.Verify(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_SwallowsJSDisconnectedException()
    {
        var (_, module, sut) = CreateSut();
        await sut.GetTimeZoneAsync(); // forces the module import; return value defaults to null which is fine here
        module
            .Setup(m => m.DisposeAsync())
            .Returns(() => ValueTask.FromException(new JSDisconnectedException("Circuit disconnected.")));

        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }
}
