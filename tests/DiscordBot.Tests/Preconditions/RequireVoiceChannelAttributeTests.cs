using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireVoiceChannelAttribute"/>.
///
/// Note: Due to Discord.NET's use of sealed types in SocketVoiceChannel and SocketGuildUser,
/// we can only test the paths that don't require mocking their non-virtual members.
/// This test suite focuses on:
/// 1. Guild context validation
/// 2. User type validation (IUser vs SocketGuildUser)
/// 3. Error messages and success conditions
///
/// Full integration testing with actual SocketGuildUser objects would be needed to test
/// voice channel presence detection.
/// </summary>
public class RequireVoiceChannelAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly RequireVoiceChannelAttribute _attribute;

    public RequireVoiceChannelAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _attribute = new RequireVoiceChannelAttribute();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenContextGuildIsNull_ShouldReturnError()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        _mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the command was used in a DM (no guild context)");
        result.ErrorReason.Should().Be(
            "This command can only be used in a server.",
            "the error message should indicate guild context is required");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserIsNotSocketGuildUser_ShouldReturnError()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var mockUser = new Mock<IUser>(); // Not SocketGuildUser
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the user is not a SocketGuildUser");
        result.ErrorReason.Should().Be(
            "Could not verify user's voice state.",
            "the error message should indicate the user's voice state cannot be verified");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenMultipleConditionsFail_ShouldCheckGuildFirst()
    {
        // Arrange - no guild, so guild user type check shouldn't occur
        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        _mockContext.Setup(c => c.User).Returns(new Mock<IUser>().Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Be("This command can only be used in a server.",
            "guild context check should happen before user type check");
    }

    [Fact]
    public void CheckRequirementsAsync_Contract_DocumentsRequirements()
    {
        // This test documents the contract that CheckRequirementsAsync must fulfill:
        //
        // 1. If context.Guild is null, return error: "This command can only be used in a server."
        // 2. If context.User is not a SocketGuildUser, return error: "Could not verify user's voice state."
        // 3. If guildUser.VoiceChannel is null, return error: "You need to be in a voice channel to use this command."
        // 4. If all checks pass, return success.
        //
        // Integration tests using actual SocketGuildUser instances would be required
        // to verify voice channel presence detection, as SocketGuildUser is a sealed type
        // with non-overridable members that cannot be mocked.

        _attribute.Should().NotBeNull();
    }
}
