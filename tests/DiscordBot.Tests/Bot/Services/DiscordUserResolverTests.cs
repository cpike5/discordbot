using Discord.WebSocket;
using DiscordBot.Bot.Services.DiscordIntegration;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="DiscordUserResolver"/>.
/// NOTE: DiscordSocketClient is a concrete class that cannot be easily mocked,
/// so these tests use a real (disconnected) client instance. REST calls will fail,
/// exercising the fallback/error paths.
/// </summary>
public class DiscordUserResolverTests : IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<DiscordUserResolver>> _mockLogger;
    private readonly DiscordUserResolver _resolver;

    public DiscordUserResolverTests()
    {
        _client = new DiscordSocketClient();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<DiscordUserResolver>>();
        _resolver = new DiscordUserResolver(_client, _cache, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        _cache.Dispose();
    }

    [Fact]
    public async Task ResolveUserAsync_UnknownUser_ReturnsFallbackValues()
    {
        // Arrange - use a user ID that won't exist (client is not connected)
        const ulong unknownUserId = 123456789012345678;

        // Act
        var (username, avatarUrl) = await _resolver.ResolveUserAsync(unknownUserId);

        // Assert
        username.Should().Be($"Unknown#{unknownUserId}");
        avatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUsersAsync_EmptyInput_ReturnsEmptyDictionary()
    {
        // Act
        var results = await _resolver.ResolveUsersAsync(Enumerable.Empty<ulong>());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveUsersAsync_DuplicateIds_DeduplicatesResults()
    {
        // Arrange
        const ulong userId = 987654321098765432;
        var duplicateIds = new[] { userId, userId, userId };

        // Act
        var results = await _resolver.ResolveUsersAsync(duplicateIds);

        // Assert - should only have one entry despite three duplicates
        results.Should().HaveCount(1);
        results.Should().ContainKey(userId);
        results[userId].Username.Should().Be($"Unknown#{userId}");
    }

    [Fact]
    public async Task ResolveUserAsync_CachesResult_ReturnsCachedOnSecondCall()
    {
        // Arrange
        const ulong userId = 111222333444555666;

        // Act - call twice
        var first = await _resolver.ResolveUserAsync(userId);
        var second = await _resolver.ResolveUserAsync(userId);

        // Assert - both should return the same fallback (second call should hit cache)
        first.Should().Be(second);
        first.Username.Should().Be($"Unknown#{userId}");
    }

    [Fact]
    public async Task ResolveUsersAsync_MultipleIds_ReturnsAllResults()
    {
        // Arrange
        var userIds = new ulong[] { 100, 200, 300 };

        // Act
        var results = await _resolver.ResolveUsersAsync(userIds);

        // Assert
        results.Should().HaveCount(3);
        results.Should().ContainKey(100ul);
        results.Should().ContainKey(200ul);
        results.Should().ContainKey(300ul);

        foreach (var (id, (username, avatarUrl)) in results)
        {
            username.Should().Be($"Unknown#{id}");
            avatarUrl.Should().BeNull();
        }
    }
}
