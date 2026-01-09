using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for AudioService.
/// NOTE: Testing Discord voice channel operations is limited because DiscordSocketClient,
/// SocketGuild, and SocketVoiceChannel are sealed classes that cannot be easily mocked.
/// These tests focus on testing state management and thread-safety logic.
/// Integration testing provides better coverage for actual Discord client interactions.
/// </summary>
public class AudioServiceTests : IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly Mock<ILogger<AudioService>> _mockLogger;
    private readonly AudioService _service;

    public AudioServiceTests()
    {
        _client = new DiscordSocketClient();
        _mockLogger = new Mock<ILogger<AudioService>>();
        _service = new AudioService(_client, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    #region IsConnected Tests

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act
        var result = _service.IsConnected(guildId);

        // Assert
        result.Should().BeFalse("no voice connection has been established");
    }

    [Fact]
    public void IsConnected_WithDifferentGuildIds_ReturnsFalseForEach()
    {
        // Arrange
        var guildIds = new ulong[] { 111111111UL, 222222222UL, 333333333UL };

        // Act & Assert
        foreach (var guildId in guildIds)
        {
            _service.IsConnected(guildId).Should().BeFalse(
                $"guild {guildId} should not have a connection");
        }
    }

    #endregion

    #region GetConnectedChannelId Tests

    [Fact]
    public void GetConnectedChannelId_WhenNotConnected_ReturnsNull()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act
        var result = _service.GetConnectedChannelId(guildId);

        // Assert
        result.Should().BeNull("no voice connection exists for this guild");
    }

    #endregion

    #region GetAudioClient Tests

    [Fact]
    public void GetAudioClient_WhenNotConnected_ReturnsNull()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act
        var result = _service.GetAudioClient(guildId);

        // Assert
        result.Should().BeNull("no audio client exists for this guild");
    }

    #endregion

    #region JoinChannelAsync Tests - Error Cases

    [Fact]
    public async Task JoinChannelAsync_WhenGuildNotFound_ReturnsNull()
    {
        // Arrange
        // Using an invalid guild ID that won't exist in the client
        ulong invalidGuildId = 999999999999999999UL;
        ulong voiceChannelId = 123456789UL;

        // Act
        var result = await _service.JoinChannelAsync(invalidGuildId, voiceChannelId);

        // Assert
        result.Should().BeNull("guild does not exist in the client");
        _service.IsConnected(invalidGuildId).Should().BeFalse();
    }

    #endregion

    #region LeaveChannelAsync Tests

    [Fact]
    public async Task LeaveChannelAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act
        var result = await _service.LeaveChannelAsync(guildId);

        // Assert
        result.Should().BeFalse("cannot leave a channel when not connected");
    }

    [Fact]
    public async Task LeaveChannelAsync_MultipleCalls_AllReturnFalse()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act
        var results = new List<bool>();
        for (int i = 0; i < 3; i++)
        {
            results.Add(await _service.LeaveChannelAsync(guildId));
        }

        // Assert
        results.Should().AllBeEquivalentTo(false, "all calls should return false when not connected");
    }

    #endregion

    #region DisconnectAllAsync Tests

    [Fact]
    public async Task DisconnectAllAsync_WhenNoConnectionsExist_CompletesSuccessfully()
    {
        // Act - Should not throw
        await _service.DisconnectAllAsync();

        // Assert - No exceptions thrown, service still functional
        _service.IsConnected(123456789UL).Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAllAsync_CanBeCalledMultipleTimes()
    {
        // Act - Should not throw even when called multiple times
        await _service.DisconnectAllAsync();
        await _service.DisconnectAllAsync();
        await _service.DisconnectAllAsync();

        // Assert - Service is still operational
        _service.IsConnected(123456789UL).Should().BeFalse();
    }

    #endregion

    #region UpdateLastActivity Tests

    [Fact]
    public void UpdateLastActivity_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act & Assert - Should not throw
        var action = () => _service.UpdateLastActivity(guildId);
        action.Should().NotThrow("updating activity for non-existent connection is a no-op");
    }

    [Fact]
    public void UpdateLastActivity_MultipleCalls_DoNotThrow()
    {
        // Arrange
        ulong guildId = 987654321UL;

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var action = () => _service.UpdateLastActivity(guildId);
            action.Should().NotThrow();
        }
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task LeaveChannelAsync_ConcurrentCalls_DoNotThrow()
    {
        // Arrange
        ulong guildId = 987654321UL;
        int concurrentCalls = 10;

        // Act - Fire concurrent leave requests
        var tasks = Enumerable
            .Range(0, concurrentCalls)
            .Select(_ => _service.LeaveChannelAsync(guildId))
            .ToList();

        // Should not throw
        var results = await Task.WhenAll(tasks);

        // Assert - All should return false since no connection exists
        results.Should().AllBeEquivalentTo(false);
    }

    [Fact]
    public async Task JoinChannelAsync_ConcurrentCalls_DoNotThrow()
    {
        // Arrange
        ulong guildId = 987654321UL;
        ulong channelId = 123456789UL;
        int concurrentCalls = 5;

        // Act - Fire concurrent join requests (all will fail due to no guild)
        var tasks = Enumerable
            .Range(0, concurrentCalls)
            .Select(_ => _service.JoinChannelAsync(guildId, channelId))
            .ToList();

        // Should not throw
        var results = await Task.WhenAll(tasks);

        // Assert - All should return null since guild doesn't exist
        results.Should().AllSatisfy(r => r.Should().BeNull());
    }

    [Fact]
    public async Task MixedOperations_ConcurrentCalls_DoNotThrow()
    {
        // Arrange
        ulong guildId = 987654321UL;
        ulong channelId = 123456789UL;

        // Act - Mix of operations running concurrently
        var tasks = new List<Task>
        {
            _service.JoinChannelAsync(guildId, channelId),
            _service.LeaveChannelAsync(guildId),
            Task.Run(() => _service.IsConnected(guildId)),
            Task.Run(() => _service.GetConnectedChannelId(guildId)),
            Task.Run(() => _service.GetAudioClient(guildId)),
            Task.Run(() => _service.UpdateLastActivity(guildId)),
            _service.DisconnectAllAsync()
        };

        // Assert - Should complete without throwing
        await Task.WhenAll(tasks);
    }

    #endregion

    #region State Consistency Tests

    [Fact]
    public void InitialState_NoConnections()
    {
        // Arrange - Fresh service instance already created in constructor

        // Assert - Check initial state for various guild IDs
        for (ulong guildId = 1; guildId <= 5; guildId++)
        {
            _service.IsConnected(guildId).Should().BeFalse();
            _service.GetConnectedChannelId(guildId).Should().BeNull();
            _service.GetAudioClient(guildId).Should().BeNull();
        }
    }

    #endregion
}
