using DiscordBot.Bot.Blazor.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;

namespace DiscordBot.Tests.Blazor.Interop;

public class AudioInteropTests
{
    private const string ModulePath = "./js/blazor/audio.js";

    private sealed class FakeCallbackTarget
    {
        [JSInvokable]
        public void OnPreviewEnded()
        {
        }
    }

    private static (Mock<IJSRuntime> JsRuntime, Mock<IJSObjectReference> Module, AudioInterop Sut) CreateSut()
    {
        var moduleMock = new Mock<IJSObjectReference>();
        var jsRuntimeMock = new Mock<IJSRuntime>();

        jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)))
            .ReturnsAsync(moduleMock.Object);

        var sut = new AudioInterop(jsRuntimeMock.Object);
        return (jsRuntimeMock, moduleMock, sut);
    }

    [Fact]
    public async Task PlayPreviewAsync_ImportsTheAudioModule_AndInvokesPlayPreviewWithTheUrlAndRef()
    {
        var (jsRuntime, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());

        await sut.PlayPreviewAsync("https://example.test/sound.mp3", dotNetRef);

        jsRuntime.Verify(
            js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == ModulePath)),
            Times.Once);
        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "playPreview",
                It.Is<object?[]>(args =>
                    args.Length == 2 &&
                    (string)args[0]! == "https://example.test/sound.mp3" &&
                    ReferenceEquals(args[1], dotNetRef))),
            Times.Once);
    }

    [Fact]
    public async Task StopPreviewAsync_InvokesStopPreview()
    {
        var (_, module, sut) = CreateSut();

        await sut.StopPreviewAsync();

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>("stopPreview", It.Is<object?[]>(args => args.Length == 0)),
            Times.Once);
    }

    [Fact]
    public async Task GetDurationAsync_InvokesGetDurationWithTheInputAndIndex_AndReturnsTheSeconds()
    {
        var (_, module, sut) = CreateSut();
        module
            .Setup(m => m.InvokeAsync<double>(
                "getDuration",
                It.Is<object?[]>(args => args.Length == 2 && (int)args[1]! == 0)))
            .ReturnsAsync(12.5);

        var seconds = await sut.GetDurationAsync(default, 0);

        Assert.Equal(12.5, seconds);
    }

    [Fact]
    public async Task RegisterDropZoneAsync_InvokesRegisterDropZone_AndReturnsTheHandle()
    {
        var (_, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());
        module
            .Setup(m => m.InvokeAsync<int>(
                "registerDropZone",
                It.Is<object?[]>(args => args.Length == 2 && ReferenceEquals(args[1], dotNetRef))))
            .ReturnsAsync(5);

        var handle = await sut.RegisterDropZoneAsync(default, dotNetRef);

        Assert.Equal(5, handle);
    }

    [Fact]
    public async Task UnregisterDropZoneAsync_InvokesUnregisterDropZoneWithTheHandle()
    {
        var (_, module, sut) = CreateSut();

        await sut.UnregisterDropZoneAsync(5);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "unregisterDropZone",
                It.Is<object?[]>(args => args.Length == 1 && (int)args[0]! == 5)),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WithAnElementReference_InvokesUploadWithTheInputUrlTokenAndRef()
    {
        var (_, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());

        await sut.UploadAsync(default(ElementReference), "/api/guilds/1/sounds", "the-token", dotNetRef);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "upload",
                It.Is<object?[]>(args =>
                    args.Length == 4 &&
                    (string)args[1]! == "/api/guilds/1/sounds" &&
                    (string)args[2]! == "the-token" &&
                    ReferenceEquals(args[3], dotNetRef))),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WithADropToken_InvokesUploadWithTheToken()
    {
        var (_, module, sut) = CreateSut();
        using var dotNetRef = DotNetObjectReference.Create(new FakeCallbackTarget());

        await sut.UploadAsync("drop-1", "/api/guilds/1/sounds", "the-token", dotNetRef);

        module.Verify(
            m => m.InvokeAsync<It.IsAnyType>(
                "upload",
                It.Is<object?[]>(args =>
                    args.Length == 4 &&
                    (string)args[0]! == "drop-1" &&
                    (string)args[1]! == "/api/guilds/1/sounds")),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithNoModuleImported_DoesNothing()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var sut = new AudioInterop(jsRuntimeMock.Object);

        await sut.DisposeAsync();

        jsRuntimeMock.Verify(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_SwallowsJSDisconnectedException()
    {
        var (_, module, sut) = CreateSut();
        await sut.StopPreviewAsync(); // forces the module import
        module
            .Setup(m => m.DisposeAsync())
            .Returns(() => ValueTask.FromException(new JSDisconnectedException("Circuit disconnected.")));

        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }
}
