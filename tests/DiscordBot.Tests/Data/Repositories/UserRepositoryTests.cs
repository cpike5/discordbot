using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for UserRepository.
/// </summary>
public class UserRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly UserRepository _repository;
    private readonly Mock<ILogger<UserRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<User>>> _mockBaseLogger;

    public UserRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<UserRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<User>>>();
        _repository = new UserRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByDiscordIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        var user = new User
        {
            Id = 987654321,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDiscordIdAsync(987654321);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(987654321);
        result.Username.Should().Be("TestUser");
        result.Discriminator.Should().Be("1234");
    }

    [Fact]
    public async Task GetByDiscordIdAsync_WithNonExistentUser_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByDiscordIdAsync(999999999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLastSeenAsync_UpdatesTimestamp()
    {
        // Arrange
        var originalLastSeen = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            Id = 987654321,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = originalLastSeen
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        await _repository.UpdateLastSeenAsync(987654321);

        // Assert
        // Detach the tracked entity and reload from database to see ExecuteUpdateAsync changes
        _context.Entry(user).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedUser = await _context.Users.FindAsync(987654321UL);
        updatedUser.Should().NotBeNull();
        updatedUser!.LastSeenAt.Should().BeAfter(originalLastSeen);
        updatedUser.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetRecentlyActiveAsync_ReturnsUsersWithinTimeframe()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var recentUser1 = new User
        {
            Id = 111111111,
            Username = "RecentUser1",
            Discriminator = "0001",
            FirstSeenAt = now.AddDays(-30),
            LastSeenAt = now.AddMinutes(-5)
        };
        var recentUser2 = new User
        {
            Id = 222222222,
            Username = "RecentUser2",
            Discriminator = "0002",
            FirstSeenAt = now.AddDays(-30),
            LastSeenAt = now.AddMinutes(-10)
        };
        var oldUser = new User
        {
            Id = 333333333,
            Username = "OldUser",
            Discriminator = "0003",
            FirstSeenAt = now.AddDays(-30),
            LastSeenAt = now.AddDays(-2)
        };

        await _context.Users.AddRangeAsync(recentUser1, recentUser2, oldUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentlyActiveAsync(TimeSpan.FromHours(1));

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.Id == 111111111);
        result.Should().Contain(u => u.Id == 222222222);
        result.Should().NotContain(u => u.Id == 333333333);
    }

    [Fact]
    public async Task GetRecentlyActiveAsync_ExcludesInactiveUsers()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var activeUser = new User
        {
            Id = 111111111,
            Username = "ActiveUser",
            Discriminator = "0001",
            FirstSeenAt = now.AddDays(-30),
            LastSeenAt = now.AddMinutes(-5)
        };
        var inactiveUser = new User
        {
            Id = 222222222,
            Username = "InactiveUser",
            Discriminator = "0002",
            FirstSeenAt = now.AddDays(-30),
            LastSeenAt = now.AddHours(-25)
        };

        await _context.Users.AddRangeAsync(activeUser, inactiveUser);
        await _context.SaveChangesAsync();

        // Act - Get users active in the last 24 hours
        var result = await _repository.GetRecentlyActiveAsync(TimeSpan.FromHours(24));

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(u => u.Id == 111111111);
        result.Should().NotContain(u => u.Id == 222222222);
    }

    [Fact]
    public async Task UpsertAsync_WithNewUser_CreatesUser()
    {
        // Arrange
        var newUser = new User
        {
            Id = 987654321,
            Username = "NewUser",
            Discriminator = "5678",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.UpsertAsync(newUser);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(987654321);
        result.Username.Should().Be("NewUser");
        result.Discriminator.Should().Be("5678");

        // Verify it was actually saved to the database
        var savedUser = await _context.Users.FindAsync(987654321UL);
        savedUser.Should().NotBeNull();
        savedUser!.Username.Should().Be("NewUser");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingUser_UpdatesUser()
    {
        // Arrange
        var existingUser = new User
        {
            Id = 987654321,
            Username = "OldUsername",
            Discriminator = "0000",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow.AddDays(-1)
        };
        await _context.Users.AddAsync(existingUser);
        await _context.SaveChangesAsync();

        // Detach the entity to simulate a fresh update
        _context.Entry(existingUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updatedUser = new User
        {
            Id = 987654321,
            Username = "NewUsername",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30), // FirstSeenAt should not change
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.UpsertAsync(updatedUser);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(987654321);
        result.Username.Should().Be("NewUsername");
        result.Discriminator.Should().Be("1234");

        // Verify the changes were persisted
        var savedUser = await _context.Users.FindAsync(987654321UL);
        savedUser.Should().NotBeNull();
        savedUser!.Username.Should().Be("NewUsername");
        savedUser.Discriminator.Should().Be("1234");
    }

    [Fact]
    public async Task GetWithCommandLogsAsync_IncludesCommandLogs()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var user = new User
        {
            Id = 987654321,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow
        };

        var commandLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = true
        };

        var commandLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 50,
            Success = true
        };

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.CommandLogs.AddRangeAsync(commandLog1, commandLog2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetWithCommandLogsAsync(987654321);

        // Assert
        result.Should().NotBeNull();
        result!.CommandLogs.Should().HaveCount(2);
        result.CommandLogs.Should().Contain(c => c.CommandName == "test");
        result.CommandLogs.Should().Contain(c => c.CommandName == "ping");
    }
}
