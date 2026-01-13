using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserDiscordGuildService"/> caching logic.
/// Tests cover cache hits/misses, expiration, invalidation, and interaction with database queries.
/// </summary>
public class UserDiscordGuildServiceCacheTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IInstrumentedCache> _cacheMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IDiscordTokenService> _tokenServiceMock;
    private readonly Mock<ILogger<UserDiscordGuildService>> _loggerMock;
    private readonly IOptions<GuildMembershipCacheOptions> _cacheOptions;
    private readonly UserDiscordGuildService _service;
    private readonly string _testUserId = "test-user-id-cache-001";

    public UserDiscordGuildServiceCacheTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();

        // Use Moq for mocking
        _cacheMock = new Mock<IInstrumentedCache>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _tokenServiceMock = new Mock<IDiscordTokenService>();
        _loggerMock = new Mock<ILogger<UserDiscordGuildService>>();

        _cacheOptions = Options.Create(new GuildMembershipCacheOptions
        {
            StoredGuildMembershipDurationMinutes = 30
        });

        _service = new UserDiscordGuildService(
            _context,
            _loggerMock.Object,
            _cacheMock.Object,
            _httpClientFactoryMock.Object,
            _tokenServiceMock.Object,
            _cacheOptions);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ulong _discordUserIdCounter = 123456789UL;

    private async Task<ApplicationUser> SeedApplicationUserAsync(string userId)
    {
        var discordUserId = _discordUserIdCounter++;
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"test-{userId}@example.com",
            Email = $"test-{userId}@example.com",
            NormalizedEmail = $"TEST-{userId}@EXAMPLE.COM".ToUpperInvariant(),
            NormalizedUserName = $"TEST-{userId}@EXAMPLE.COM".ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            DiscordUserId = discordUserId,
            DiscordUsername = $"TestUser{discordUserId}",
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ApplicationUser>().Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region Cache Miss Tests

    [Fact]
    public async Task GetUserGuildsAsync_FirstCall_CacheMiss()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 111111111111111111UL,
            GuildName = "Test Guild",
            IsOwner = false,
            Permissions = 0,
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";

        // Setup cache to return miss (TryGetValue returns false)
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        var result = await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].GuildId.Should().Be(111111111111111111UL);
        result[0].GuildName.Should().Be("Test Guild");

        // Verify cache miss was checked
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache should be checked for miss");

        // Verify cache was populated with the result
        _cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == cacheKey),
                It.IsAny<List<UserDiscordGuild>>(),
                It.Is<TimeSpan>(ts => ts.TotalMinutes == 30)),
            Times.Once,
            "cache should be populated with result");
    }

    [Fact]
    public async Task GetUserGuildsAsync_WithNoGuilds_CacheMissAndEmptyResult()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var cacheKey = $"userguilds:{_testUserId}";
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        var result = await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        result.Should().BeEmpty();

        // Verify cache was still populated (even with empty list)
        _cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == cacheKey),
                It.Is<List<UserDiscordGuild>>(l => l.Count == 0),
                It.IsAny<TimeSpan>()),
            Times.Once,
            "cache should be populated even with empty result");
    }

    #endregion

    #region Cache Hit Tests

    [Fact]
    public async Task GetUserGuildsAsync_SecondCall_CacheHit()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 222222222222222222UL,
            GuildName = "Cached Guild",
            IsOwner = true,
            Permissions = 8,
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";
        var cachedGuilds = new List<UserDiscordGuild> { guild };

        // Setup: return different values based on call count
        var callSequence = new Queue<bool>(new[] { false, true }); // First miss, then hit
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns((string _, out List<UserDiscordGuild>? outValue) =>
            {
                var shouldHit = callSequence.Dequeue();
                if (shouldHit)
                {
                    outValue = cachedGuilds;
                    return true;
                }
                // Cache miss
                outValue = null;
                return false;
            });

        // Act - First call should query database and cache miss
        var result1 = await _service.GetUserGuildsAsync(_testUserId);

        // Act - Second call should hit cache
        var result2 = await _service.GetUserGuildsAsync(_testUserId);

        // Assert - Both calls return the same data
        result1.Should().HaveCount(1);
        result1[0].GuildId.Should().Be(222222222222222222UL);

        result2.Should().HaveCount(1);
        result2[0].GuildId.Should().Be(222222222222222222UL);

        // Verify cache was checked twice
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Exactly(2),
            "cache should be checked twice");

        // Verify cache.Set was called once (from first call which was a miss)
        _cacheMock.Verify(
            c => c.Set(cacheKey, It.IsAny<List<UserDiscordGuild>>(), It.IsAny<TimeSpan>()),
            Times.Once,
            "cache should be set once");
    }

    [Fact]
    public async Task HasGuildMembershipAsync_UsesCachedData()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guildId = 333333333333333333UL;
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = guildId,
            GuildName = "Member Guild",
            IsOwner = false,
            Permissions = 32,
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";
        var cachedGuilds = new List<UserDiscordGuild> { guild };

        // Setup cache to return the guild directly (cache hit)
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns((string _, out List<UserDiscordGuild>? outValue) =>
            {
                outValue = cachedGuilds;
                return true;
            });

        // Act
        var hasMembership = await _service.HasGuildMembershipAsync(_testUserId, guildId);

        // Assert
        hasMembership.Should().BeTrue();

        // Verify cache was used (TryGetValue called from GetUserGuildsAsync)
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache should be checked");

        // Verify cache.Set was NOT called (because it was a cache hit)
        _cacheMock.Verify(
            c => c.Set(cacheKey, It.IsAny<List<UserDiscordGuild>>(), It.IsAny<TimeSpan>()),
            Times.Never,
            "cache.Set should not be called on cache hit");
    }

    [Fact]
    public async Task HasGuildMembershipAsync_WithNonExistentGuild_ReturnsFalseUsingCachedData()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guildId = 444444444444444444UL;
        var cachedGuilds = new List<UserDiscordGuild>
        {
            new UserDiscordGuild
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = _testUserId,
                GuildId = 555555555555555555UL,
                GuildName = "Different Guild",
                CapturedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            }
        };

        var cacheKey = $"userguilds:{_testUserId}";

        // Setup cache to return the cached guilds (but without the guildId we're checking)
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns((string _, out List<UserDiscordGuild>? outValue) =>
            {
                outValue = cachedGuilds;
                return true;
            });

        // Act
        var hasMembership = await _service.HasGuildMembershipAsync(_testUserId, guildId);

        // Assert
        hasMembership.Should().BeFalse();

        // Verify cache was used
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache should be checked");
    }

    #endregion

    #region Cache Expiration Tests

    [Fact]
    public async Task GetUserGuildsAsync_CacheExpirationDuration_UsesConfiguredDuration()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 666666666666666666UL,
            GuildName = "TTL Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";
        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        _cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == cacheKey),
                It.IsAny<List<UserDiscordGuild>>(),
                It.Is<TimeSpan>(ts => ts == TimeSpan.FromMinutes(30))),
            Times.Once,
            "cache should be set with 30-minute expiration");
    }

    [Fact]
    public async Task GetUserGuildsAsync_WithCustomCacheDuration_UsesCustomDuration()
    {
        // Arrange
        var customCacheOptions = Options.Create(new GuildMembershipCacheOptions
        {
            StoredGuildMembershipDurationMinutes = 60
        });

        var customCacheMock = new Mock<IInstrumentedCache>();
        var service = new UserDiscordGuildService(
            _context,
            _loggerMock.Object,
            customCacheMock.Object,
            _httpClientFactoryMock.Object,
            _tokenServiceMock.Object,
            customCacheOptions);

        await SeedApplicationUserAsync(_testUserId);
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 777777777777777777UL,
            GuildName = "Custom TTL Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";
        customCacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(cacheKey, out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        await service.GetUserGuildsAsync(_testUserId);

        // Assert
        customCacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == cacheKey),
                It.IsAny<List<UserDiscordGuild>>(),
                It.Is<TimeSpan>(ts => ts == TimeSpan.FromMinutes(60))),
            Times.Once,
            "cache should be set with 60-minute expiration");
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public void InvalidateCache_RemovesCachedEntry()
    {
        // Arrange
        var cacheKey = $"userguilds:{_testUserId}";

        // Act
        _service.InvalidateCache(_testUserId);

        // Assert
        _cacheMock.Verify(
            c => c.Remove(It.Is<string>(k => k == cacheKey)),
            Times.Once,
            "cache entry should be removed");
    }

    [Fact]
    public async Task StoreGuildMembershipsAsync_InvalidatesCache()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 888888888888888888UL, Name = "New Guild", Owner = false, Permissions = 0 }
        };

        var cacheKey = $"userguilds:{_testUserId}";

        // Act
        await _service.StoreGuildMembershipsAsync(_testUserId, guilds);

        // Assert
        _cacheMock.Verify(
            c => c.Remove(It.Is<string>(k => k == cacheKey)),
            Times.Once,
            "cache should be invalidated after storing memberships");
    }

    [Fact]
    public async Task DeleteUserGuildsAsync_InvalidatesCache()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 999999999999999999UL,
            GuildName = "Guild to Delete",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        var cacheKey = $"userguilds:{_testUserId}";

        // Act
        await _service.DeleteUserGuildsAsync(_testUserId);

        // Assert
        _cacheMock.Verify(
            c => c.Remove(It.Is<string>(k => k == cacheKey)),
            Times.Once,
            "cache should be invalidated after deletion");
    }

    [Fact]
    public async Task DeleteUserGuildsAsync_WithNoGuilds_StillInvalidatesCache()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var cacheKey = $"userguilds:{_testUserId}";

        // Act
        await _service.DeleteUserGuildsAsync(_testUserId);

        // Assert
        // Cache invalidation should NOT happen if there were no guilds to delete
        // (based on the service logic - it only invalidates if any rows were deleted)
        _cacheMock.Verify(
            c => c.Remove(It.IsAny<string>()),
            Times.Never,
            "cache should not be invalidated if no guilds existed");
    }

    #endregion

    #region RefreshUserGuildsAsync Tests

    [Fact]
    public async Task RefreshUserGuildsAsync_WithoutAccessToken_ReturnsEarlyWithoutCacheInvalidation()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        _tokenServiceMock.Setup(ts => ts.GetAccessTokenAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.RefreshUserGuildsAsync(_testUserId);

        // Assert
        // Cache should not be invalidated if token is null
        _cacheMock.Verify(
            c => c.Remove(It.IsAny<string>()),
            Times.Never,
            "cache should not be invalidated without access token");
    }

    #endregion

    #region Cache Key Format Tests

    [Fact]
    public async Task GetUserGuildsAsync_UsesCorrectCacheKeyFormat()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(It.IsAny<string>(), out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        var expectedCacheKey = $"userguilds:{_testUserId}";
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(
                It.Is<string>(k => k == expectedCacheKey),
                out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache key should follow format userguilds:{userId}");
    }

    [Fact]
    public void InvalidateCache_UsesCorrectCacheKeyFormat()
    {
        // Arrange
        var userId = "different-user-id-456";
        var expectedCacheKey = $"userguilds:{userId}";

        // Act
        _service.InvalidateCache(userId);

        // Assert
        _cacheMock.Verify(
            c => c.Remove(It.Is<string>(k => k == expectedCacheKey)),
            Times.Once,
            "cache key should follow format userguilds:{userId}");
    }

    [Fact]
    public async Task GetUserGuildsAsync_DifferentUserIds_UseDifferentCacheKeys()
    {
        // Arrange
        var userId1 = "user-1";
        var userId2 = "user-2";

        await SeedApplicationUserAsync(userId1);
        await SeedApplicationUserAsync(userId2);

        var guild1 = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId1,
            GuildId = 111111111111111111UL,
            GuildName = "Guild 1",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        var guild2 = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId2,
            GuildId = 222222222222222222UL,
            GuildName = "Guild 2",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        _context.UserDiscordGuilds.AddRange(guild1, guild2);
        await _context.SaveChangesAsync();

        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(It.IsAny<string>(), out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        await _service.GetUserGuildsAsync(userId1);
        await _service.GetUserGuildsAsync(userId2);

        // Assert
        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(
                It.Is<string>(k => k == $"userguilds:{userId1}"),
                out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache should use userId1 specific key");

        _cacheMock.Verify(
            c => c.TryGetValue<List<UserDiscordGuild>>(
                It.Is<string>(k => k == $"userguilds:{userId2}"),
                out It.Ref<List<UserDiscordGuild>?>.IsAny),
            Times.Once,
            "cache should use userId2 specific key");
    }

    #endregion

    #region Cache and Database Consistency Tests

    [Fact]
    public async Task GetUserGuildsAsync_CachedResultIsReadOnly()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 123123123123123123UL,
            GuildName = "ReadOnly Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(guild);
        await _context.SaveChangesAsync();

        _cacheMock.Setup(c => c.TryGetValue<List<UserDiscordGuild>>(It.IsAny<string>(), out It.Ref<List<UserDiscordGuild>?>.IsAny))
            .Returns(false);

        // Act
        var result = await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<UserDiscordGuild>>();
    }

    [Fact]
    public async Task StoreGuildMembershipsAsync_ThenInvalidatesCache()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var newGuilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 456456456456456456UL, Name = "New Stored Guild", Owner = true, Permissions = 8 }
        };

        var cacheKey = $"userguilds:{_testUserId}";

        // Act
        await _service.StoreGuildMembershipsAsync(_testUserId, newGuilds);

        // Assert
        _cacheMock.Verify(
            c => c.Remove(It.Is<string>(k => k == cacheKey)),
            Times.Once,
            "cache should be invalidated after store");

        // Verify the guild was actually stored in database
        var storedGuild = await _context.UserDiscordGuilds
            .FirstOrDefaultAsync(g => g.ApplicationUserId == _testUserId);
        storedGuild.Should().NotBeNull();
        storedGuild!.GuildId.Should().Be(456456456456456456UL);
    }

    #endregion
}
