using DiscordBot.Bot.Blazor.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;

namespace DiscordBot.Tests.Blazor.Interop;

public class ChartInteropTests
{
    private const string ModulePath = "./js/blazor/charts.js";

    private static (Mock<IJSRuntime> JsRuntime, Mock<IJSObjectReference> Module, ChartInterop Sut) CreateSut()
    {
        var moduleMock = new Mock<IJSObjectReference>();
        var jsRuntimeMock = new Mock<IJSRuntime>();

        jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)))
            .ReturnsAsync(moduleMock.Object);

        var sut = new ChartInterop(jsRuntimeMock.Object);
        return (jsRuntimeMock, moduleMock, sut);
    }

    [Fact]
    public async Task CreateAsync_ImportsTheChartsModule()
    {
        var (jsRuntime, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<int>("create", It.IsAny<object?[]>()))
            .ReturnsAsync(7);

        await sut.CreateAsync(default, new { type = "line" });

        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvokesCreateWithTheCanvasAndConfig_AndReturnsTheHandle()
    {
        var (_, module, sut) = CreateSut();
        var config = new { type = "bar" };
        module
            .Setup(m => m.InvokeAsync<int>(
                "create",
                It.Is<object?[]>(args => args.Length == 2 && ReferenceEquals(args[1], config))))
            .ReturnsAsync(42);

        var handle = await sut.CreateAsync(default, config);

        Assert.Equal(42, handle);
    }

    [Fact]
    public async Task CreateAsync_OnlyImportsTheModuleOnce_AcrossMultipleCalls()
    {
        var (jsRuntime, module, sut) = CreateSut();
        module.Setup(m => m.InvokeAsync<int>("create", It.IsAny<object?[]>())).ReturnsAsync(1);

        await sut.CreateAsync(default, new { });
        await sut.CreateAsync(default, new { });

        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_InvokesUpdateWithTheHandleDataAndOptions()
    {
        var (_, module, sut) = CreateSut();
        var data = new { labels = new[] { "a" } };
        var options = new { plugins = new { } };

        await sut.UpdateAsync(7, data, options);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "update",
                It.Is<object?[]>(args =>
                    args.Length == 3 &&
                    (int)args[0]! == 7 &&
                    ReferenceEquals(args[1], data) &&
                    ReferenceEquals(args[2], options))),
            Times.Once);
    }

    [Fact]
    public async Task DestroyAsync_InvokesDestroyWithTheHandle()
    {
        var (_, module, sut) = CreateSut();

        await sut.DestroyAsync(3);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "destroy",
                It.Is<object?[]>(args => args.Length == 1 && (int)args[0]! == 3)),
            Times.Once);
    }

    [Fact]
    public async Task DestroyAllAsync_InvokesDestroyAll()
    {
        var (_, module, sut) = CreateSut();

        await sut.DestroyAllAsync();

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>("destroyAll", It.Is<object?[]>(args => args.Length == 0)),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithNoModuleImported_DoesNothing()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var sut = new ChartInterop(jsRuntimeMock.Object);

        await sut.DisposeAsync();

        jsRuntimeMock.Verify(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheImportedModule()
    {
        var (_, module, sut) = CreateSut();
        await sut.CreateAsync(default, new { });
        module.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        await sut.DisposeAsync();

        module.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_SwallowsJSDisconnectedException()
    {
        var (_, module, sut) = CreateSut();
        await sut.CreateAsync(default, new { });
        module
            .Setup(m => m.DisposeAsync())
            .Returns(() => ValueTask.FromException(new JSDisconnectedException("Circuit disconnected.")));

        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }
}
