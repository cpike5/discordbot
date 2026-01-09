using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireAudioEnabledAttribute"/>.
/// </summary>
public class RequireAudioEnabledAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGuildAudioSettingsService> _mockAudioSettingsService;
    private readonly RequireAudioEnabledAttribute _attribute;

    public RequireAudioEnabledAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockAudioSettingsService = new Mock<IGuildAudioSettingsService>();
        _attribute = new RequireAudioEnabledAttribute();

        // Setup default service provider behavior
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IGuildAudioSettingsService)))
            .Returns(_mockAudioSettingsService.Object);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenAudioIsEnabled_ShouldReturnSuccess()
    {
        // Arrange
        var guildId = 123456789UL;
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId);

        var audioSettings = new GuildAudioSettings
        {
            GuildId = guildId,
            AudioEnabled = true
        };

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockAudioSettingsService
            .Setup(s => s.GetSettingsAsync(guildId, default))
            .ReturnsAsync(audioSettings);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("audio features are enabled for the guild");
        result.ErrorReason.Should().BeNull();
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(guildId, default),
            Times.Once,
            "the service should be called once to check audio settings");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenAudioIsDisabled_ShouldReturnError()
    {
        // Arrange
        var guildId = 123456789UL;
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId);

        var audioSettings = new GuildAudioSettings
        {
            GuildId = guildId,
            AudioEnabled = false
        };

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockAudioSettingsService
            .Setup(s => s.GetSettingsAsync(guildId, default))
            .ReturnsAsync(audioSettings);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("audio features are disabled for the guild");
        result.ErrorReason.Should().Contain(
            "Audio features are disabled for this server",
            "the error message should inform the user that audio is disabled");
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(guildId, default),
            Times.Once);
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
        result.ErrorReason.Should().Be(
            "This command can only be used in a server.",
            "the error message should indicate guild context is required");
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(It.IsAny<ulong>(), default),
            Times.Never,
            "the service should not be called when there is no guild context");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenMultipleGuilds_ShouldCheckCorrectGuild()
    {
        // Arrange
        var guildId1 = 111111111UL;
        var guildId2 = 222222222UL;
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId1);

        var audioSettings = new GuildAudioSettings
        {
            GuildId = guildId1,
            AudioEnabled = true
        };

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockAudioSettingsService
            .Setup(s => s.GetSettingsAsync(guildId1, default))
            .ReturnsAsync(audioSettings);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(guildId1, default),
            Times.Once,
            "the service should be called with the correct guild ID");
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(guildId2, default),
            Times.Never,
            "the service should not be called with other guild IDs");
    }
}
