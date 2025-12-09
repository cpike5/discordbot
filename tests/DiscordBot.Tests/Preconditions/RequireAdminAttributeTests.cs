using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireAdminAttribute"/>.
/// </summary>
public class RequireAdminAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly RequireAdminAttribute _attribute;

    public RequireAdminAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _attribute = new RequireAdminAttribute();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserHasAdministratorPermission_ShouldReturnSuccess()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var guildPermissions = new GuildPermissions(administrator: true);
        var mockGuildUser = new Mock<IGuildUser>();
        mockGuildUser.Setup(u => u.GuildPermissions).Returns(guildPermissions);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockContext.Setup(c => c.User).Returns(mockGuildUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("the user has Administrator permission");
        result.ErrorReason.Should().BeNull();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserLacksAdministratorPermission_ShouldReturnError()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var guildPermissions = new GuildPermissions(
            manageChannels: true,
            manageMessages: true,
            administrator: false);
        var mockGuildUser = new Mock<IGuildUser>();
        mockGuildUser.Setup(u => u.GuildPermissions).Returns(guildPermissions);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockContext.Setup(c => c.User).Returns(mockGuildUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the user does not have Administrator permission");
        result.ErrorReason.Should().Be("You must have Administrator permission to use this command.");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenContextGuildIsNull_ShouldReturnError()
    {
        // Arrange
        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the command was used in a DM (no guild context)");
        result.ErrorReason.Should().Be("This command can only be used in a guild (server).");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserIsNotGuildUser_ShouldReturnError()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var mockUser = new Mock<IUser>(); // Not IGuildUser
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
        result.IsSuccess.Should().BeFalse("the user is not an IGuildUser");
        result.ErrorReason.Should().Be("Unable to retrieve guild user information.");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserHasNoPermissions_ShouldReturnError()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var guildPermissions = new GuildPermissions(); // No permissions
        var mockGuildUser = new Mock<IGuildUser>();
        mockGuildUser.Setup(u => u.GuildPermissions).Returns(guildPermissions);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockContext.Setup(c => c.User).Returns(mockGuildUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the user has no permissions");
        result.ErrorReason.Should().Be("You must have Administrator permission to use this command.");
    }
}
