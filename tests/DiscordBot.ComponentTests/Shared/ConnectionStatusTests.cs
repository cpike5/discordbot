using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class ConnectionStatusTests : TestContext
{
    [Theory]
    [InlineData(ConnectionState.Connected, "connected", "Connected")]
    [InlineData(ConnectionState.Connecting, "connecting", "Connecting...")]
    [InlineData(ConnectionState.Reconnecting, "reconnecting", "Reconnecting...")]
    [InlineData(ConnectionState.Disconnected, "disconnected", "Disconnected")]
    public void RendersDataStateAndLabel_ForEachState(ConnectionState state, string expectedDataState, string expectedText)
    {
        var cut = RenderComponent<ConnectionStatus>(p => p.Add(x => x.State, state));

        var root = cut.Find("#connection-status");
        root.GetAttribute("data-state").Should().Be(expectedDataState);
        cut.Find(".connection-text").TextContent.Trim().Should().Be(expectedText);
    }

    [Fact]
    public void CustomText_OverridesDefaultLabel()
    {
        var cut = RenderComponent<ConnectionStatus>(p => p
            .Add(x => x.State, ConnectionState.Connected)
            .Add(x => x.CustomText, "Live"));

        cut.Find(".connection-text").TextContent.Trim().Should().Be("Live");
    }

    [Fact]
    public void HasStatusRole_AndPoliteLiveRegion()
    {
        var cut = RenderComponent<ConnectionStatus>();

        var root = cut.Find("#connection-status");
        root.GetAttribute("role").Should().Be("status");
        root.GetAttribute("aria-live").Should().Be("polite");
    }
}
