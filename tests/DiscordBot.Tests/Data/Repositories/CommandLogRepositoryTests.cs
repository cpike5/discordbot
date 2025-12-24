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
    private readonly Mock<ILogger<Repository<CommandLog>>> _mockBaseLogger;

    public CommandLogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<CommandLogRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<CommandLog>>>();
        _repository = new CommandLogRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
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

    #region GetFilteredLogsAsync Tests

    [Fact]
    public async Task GetFilteredLogsAsync_WithNoFilters_ReturnsAllLogsPaginated()
    {
        // Arrange
        await SeedTestDataAsync();

        for (int i = 0; i < 10; i++)
        {
            await _repository.LogCommandAsync(123456789, 987654321, $"cmd{i}", null, 100, true);
        }

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(page: 1, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(10);
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithGuildIdFilter_ReturnsFilteredLogs()
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
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(guildId: 123456789);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(log => log.GuildId.Should().Be(123456789));
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithUserIdFilter_ReturnsFilteredLogs()
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
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(userId: 987654321);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(log => log.UserId.Should().Be(987654321));
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithCommandNameFilter_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(commandName: "ping");

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(log => log.CommandName.Should().Be("ping"));
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithCommandNameFilter_IsCaseInsensitive()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "PING", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "Ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(commandName: "PiNg");

        // Assert
        items.Should().HaveCount(3);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithDateRangeFilters_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;

        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var recentLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "recent",
            ExecutedAt = now.AddHours(-2),
            ResponseTimeMs = 100,
            Success = true
        };

        var futureLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "future",
            ExecutedAt = now.AddDays(1),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(oldLog, recentLog, futureLog);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(
            startDate: now.AddDays(-1),
            endDate: now);

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].CommandName.Should().Be("recent");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithSuccessOnlyFilter_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "success1", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "failed1", null, 100, false, "Error");
        await _repository.LogCommandAsync(123456789, 987654321, "success2", null, 100, true);

        // Act - Get only successful commands
        var (successItems, successCount) = await _repository.GetFilteredLogsAsync(successOnly: true);

        // Assert
        successItems.Should().HaveCount(2);
        successCount.Should().Be(2);
        successItems.Should().AllSatisfy(log => log.Success.Should().BeTrue());

        // Act - Get only failed commands
        var (failedItems, failedCount) = await _repository.GetFilteredLogsAsync(successOnly: false);

        // Assert
        failedItems.Should().HaveCount(1);
        failedCount.Should().Be(1);
        failedItems.Should().AllSatisfy(log => log.Success.Should().BeFalse());
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithSearchTerm_SearchesAcrossCommandName()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "pingpong", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "status", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "ping");

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(log => log.CommandName.ToLower().Should().Contain("ping"));
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithSearchTerm_SearchesAcrossUsername()
    {
        // Arrange
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var user1 = new User
        {
            Id = 111111111,
            Username = "Alice",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        var user2 = new User
        {
            Id = 222222222,
            Username = "Bob",
            Discriminator = "0002",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        await _repository.LogCommandAsync(123456789, 111111111, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 222222222, "ping", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "Alice");

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].User!.Username.Should().Be("Alice");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithSearchTerm_SearchesAcrossGuildName()
    {
        // Arrange
        var guild1 = new Guild
        {
            Id = 111111111,
            Name = "Gaming Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var guild2 = new Guild
        {
            Id = 222222222,
            Name = "Dev Team",
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

        await _context.Guilds.AddRangeAsync(guild1, guild2);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        await _repository.LogCommandAsync(111111111, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(222222222, 987654321, "ping", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "Gaming");

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].Guild!.Name.Should().Contain("Gaming");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithSearchTerm_IsCaseInsensitive()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "PING", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "Ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "PiNg");

        // Assert
        items.Should().HaveCount(3);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithMultipleFilters_ReturnsFilteredLogs()
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

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, false, "Error");
        await _repository.LogCommandAsync(111111111, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(
            guildId: 123456789,
            commandName: "ping",
            successOnly: true);

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].GuildId.Should().Be(123456789);
        items[0].CommandName.Should().Be("ping");
        items[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetFilteredLogsAsync_ReturnsOrderedByDateDescending()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;

        var log1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "first",
            ExecutedAt = now.AddMinutes(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var log2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "second",
            ExecutedAt = now.AddMinutes(-5),
            ResponseTimeMs = 100,
            Success = true
        };

        var log3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "third",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(log1, log2, log3);
        await _context.SaveChangesAsync();

        // Act
        var (items, _) = await _repository.GetFilteredLogsAsync();

        // Assert
        items.Should().HaveCount(3);
        items[0].CommandName.Should().Be("third"); // Most recent first
        items[1].CommandName.Should().Be("second");
        items[2].CommandName.Should().Be("first");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            var log = new CommandLog
            {
                Id = Guid.NewGuid(),
                GuildId = 123456789,
                UserId = 987654321,
                CommandName = $"cmd{i:D2}",
                ExecutedAt = now.AddMinutes(-i), // Creates ordered logs
                ResponseTimeMs = 100,
                Success = true
            };
            await _context.CommandLogs.AddAsync(log);
        }
        await _context.SaveChangesAsync();

        // Act - Get page 2 with page size 5
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(page: 2, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(15);
        // Should skip first 5, so cmd05-cmd09
        items[0].CommandName.Should().Be("cmd05");
        items[4].CommandName.Should().Be("cmd09");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_IncludesUserAndGuildRelations()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);

        // Act
        var (items, _) = await _repository.GetFilteredLogsAsync();

        // Assert
        items.Should().HaveCount(1);
        items[0].User.Should().NotBeNull();
        items[0].User!.Username.Should().Be("TestUser");
        items[0].Guild.Should().NotBeNull();
        items[0].Guild!.Name.Should().Be("Test Guild");
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithEmptySearchTerm_ReturnsAllLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "");

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFilteredLogsAsync_WithWhitespaceSearchTerm_ReturnsAllLogs()
    {
        // Arrange
        await SeedTestDataAsync();

        await _repository.LogCommandAsync(123456789, 987654321, "ping", null, 100, true);
        await _repository.LogCommandAsync(123456789, 987654321, "test", null, 100, true);

        // Act
        var (items, totalCount) = await _repository.GetFilteredLogsAsync(searchTerm: "   ");

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    #endregion

    #region Metrics Helper Methods Tests

    [Fact]
    public async Task GetUniqueUserCountAsync_ReturnsCorrectCount()
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

        var user3 = new User
        {
            Id = 222222222,
            Username = "User3",
            Discriminator = "9999",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Users.AddRangeAsync(user2, user3);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        // Add logs for different users with different timestamps
        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        var recentLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "recent1",
            ExecutedAt = now.AddDays(-2),
            ResponseTimeMs = 100,
            Success = true
        };

        var recentLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 111111111,
            CommandName = "recent2",
            ExecutedAt = now.AddDays(-3),
            ResponseTimeMs = 100,
            Success = true
        };

        var recentLog3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 222222222,
            CommandName = "recent3",
            ExecutedAt = now.AddDays(-1),
            ResponseTimeMs = 100,
            Success = true
        };

        // Same user as recentLog1 - should not increase unique count
        var recentLog4 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "recent4",
            ExecutedAt = now.AddHours(-1),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(oldLog, recentLog1, recentLog2, recentLog3, recentLog4);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetUniqueUserCountAsync(sevenDaysAgo);

        // Assert
        count.Should().Be(3, "there should be 3 unique users with commands in the last 7 days");
    }

    [Fact]
    public async Task GetUniqueUserCountAsync_WithNoRecentUsers_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-30),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddAsync(oldLog);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetUniqueUserCountAsync(now.AddDays(-7));

        // Assert
        count.Should().Be(0, "there should be no users with commands in the last 7 days");
    }

    [Fact]
    public async Task GetActiveGuildCountAsync_ReturnsCorrectCount()
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

        var guild3 = new Guild
        {
            Id = 222222222,
            Name = "Guild 3",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Guilds.AddRangeAsync(guild2, guild3);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        // Add logs for different guilds
        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-2),
            ResponseTimeMs = 100,
            Success = true
        };

        var todayLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "today1",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        var todayLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 111111111,
            UserId = 987654321,
            CommandName = "today2",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        // Same guild as todayLog1 - should not increase count
        var todayLog3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "today3",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        // DM command (null guild) - should not be counted
        var dmLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            UserId = 987654321,
            CommandName = "dm",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(oldLog, todayLog1, todayLog2, todayLog3, dmLog);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetActiveGuildCountAsync(startOfToday);

        // Assert
        count.Should().Be(2, "there should be 2 guilds with command activity today");
    }

    [Fact]
    public async Task GetActiveGuildCountAsync_WithNoRecentGuilds_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddAsync(oldLog);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetActiveGuildCountAsync(now.Date);

        // Assert
        count.Should().Be(0, "there should be no guilds with command activity today");
    }

    [Fact]
    public async Task GetActiveGuildCountAsync_ExcludesDmCommands()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;

        // DM commands only
        var dmLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            UserId = 987654321,
            CommandName = "dm1",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        var dmLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            UserId = 987654321,
            CommandName = "dm2",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(dmLog1, dmLog2);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetActiveGuildCountAsync(now.Date);

        // Assert
        count.Should().Be(0, "DM commands should not be counted as guild activity");
    }

    [Fact]
    public async Task GetCommandCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        // Add commands from different times
        var oldLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old1",
            ExecutedAt = now.AddDays(-2),
            ResponseTimeMs = 100,
            Success = true
        };

        var oldLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old2",
            ExecutedAt = now.AddDays(-1),
            ResponseTimeMs = 100,
            Success = true
        };

        var todayLog1 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "today1",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        var todayLog2 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "today2",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        var todayLog3 = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "today3",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddRangeAsync(oldLog1, oldLog2, todayLog1, todayLog2, todayLog3);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCommandCountAsync(startOfToday);

        // Assert
        count.Should().Be(3, "there should be 3 commands executed today");
    }

    [Fact]
    public async Task GetCommandCountAsync_WithNoRecentCommands_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var oldLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "old",
            ExecutedAt = now.AddDays(-10),
            ResponseTimeMs = 100,
            Success = true
        };

        await _context.CommandLogs.AddAsync(oldLog);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCommandCountAsync(now.Date);

        // Assert
        count.Should().Be(0, "there should be no commands executed today");
    }

    [Fact]
    public async Task GetCommandCountAsync_IncludesBothSuccessAndFailure()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;

        var successLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "success",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = true
        };

        var failureLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            UserId = 987654321,
            CommandName = "failure",
            ExecutedAt = now,
            ResponseTimeMs = 100,
            Success = false,
            ErrorMessage = "Test error"
        };

        await _context.CommandLogs.AddRangeAsync(successLog, failureLog);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCommandCountAsync(now.Date);

        // Assert
        count.Should().Be(2, "both successful and failed commands should be counted");
    }

    #endregion
}
