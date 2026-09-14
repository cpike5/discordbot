using DiscordBot.Bot.Blazor.Interop;
using Microsoft.JSInterop;
using Moq;

namespace DiscordBot.Tests.Blazor.Interop;

/// <summary>
/// Mirrors <c>BrowserInteropTests</c>'s Moq-based pattern (a mocked <see cref="IJSRuntime"/>
/// import returning a mocked <see cref="IJSObjectReference"/>) rather than bUnit's
/// <c>JSInterop.SetupModule</c> - none of the other three interop wrappers
/// (<c>BrowserInterop</c>/<c>ChartInterop</c>/<c>AudioInterop</c>) use bUnit for this, since a
/// thin JS-import wrapper has no component tree to render; keeping this one in
/// <c>tests/DiscordBot.Tests/Blazor/Interop/</c> alongside them, rather than
/// <c>tests/DiscordBot.ComponentTests</c>, follows that existing convention (CLAUDE.md: "copy the
/// pattern, do not invent a new one").
/// </summary>
public class ThemeInteropTests
{
    private const string ModulePath = "./js/blazor/theme.js";

    private static (Mock<IJSRuntime> JsRuntime, Mock<IJSObjectReference> Module, ThemeInterop Sut) CreateSut()
    {
        var moduleMock = new Mock<IJSObjectReference>();
        var jsRuntimeMock = new Mock<IJSRuntime>();

        jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)))
            .ReturnsAsync(moduleMock.Object);

        var sut = new ThemeInterop(jsRuntimeMock.Object);
        return (jsRuntimeMock, moduleMock, sut);
    }

    [Fact]
    public async Task ApplyAsync_ImportsTheThemeModule_AndInvokesApplyWithTheThemeKey()
    {
        var (jsRuntime, module, sut) = CreateSut();

        await sut.ApplyAsync("discord-dark");

        // InvokeVoidAsync is an extension method (over the interface's own generic InvokeAsync<TValue>)
        // and Moq cannot set up/verify extension methods directly - verify the underlying call it
        // delegates to instead, same as ChartInteropTests' DestroyAsync coverage does.
        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "apply",
                It.Is<object?[]>(a => a.Length == 1 && (string)a[0]! == "discord-dark")),
            Times.Once);
        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)),
            Times.Once);
    }

    [Fact]
    public async Task ClearAsync_InvokesClear_WithNoArguments()
    {
        var (_, module, sut) = CreateSut();

        await sut.ClearAsync();

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>("clear", It.Is<object?[]>(a => a.Length == 0)),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsTheModulesValue()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<string?>("getCurrent", It.Is<object?[]>(a => a.Length == 0)))
            .ReturnsAsync("purple-dusk");

        var current = await sut.GetCurrentAsync();

        Assert.Equal("purple-dusk", current);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenModuleReturnsNull_ReturnsNull()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<string?>("getCurrent", It.Is<object?[]>(a => a.Length == 0)))
            .ReturnsAsync((string?)null);

        var current = await sut.GetCurrentAsync();

        Assert.Null(current);
    }

    [Fact]
    public async Task ModuleImport_IsCachedAcrossCalls()
    {
        var (jsRuntime, _, sut) = CreateSut();

        await sut.ApplyAsync("a");
        await sut.ClearAsync();
        await sut.GetCurrentAsync();

        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleWasNeverImported_DoesNothing()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var sut = new ThemeInterop(jsRuntimeMock.Object);

        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        Assert.Null(exception);
        jsRuntimeMock.Verify(
            js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()),
            Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheImportedModule()
    {
        var (_, module, sut) = CreateSut();
        await sut.GetCurrentAsync();

        await sut.DisposeAsync();

        module.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_SwallowsJSDisconnectedException()
    {
        var (_, module, sut) = CreateSut();
        await sut.GetCurrentAsync();
        module
            .Setup(m => m.DisposeAsync())
            .Returns(() => ValueTask.FromException(new JSDisconnectedException("circuit gone")));

        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ModuleImport_WhenItFails_IsNotCachedForever_AndRetriesOnNextCall()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var callCount = 0;
        jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1
                    ? ValueTask.FromException<IJSObjectReference>(new JSException("boom"))
                    : ValueTask.FromResult(new Mock<IJSObjectReference>().Object);
            });

        var sut = new ThemeInterop(jsRuntimeMock.Object);
        await Assert.ThrowsAsync<JSException>(() => sut.GetCurrentAsync());

        // The failed import must not be cached forever - a second call retries the import rather
        // than forever awaiting (or rethrowing from) the same faulted task.
        var secondCallException = await Record.ExceptionAsync(() => sut.GetCurrentAsync());
        Assert.Null(secondCallException);
        Assert.Equal(2, callCount);
    }
}
