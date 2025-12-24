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
    public async Task SetActiveStatusAsync_UpdatesStatus()
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
        await _repository.SetActiveStatusAsync(123456789, false);

        // Assert
        // Detach the tracked entity and reload from database to see ExecuteUpdateAsync changes
        _context.Entry(guild).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedGuild = await _context.Guilds.FindAsync(123456789UL);
        updatedGuild.Should().NotBeNull();
        updatedGuild!.IsActive.Should().BeFalse();
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
}
