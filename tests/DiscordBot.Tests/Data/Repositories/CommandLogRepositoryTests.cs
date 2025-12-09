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
/// Unit tests for CommandLogRepository.
/// </summary>
public class CommandLogRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly CommandLogRepository _repository;
    private readonly Mock<ILogger<CommandLogRepository>> _mockLogger;

    public CommandLogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<CommandLogRepository>>();
        _repository = new CommandLogRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedTestDataAsync()
    {
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
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task LogCommandAsync_CreatesNewLog()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var result = await _repository.LogCommandAsync(
            guildId: 123456789,
            userId: 987654321,
            commandName: "test",
            parameters: "{\"param1\": \"value1\"}",
            responseTimeMs: 150,
            success: true);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.GuildId.Should().Be(123456789);
        result.UserId.Should().Be(987654321);
        result.CommandName.Should().Be("test");
        result.Parameters.Should().Be("{\"param1\": \"value1\"}");
        result.ResponseTimeMs.Should().Be(150);
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify it was saved to the database
        var savedLog = await _context.CommandLogs.FindAsync(result.Id);
        savedLog.Should().NotBeNull();
        savedLog!.CommandName.Should().Be("test");
    }

    [Fact]
    public async Task LogCommandAsync_WithError_SetsErrorMessage()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var result = await _repository.LogCommandAsync(
            guildId: 123456789,
            userId: 987654321,
            commandName: "failing-command",
            parameters: null,
            responseTimeMs: 50,
            success: false,
            errorMessage: "Command execution failed");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Command execution failed");
        result.Parameters.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuildAsync_ReturnsLogsForGuild()
    {
        // Arrange
        await SeedTestDataAsync();

        var guild2 = new Guild
        {
            Id = 111111111,
            Name = "Guild 2",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild2);
        await _context.SaveChangesAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "cmd1", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "cmd2", null, 100, true);
        await _repository.LogCommandAsync(111111111, 987654321, "cmd3", null, 100, true);

        // Act
        var result = await _repository.GetByGuildAsync(123456789);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.GuildId.Should().Be(123456789));
        result.Should().Contain(log => log.CommandName == "cmd1");
        result.Should().Contain(log => log.CommandName == "cmd2");
        result.Should().NotContain(log => log.CommandName == "cmd3");
    }

    [Fact]
    public async Task GetByGuildAsync_ReturnsOrderedByDate()
    {
        // Arrange
        await SeedTestDataAsync();

        // Create logs with different timestamps
        var log1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "first",
            ExecutedAt = DateTime.UtcNow.AddMinutes(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var log2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "second",
            ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
            ResponseTimeMs = 100,
            Success = true
        };

        var log3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "third",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(log1, log2, log3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildAsync(123456789);

        // Assert
        result.Should().HaveCount(3);
        result[0].CommandName.Should().Be("third"); // Most recent first
        result[1].CommandName.Should().Be("second");
        result[2].CommandName.Should().Be("first");
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsLogsForUser()
    {
        // Arrange
        await SeedTestDataAsync();

        var user2 = new User
        {
            Id = 111111111,
            Username = "User2",
            Discriminator = "5678",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user2);
        await _context.SaveChangesAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "cmd1", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "cmd2", null, 100, true);
        await _repository.LogCommandAsync(123456789, 111111111, "cmd3", null, 100, true);

        // Act
        var result = await _repository.GetByUserAsync(987654321);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.UserId.Should().Be(987654321));
        result.Should().Contain(log => log.CommandName == "cmd1");
        result.Should().Contain(log => log.CommandName == "cmd2");
        result.Should().NotContain(log => log.CommandName == "cmd3");
    }

    [Fact]
    public async Task GetByCommandNameAsync_ReturnsLogsForCommand()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act
        var result = await _repository.GetByCommandNameAsync("ping");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.CommandName.Should().Be("ping"));
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsLogsInRange()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var log1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var log2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "recent",
            ExecutedAt = now.AddHours(-2),
            ResponseTimeMs = 100,
            Success = true
        };

        var log3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "future",
            ExecutedAt = now.AddDays(1),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(log1, log2, log3);
        await _context.SaveChangesAsync();

        // Act - Get logs from the last 24 hours
        var result = await _repository.GetByDateRangeAsync(now.AddDays(-1), now);

        // Assert
        result.Should().HaveCount(1);
        result[0].CommandName.Should().Be("recent");
    }

    [Fact]
    public async Task GetFailedCommandsAsync_ReturnsOnlyFailedCommands()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "success1", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "failed1", null, 100, false, "Error 1");
        await _repository.LogCommandAsync(123456789, 987654321, "success2", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "failed2", null, 100, false, "Error 2");

        // Act
        var result = await _repository.GetFailedCommandsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.Success.Should().BeFalse());
        result.Should().Contain(log => log.CommandName == "failed1");
        result.Should().Contain(log => log.CommandName == "failed2");
        result.Should().NotContain(log => log.CommandName == "success1");
        result.Should().NotContain(log => log.CommandName == "success2");
    }

    [Fact]
    public async Task GetCommandUsageStatsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "help", null, 100, true);

        // Act
        var result = await _repository.GetCommandUsageStatsAsync();

        // Assert
        result.Should().HaveCount(3);
        result["ping"].Should().Be(3);
        result["test"].Should().Be(2);
        result["help"].Should().Be(1);
    }

    [Fact]
    public async Task GetCommandUsageStatsAsync_WithSinceParameter_ReturnsFilteredCounts()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;

        // Add old logs
        var oldLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "ping",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var oldLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "ping",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(oldLog1, oldLog2);
        await _context.SaveChangesAsync();

        // Add recent logs
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act - Get stats from the last 24 hours
        var result = await _repository.GetCommandUsageStatsAsync(since: now.AddDays(-1));

        // Assert
        result.Should().HaveCount(2);
        result["ping"].Should().Be(1); // Only the recent ping command
        result["test"].Should().Be(1);
    }
}
