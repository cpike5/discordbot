using DiscordBot.Bot.Handlers;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="NotXMessageHandler"/>'s configuration kill switch.
/// Discord's <c>SocketUserMessage</c> has no public constructor, so message-shaped
/// scenarios cannot be exercised here (see docs/articles/not-x/test-constraints.md);
/// what is testable is that the switch short-circuits before any message inspection
/// or service resolution happens at all.
/// </summary>
public class NotXMessageHandlerTests
{
    private static NotXMessageHandler CreateHandler(
        bool enabled,
        Mock<IServiceScopeFactory> scopeFactory)
    {
        return new NotXMessageHandler(
            scopeFactory.Object,
            Options.Create(new NotXOptions { Enabled = enabled }),
            Mock.Of<ILogger<NotXMessageHandler>>());
    }

    [Fact]
    public async Task HandleMessageReceivedAsync_WhenDisabledInConfiguration_ShouldNotResolveNotXService()
    {
        // The switch is the handler's first statement, so it returns before the message is
        // read — passing no message is what proves nothing downstream is reached.
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var handler = CreateHandler(enabled: false, scopeFactory);

        var act = async () => await handler.HandleMessageReceivedAsync(null!);

        await act.Should().NotThrowAsync();
        scopeFactory.Verify(
            f => f.CreateScope(),
            Times.Never,
            "a disabled feature must not open a scope or resolve INotXService");
    }

    [Fact]
    public void Constructor_ShouldDefaultToEnabled_WhenNoConfigurationSectionIsPresent()
    {
        // NotXOptions defaults to Enabled = true, so an appsettings file without a NotX
        // section leaves the feature on — the switch is opt-out, not opt-in.
        new NotXOptions().Enabled.Should().BeTrue();
    }
}
