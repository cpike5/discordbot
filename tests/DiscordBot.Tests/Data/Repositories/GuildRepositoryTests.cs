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
/// Unit tests for GuildRepository.
/// </summary>
public class GuildRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly GuildRepository _repository;
    private readonly Mock<ILogger<GuildRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<Guild>>> _mockBaseLogger;

    public GuildRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<GuildRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<Guild>>>();
        _repository = new GuildRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByDiscordIdAsync_WithExistingGuild_ReturnsGuild()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDiscordIdAsync(123456789);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(123456789);
        result.Name.Should().Be("Test Guild");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByDiscordIdAsync_WithNonExistentGuild_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByDiscordIdAsync(999999999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveGuildsAsync_ReturnsOnlyActiveGuilds()
    {
        // Arrange
        var activeGuild1 = new Guild
        {
            Id = 111111111,
            Name = "Active Guild 1",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        var activeGuild2 = new Guild
        {
            Id = 222222222,
            Name = "Active Guild 2",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        var inactiveGuild = new Guild
        {
            Id = 333333333,
            Name = "Inactive Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = false
        };

        await _context.Guilds.AddRangeAsync(activeGuild1, activeGuild2, inactiveGuild);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveGuildsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(g => g.IsActive.Should().BeTrue());
        result.Should().Contain(g => g.Id == 111111111);
        result.Should().Contain(g => g.Id == 222222222);
        result.Should().NotContain(g => g.Id == 333333333);
    }

    [Fact]
    public async Task GetWithCommandLogsAsync_IncludesCommandLogs()
    {
        // Arrange
        var user = new User
        {
            Id = 987654321,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
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

        await _context.Users.AddAsync(user);
        await _context.Guilds.AddAsync(guild);
        await _context.CommandLogs.AddRangeAsync(commandLog1, commandLog2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetWithCommandLogsAsync(123456789);

        // Assert
        result.Should().NotBeNull();
        result!.CommandLogs.Should().HaveCount(2);
        result.CommandLogs.Should().Contain(c => c.CommandName == "test");
        result.CommandLogs.Should().Contain(c => c.CommandName == "ping");
    }

    [Fact]
    public async Task UpsertAsync_WithNewGuild_CreatesGuild()
    {
        // Arrange
        var newGuild = new Guild
        {
            Id = 123456789,
            Name = "New Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true,
            Prefix = "!",
            Settings = "{\"feature1\": true}"
        };

        // Act
        var result = await _repository.UpsertAsync(newGuild);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789);
        result.Name.Should().Be("New Guild");
        result.Prefix.Should().Be("!");
        result.Settings.Should().Be("{\"feature1\": true}");

        // Verify it was actually saved to the database
        var savedGuild = await _context.Guilds.FindAsync(123456789UL);
        savedGuild.Should().NotBeNull();
        savedGuild!.Name.Should().Be("New Guild");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingGuild_UpdatesGuild()
    {
        // Arrange
        var existingGuild = new Guild
        {
            Id = 123456789,
            Name = "Original Name",
            JoinedAt = DateTime.UtcNow.AddDays(-10),
            IsActive = true,
            Prefix = "!",
            Settings = "{\"feature1\": false}"
        };
        await _context.Guilds.AddAsync(existingGuild);
        await _context.SaveChangesAsync();

        // Detach the entity to simulate a fresh update
        _context.Entry(existingGuild).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updatedGuild = new Guild
        {
            Id = 123456789,
            Name = "Updated Name",
            JoinedAt = DateTime.UtcNow.AddDays(-10), // JoinedAt should not change
            IsActive = false,
            Prefix = "?",
            Settings = "{\"feature1\": true, \"feature2\": true}"
        };

        // Act
        var result = await _repository.UpsertAsync(updatedGuild);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789);
        result.Name.Should().Be("Updated Name");
        result.IsActive.Should().BeFalse();
        result.Prefix.Should().Be("?");
        result.Settings.Should().Be("{\"feature1\": true, \"feature2\": true}");

        // Verify the changes were persisted
        var savedGuild = await _context.Guilds.FindAsync(123456789UL);
        savedGuild.Should().NotBeNull();
        savedGuild!.Name.Should().Be("Updated Name");
        savedGuild.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenSetToInactive_UpdatesStatusAndSetsLeftAt()
    {
        // Arrange
        var beforeUpdate = DateTime.UtcNow;
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true,
            LeftAt = null
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();

        // Act
        await _repository.SetActiveStatusAsync(123456789, false);

        // Assert
        // Detach the tracked entity and reload from database to see ExecuteUpdateAsync changes
        _context.Entry(guild).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedGuild = await _context.Guilds.FindAsync(123456789UL);
        updatedGuild.Should().NotBeNull();
        updatedGuild!.IsActive.Should().BeFalse();
        updatedGuild.LeftAt.Should().NotBeNull();
        updatedGuild.LeftAt!.Value.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenSetToActive_UpdatesStatusAndClearsLeftAt()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = false,
            LeftAt = DateTime.UtcNow.AddDays(-1)
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();

        // Act
        await _repository.SetActiveStatusAsync(123456789, true);

        // Assert
        // Detach the tracked entity and reload from database to see ExecuteUpdateAsync changes
        _context.Entry(guild).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedGuild = await _context.Guilds.FindAsync(123456789UL);
        updatedGuild.Should().NotBeNull();
        updatedGuild!.IsActive.Should().BeTrue();
        updatedGuild.LeftAt.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_CreatesGuild()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Act
        var result = await _repository.AddAsync(guild);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789);

        // Verify it was saved to the database
        var savedGuild = await _context.Guilds.FindAsync(123456789UL);
        savedGuild.Should().NotBeNull();
        savedGuild!.Name.Should().Be("Test Guild");
    }

    [Fact]
    public async Task DeleteAsync_RemovesGuild()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(guild);

        // Assert
        var deletedGuild = await _context.Guilds.FindAsync(123456789UL);
        deletedGuild.Should().BeNull();
    }

    [Fact]
    public async Task GetJoinedCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        var oldGuild = new Guild
        {
            Id = 111111111,
            Name = "Old Guild",
            JoinedAt = now.AddDays(-10),
            IsActive = true
        };

        var todayGuild1 = new Guild
        {
            Id = 222222222,
            Name = "Today Guild 1",
            JoinedAt = startOfToday.AddHours(2),  // 2am today - always after midnight
            IsActive = true
        };

        var todayGuild2 = new Guild
        {
            Id = 333333333,
            Name = "Today Guild 2",
            JoinedAt = startOfToday.AddHours(1),  // 1am today - always after midnight
            IsActive = true
        };

        var yesterdayGuild = new Guild
        {
            Id = 444444444,
            Name = "Yesterday Guild",
            JoinedAt = now.AddDays(-1),
            IsActive = true
        };

        await _context.Guilds.AddRangeAsync(oldGuild, todayGuild1, todayGuild2, yesterdayGuild);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetJoinedCountAsync(startOfToday);

        // Assert
        count.Should().Be(2, "there should be 2 guilds that joined today");
    }

    [Fact]
    public async Task GetJoinedCountAsync_WithNoRecentJoins_ReturnsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var oldGuild1 = new Guild
        {
            Id = 111111111,
            Name = "Old Guild 1",
            JoinedAt = now.AddDays(-30),
            IsActive = true
        };

        var oldGuild2 = new Guild
        {
            Id = 222222222,
            Name = "Old Guild 2",
            JoinedAt = now.AddDays(-15),
            IsActive = true
        };

        await _context.Guilds.AddRangeAsync(oldGuild1, oldGuild2);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetJoinedCountAsync(now.Date);

        // Assert
        count.Should().Be(0, "there should be no guilds that joined today");
    }

    [Fact]
    public async Task GetJoinedCountAsync_IncludesBothActiveAndInactiveGuilds()
    {
        // Arrange
        // Use today at noon to avoid flaky timing issues near midnight
        var today = DateTime.UtcNow.Date;
        var noonToday = today.AddHours(12);

        var activeGuild = new Guild
        {
            Id = 111111111,
            Name = "Active Guild",
            JoinedAt = noonToday,
            IsActive = true
        };

        var inactiveGuild = new Guild
        {
            Id = 222222222,
            Name = "Inactive Guild",
            JoinedAt = noonToday.AddHours(-1),
            IsActive = false
        };

        await _context.Guilds.AddRangeAsync(activeGuild, inactiveGuild);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetJoinedCountAsync(today);

        // Assert
        count.Should().Be(2, "both active and inactive guilds that joined should be counted");
    }

    [Fact]
    public async Task GetJoinedCountAsync_WithCustomDateRange_ReturnsCorrectCount()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        var guild1 = new Guild
        {
            Id = 111111111,
            Name = "Guild 1",
            JoinedAt = now.AddDays(-2),
            IsActive = true
        };

        var guild2 = new Guild
        {
            Id = 222222222,
            Name = "Guild 2",
            JoinedAt = now.AddDays(-5),
            IsActive = true
        };

        var guild3 = new Guild
        {
            Id = 333333333,
            Name = "Guild 3",
            JoinedAt = now.AddDays(-10),
            IsActive = true
        };

        await _context.Guilds.AddRangeAsync(guild1, guild2, guild3);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetJoinedCountAsync(sevenDaysAgo);

        // Assert
        count.Should().Be(2, "there should be 2 guilds that joined in the last 7 days");
    }

    [Fact]
    public async Task GetLeftCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        var activeGuild = new Guild
        {
            Id = 111111111,
            Name = "Active Guild",
            JoinedAt = now.AddDays(-10),
            IsActive = true,
            LeftAt = null
        };

        var inactiveGuildLeftToday = new Guild
        {
            Id = 222222222,
            Name = "Left Today",
            JoinedAt = now.AddDays(-5),
            IsActive = false,
            LeftAt = startOfToday.AddHours(2)
        };

        var inactiveGuildLeftLastWeek = new Guild
        {
            Id = 333333333,
            Name = "Left Last Week",
            JoinedAt = now.AddDays(-15),
            IsActive = false,
            LeftAt = now.AddDays(-7)
        };

        await _context.Guilds.AddRangeAsync(activeGuild, inactiveGuildLeftToday, inactiveGuildLeftLastWeek);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetLeftCountAsync(startOfToday);

        // Assert
        count.Should().Be(1, "only one guild left today (guild 222222222)");
    }

    [Fact]
    public async Task GetLeftCountAsync_WithNoRecentLeaves_ReturnsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var activeGuild = new Guild
        {
            Id = 111111111,
            Name = "Active Guild",
            JoinedAt = now.AddDays(-10),
            IsActive = true,
            LeftAt = null
        };

        var inactiveGuildNoLeftAt = new Guild
        {
            Id = 222222222,
            Name = "Inactive But No LeftAt",
            JoinedAt = now.AddDays(-5),
            IsActive = false,
            LeftAt = null
        };

        await _context.Guilds.AddRangeAsync(activeGuild, inactiveGuildNoLeftAt);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetLeftCountAsync(now.Date);

        // Assert
        count.Should().Be(0, "no guilds have LeftAt set today");
    }

    [Fact]
    public async Task GetLeftCountAsync_WithCustomDateRange_ReturnsCorrectCount()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        var guildLeftRecently = new Guild
        {
            Id = 111111111,
            Name = "Left 2 Days Ago",
            JoinedAt = now.AddDays(-30),
            IsActive = false,
            LeftAt = now.AddDays(-2)
        };

        var guildLeftThisWeek = new Guild
        {
            Id = 222222222,
            Name = "Left 5 Days Ago",
            JoinedAt = now.AddDays(-20),
            IsActive = false,
            LeftAt = now.AddDays(-5)
        };

        var guildLeftLongAgo = new Guild
        {
            Id = 333333333,
            Name = "Left 10 Days Ago",
            JoinedAt = now.AddDays(-40),
            IsActive = false,
            LeftAt = now.AddDays(-10)
        };

        await _context.Guilds.AddRangeAsync(guildLeftRecently, guildLeftThisWeek, guildLeftLongAgo);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetLeftCountAsync(sevenDaysAgo);

        // Assert
        count.Should().Be(2, "two guilds left in the last 7 days");
    }
}
