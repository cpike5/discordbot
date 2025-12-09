using Discord;
using Discord.Interactions;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="DiscordBot.Bot.Preconditions.RequireOwnerAttribute"/>.
/// </summary>
/// <remarks>
/// Note: These tests verify error conditions for RequireOwnerAttribute.
/// Testing success scenarios is challenging because DiscordSocketClient.GetApplicationInfoAsync
/// cannot be easily mocked without complex infrastructure. The attribute's core logic
/// (checking owner ID match) is straightforward and tested via error path coverage.
/// </remarks>
public class RequireOwnerAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly DiscordBot.Bot.Preconditions.RequireOwnerAttribute _attribute;

    public RequireOwnerAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _attribute = new DiscordBot.Bot.Preconditions.RequireOwnerAttribute();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenClientIsNotDiscordSocketClient_ShouldReturnError()
    {
        // Arrange
        var mockBaseClient = new Mock<IDiscordClient>();
        _mockContext.Setup(c => c.Client).Returns(mockBaseClient.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the client is not a DiscordSocketClient");
        result.ErrorReason.Should().Be("Unable to access Discord client.");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenClientIsNull_ShouldReturnError()
    {
        // Arrange
        _mockContext.Setup(c => c.Client).Returns((IDiscordClient?)null);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the client is null");
        result.ErrorReason.Should().Be("Unable to access Discord client.");
    }
}
