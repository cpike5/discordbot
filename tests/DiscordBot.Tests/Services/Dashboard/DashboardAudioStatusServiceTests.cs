using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Dashboard;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Dashboard;

/// <summary>
/// Unit tests for <see cref="DashboardAudioStatusService"/>.
/// Moved out of DashboardHubTests when DashboardHub was split into per-feature services, so
/// this feature is tested directly against the service rather than through the hub.
/// </summary>
public class DashboardAudioStatusServiceTests
{
    private readonly Mock<IAudioService> _mockAudioService;
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<DashboardAudioStatusService>> _mockLogger;
    private readonly DashboardAudioStatusService _service;

    private const string ConnectionId = "test-connection-id-123";
    private const string UserName = "testuser";
    private const ulong GuildId = 123456789UL;

    public DashboardAudioStatusServiceTests()
    {
        _mockAudioService = new Mock<IAudioService>();
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(MockBehavior.Default, new DiscordSocketConfig());
        _mockLogger = new Mock<ILogger<DashboardAudioStatusService>>();

        _service = new DashboardAudioStatusService(
            _mockAudioService.Object,
            _mockPlaybackService.Object,
            _mockDiscordClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void GetCurrentAudioStatus_WhenConnectedAndPlaying_ReturnsFullStatus()
    {
        // Arrange
        const ulong channelId = 987654321UL;

        _mockAudioService.Setup(s => s.IsConnected(GuildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(GuildId)).Returns(channelId);
        _mockPlaybackService.Setup(s => s.IsPlaying(GuildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.GetQueueLength(GuildId)).Returns(3);

        // Discord.NET's SocketGuild is not mockable, so the client returns null and the
        // service falls back to a null channel name — exercised separately below.
        _mockDiscordClient.Setup(c => c.GetGuild(GuildId)).Returns((SocketGuild?)null);

        // Act
        var status = _service.GetCurrentAudioStatus(GuildId, ConnectionId, UserName);

        // Assert
        status.Should().NotBeNull();
        status.GuildId.Should().Be(GuildId);
        status.IsConnected.Should().BeTrue();
        status.ChannelId.Should().Be(channelId);
        status.ChannelName.Should().BeNull();
        status.IsPlaying.Should().BeTrue();
        status.QueueLength.Should().Be(3);
    }

    [Fact]
    public void GetCurrentAudioStatus_WhenNotConnected_ReturnsDisconnectedStatusWithNoChannel()
    {
        // Arrange
        _mockAudioService.Setup(s => s.IsConnected(GuildId)).Returns(false);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(GuildId)).Returns((ulong?)null);
        _mockPlaybackService.Setup(s => s.IsPlaying(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.GetQueueLength(GuildId)).Returns(0);

        // Act
        var status = _service.GetCurrentAudioStatus(GuildId, ConnectionId, UserName);

        // Assert
        status.IsConnected.Should().BeFalse();
        status.ChannelId.Should().BeNull();
        status.ChannelName.Should().BeNull();
        status.IsPlaying.Should().BeFalse();
        status.QueueLength.Should().Be(0);

        // Not connected means no channel lookup should even be attempted
        _mockDiscordClient.Verify(c => c.GetGuild(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void GetCurrentAudioStatus_SetsTimestampToUtcNow()
    {
        // Arrange
        _mockAudioService.Setup(s => s.IsConnected(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.IsPlaying(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.GetQueueLength(GuildId)).Returns(0);

        var before = DateTime.UtcNow;

        // Act
        var status = _service.GetCurrentAudioStatus(GuildId, ConnectionId, UserName);

        // Assert
        var after = DateTime.UtcNow;
        status.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void GetCurrentAudioStatus_WithNullConnectionAndUserName_DoesNotThrow()
    {
        // Arrange
        _mockAudioService.Setup(s => s.IsConnected(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.IsPlaying(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.GetQueueLength(GuildId)).Returns(0);

        // Act
        var act = () => _service.GetCurrentAudioStatus(GuildId, connectionId: null, userName: null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetCurrentAudioStatus_QueriesAudioAndPlaybackServicesForTheGivenGuild()
    {
        // Arrange
        _mockAudioService.Setup(s => s.IsConnected(GuildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(GuildId)).Returns((ulong?)null);
        _mockPlaybackService.Setup(s => s.IsPlaying(GuildId)).Returns(false);
        _mockPlaybackService.Setup(s => s.GetQueueLength(GuildId)).Returns(0);

        // Act
        _service.GetCurrentAudioStatus(GuildId, ConnectionId, UserName);

        // Assert
        _mockAudioService.Verify(s => s.IsConnected(GuildId), Times.Once);
        _mockAudioService.Verify(s => s.GetConnectedChannelId(GuildId), Times.Once);
        _mockPlaybackService.Verify(s => s.IsPlaying(GuildId), Times.Once);
        _mockPlaybackService.Verify(s => s.GetQueueLength(GuildId), Times.Once);
    }
}
