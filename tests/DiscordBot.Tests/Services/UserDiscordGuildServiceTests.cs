using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserDiscordGuildService"/>.
/// Tests cover storing, retrieving, and deleting Discord guild memberships.
/// </summary>
public class UserDiscordGuildServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILogger<UserDiscordGuildService>> _mockLogger;
    private readonly UserDiscordGuildService _service;
    private readonly string _testUserId = "test-user-id-123";

    public UserDiscordGuildServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<UserDiscordGuildService>>();
        _service = new UserDiscordGuildService(_context, _mockLogger.Object);
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

        // Use the DbSet<ApplicationUser> from the base IdentityDbContext
        _context.Set<ApplicationUser>().Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region StoreGuildMembershipsAsync Tests

    [Fact]
    public async Task StoreGuildMembershipsAsync_WithNewGuilds_AddsAllMemberships()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 111111111111111111UL, Name = "Guild One", Icon = "abc123", Owner = true, Permissions = 8 },
            new DiscordGuildDto { Id = 222222222222222222UL, Name = "Guild Two", Icon = null, Owner = false, Permissions = 0 },
            new DiscordGuildDto { Id = 333333333333333333UL, Name = "Guild Three", Icon = "def456", Owner = false, Permissions = 32 }
        };

        // Act
        var count = await _service.StoreGuildMembershipsAsync(_testUserId, guilds);

        // Assert
        count.Should().Be(3);

        var storedGuilds = await _context.UserDiscordGuilds
            .Where(g => g.ApplicationUserId == _testUserId)
            .ToListAsync();

        storedGuilds.Should().HaveCount(3);
        storedGuilds.Should().Contain(g => g.GuildId == 111111111111111111UL && g.GuildName == "Guild One" && g.IsOwner);
        storedGuilds.Should().Contain(g => g.GuildId == 222222222222222222UL && g.GuildName == "Guild Two" && !g.IsOwner);
        storedGuilds.Should().Contain(g => g.GuildId == 333333333333333333UL && g.GuildName == "Guild Three");
    }

    [Fact]
    public async Task StoreGuildMembershipsAsync_WithExistingGuilds_UpdatesMemberships()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        // Pre-seed some guild memberships
        var existingMembership = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 111111111111111111UL,
            GuildName = "Old Guild Name",
            GuildIconHash = "old-icon",
            IsOwner = false,
            Permissions = 0,
            CapturedAt = DateTime.UtcNow.AddDays(-30),
            LastUpdatedAt = DateTime.UtcNow.AddDays(-30)
        };
        _context.UserDiscordGuilds.Add(existingMembership);
        await _context.SaveChangesAsync();

        // New guilds with updated info
        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 111111111111111111UL, Name = "New Guild Name", Icon = "new-icon", Owner = true, Permissions = 8 }
        };

        // Act
        var count = await _service.StoreGuildMembershipsAsync(_testUserId, guilds);

        // Assert
        count.Should().Be(1);

        var storedGuild = await _context.UserDiscordGuilds
            .SingleAsync(g => g.ApplicationUserId == _testUserId && g.GuildId == 111111111111111111UL);

        storedGuild.GuildName.Should().Be("New Guild Name");
        storedGuild.GuildIconHash.Should().Be("new-icon");
        storedGuild.IsOwner.Should().BeTrue();
        storedGuild.Permissions.Should().Be(8);
        storedGuild.LastUpdatedAt.Should().BeAfter(storedGuild.CapturedAt);
    }

    [Fact]
    public async Task StoreGuildMembershipsAsync_RemovesGuildsUserLeft()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        // Pre-seed guild memberships including one the user will leave
        var stayingGuild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 111111111111111111UL,
            GuildName = "Staying Guild",
            IsOwner = false,
            Permissions = 0,
            CapturedAt = DateTime.UtcNow.AddDays(-30),
            LastUpdatedAt = DateTime.UtcNow.AddDays(-30)
        };
        var leavingGuild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 999999999999999999UL,
            GuildName = "Leaving Guild",
            IsOwner = false,
            Permissions = 0,
            CapturedAt = DateTime.UtcNow.AddDays(-30),
            LastUpdatedAt = DateTime.UtcNow.AddDays(-30)
        };
        _context.UserDiscordGuilds.AddRange(stayingGuild, leavingGuild);
        await _context.SaveChangesAsync();

        // New guilds list doesn't include the "leaving" guild
        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 111111111111111111UL, Name = "Staying Guild", Owner = false, Permissions = 0 }
        };

        // Act
        var count = await _service.StoreGuildMembershipsAsync(_testUserId, guilds);

        // Assert
        count.Should().Be(1);

        var storedGuilds = await _context.UserDiscordGuilds
            .Where(g => g.ApplicationUserId == _testUserId)
            .ToListAsync();

        storedGuilds.Should().HaveCount(1);
        storedGuilds.Should().NotContain(g => g.GuildId == 999999999999999999UL);
    }

    [Fact]
    public async Task StoreGuildMembershipsAsync_WithEmptyList_RemovesAllMemberships()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        // Pre-seed some guild memberships
        var existingMembership = new UserDiscordGuild
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
        _context.UserDiscordGuilds.Add(existingMembership);
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.StoreGuildMembershipsAsync(_testUserId, Array.Empty<DiscordGuildDto>());

        // Assert
        count.Should().Be(0);

        var storedGuilds = await _context.UserDiscordGuilds
            .Where(g => g.ApplicationUserId == _testUserId)
            .ToListAsync();

        storedGuilds.Should().BeEmpty();
    }

    #endregion

    #region GetUserGuildsAsync Tests

    [Fact]
    public async Task GetUserGuildsAsync_WithExistingGuilds_ReturnsGuildsSortedByName()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var guilds = new[]
        {
            new UserDiscordGuild { Id = Guid.NewGuid(), ApplicationUserId = _testUserId, GuildId = 111UL, GuildName = "Zebra Guild", CapturedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
            new UserDiscordGuild { Id = Guid.NewGuid(), ApplicationUserId = _testUserId, GuildId = 222UL, GuildName = "Alpha Guild", CapturedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
            new UserDiscordGuild { Id = Guid.NewGuid(), ApplicationUserId = _testUserId, GuildId = 333UL, GuildName = "Beta Guild", CapturedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow }
        };
        _context.UserDiscordGuilds.AddRange(guilds);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        result.Should().HaveCount(3);
        result[0].GuildName.Should().Be("Alpha Guild");
        result[1].GuildName.Should().Be("Beta Guild");
        result[2].GuildName.Should().Be("Zebra Guild");
    }

    [Fact]
    public async Task GetUserGuildsAsync_WithNoGuilds_ReturnsEmptyList()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        // Act
        var result = await _service.GetUserGuildsAsync(_testUserId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region HasGuildMembershipAsync Tests

    [Fact]
    public async Task HasGuildMembershipAsync_WhenMembershipExists_ReturnsTrue()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guildId = 111111111111111111UL;

        var membership = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = guildId,
            GuildName = "Test Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.Add(membership);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasGuildMembershipAsync(_testUserId, guildId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasGuildMembershipAsync_WhenMembershipDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);
        var guildId = 111111111111111111UL;

        // Act
        var result = await _service.HasGuildMembershipAsync(_testUserId, guildId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteUserGuildsAsync Tests

    [Fact]
    public async Task DeleteUserGuildsAsync_WithExistingGuilds_DeletesAllMemberships()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        var guilds = new[]
        {
            new UserDiscordGuild { Id = Guid.NewGuid(), ApplicationUserId = _testUserId, GuildId = 111UL, GuildName = "Guild One", CapturedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
            new UserDiscordGuild { Id = Guid.NewGuid(), ApplicationUserId = _testUserId, GuildId = 222UL, GuildName = "Guild Two", CapturedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow }
        };
        _context.UserDiscordGuilds.AddRange(guilds);
        await _context.SaveChangesAsync();

        // Verify guilds exist before deletion
        var countBefore = await _context.UserDiscordGuilds.CountAsync(g => g.ApplicationUserId == _testUserId);
        countBefore.Should().Be(2);

        // Act
        await _service.DeleteUserGuildsAsync(_testUserId);

        // Assert
        var countAfter = await _context.UserDiscordGuilds.CountAsync(g => g.ApplicationUserId == _testUserId);
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task DeleteUserGuildsAsync_WithNoGuilds_CompletesWithoutError()
    {
        // Arrange
        await SeedApplicationUserAsync(_testUserId);

        // Act
        var action = async () => await _service.DeleteUserGuildsAsync(_testUserId);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteUserGuildsAsync_DoesNotAffectOtherUsers()
    {
        // Arrange
        var otherUserId = "other-user-id-456";
        await SeedApplicationUserAsync(_testUserId);
        await SeedApplicationUserAsync(otherUserId);

        var testUserGuild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = _testUserId,
            GuildId = 111UL,
            GuildName = "Test User Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        var otherUserGuild = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = otherUserId,
            GuildId = 222UL,
            GuildName = "Other User Guild",
            CapturedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
        _context.UserDiscordGuilds.AddRange(testUserGuild, otherUserGuild);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteUserGuildsAsync(_testUserId);

        // Assert
        var testUserCount = await _context.UserDiscordGuilds.CountAsync(g => g.ApplicationUserId == _testUserId);
        var otherUserCount = await _context.UserDiscordGuilds.CountAsync(g => g.ApplicationUserId == otherUserId);

        testUserCount.Should().Be(0);
        otherUserCount.Should().Be(1);
    }

    #endregion
}
