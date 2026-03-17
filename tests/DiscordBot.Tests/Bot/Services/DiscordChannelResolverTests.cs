using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="DiscordChannelResolver"/>.
/// NOTE: DiscordSocketClient, SocketGuild, and SocketChannel are sealed classes
/// that cannot be easily mocked. These tests use a real DiscordSocketClient instance
/// (not connected to Discord) to verify the fallback/error handling behavior.
/// The resolver should return "Unknown Channel" or empty lists when guilds/channels
/// are not found in the client cache.
/// </summary>
public class DiscordChannelResolverTests : IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly Mock<ILogger<DiscordChannelResolver>> _mockLogger;
    private readonly DiscordChannelResolver _resolver;

    // Use a guild ID that won't exist in the disconnected client
    private const ulong NonExistentGuildId = 123456789012345678;
    private const ulong NonExistentChannelId = 987654321098765432;

    public DiscordChannelResolverTests()
    {
        _client = new DiscordSocketClient();
        _mockLogger = new Mock<ILogger<DiscordChannelResolver>>();
        _resolver = new DiscordChannelResolver(_client, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ResolveChannelName_GuildNotFound_ReturnsUnknownChannel()
    {
        // Act
        var result = _resolver.ResolveChannelName(NonExistentGuildId, NonExistentChannelId);

        // Assert
        result.Should().Be("Unknown Channel");
    }

    [Fact]
    public void ResolveChannelNames_GuildNotFound_ReturnsAllUnknown()
    {
        // Arrange
        var channelIds = new List<ulong> { 111111111111111111, 222222222222222222, 333333333333333333 };

        // Act
        var result = _resolver.ResolveChannelNames(NonExistentGuildId, channelIds);

        // Assert
        result.Should().HaveCount(3);
        result.Values.Should().AllBe("Unknown Channel");
        result.Keys.Should().BeEquivalentTo(channelIds);
    }

    [Fact]
    public void ResolveChannelNames_EmptyChannelIds_ReturnsEmptyDictionary()
    {
        // Act
        var result = _resolver.ResolveChannelNames(NonExistentGuildId, Enumerable.Empty<ulong>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetTextChannels_GuildNotFound_ReturnsEmptyList()
    {
        // Act
        var result = _resolver.GetTextChannels(NonExistentGuildId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveChannelName_MultipleCallsSameChannel_ReturnsSameResult()
    {
        // Act - verify idempotency
        var result1 = _resolver.ResolveChannelName(NonExistentGuildId, NonExistentChannelId);
        var result2 = _resolver.ResolveChannelName(NonExistentGuildId, NonExistentChannelId);

        // Assert
        result1.Should().Be(result2);
        result1.Should().Be("Unknown Channel");
    }

    [Fact]
    public void ResolveChannelNames_DuplicateChannelIds_HandlesCorrectly()
    {
        // Arrange - duplicate IDs should not cause issues
        var channelIds = new List<ulong> { 111111111111111111, 111111111111111111 };

        // Act
        var result = _resolver.ResolveChannelNames(NonExistentGuildId, channelIds);

        // Assert - dictionary collapses duplicates to single entry
        result.Should().HaveCount(1);
        result[111111111111111111].Should().Be("Unknown Channel");
    }

    [Fact]
    public void ImplementsInterface()
    {
        // Assert
        _resolver.Should().BeAssignableTo<IDiscordChannelResolver>();
    }
}
