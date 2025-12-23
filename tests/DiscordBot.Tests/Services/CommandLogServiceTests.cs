using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandLogService"/>.
/// </summary>
public class CommandLogServiceTests
{
    private readonly Mock<ICommandLogRepository> _mockCommandLogRepository;
    private readonly Mock<ILogger<CommandLogService>> _mockLogger;
    private readonly CommandLogService _service;

    public CommandLogServiceTests()
    {
        _mockCommandLogRepository = new Mock<ICommandLogRepository>();
        _mockLogger = new Mock<ILogger<CommandLogService>>();
        _service = new CommandLogService(_mockCommandLogRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldReturnPaginatedResults()
    {
        // Arrange
        var logs = CreateTestCommandLogs(10);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            Page = 1,
            PageSize = 5
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5, "page size is 5");
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(5);
        result.TotalCount.Should().Be(10, "there are 10 total logs");
        result.Items.Should().BeInDescendingOrder(l => l.ExecutedAt, "logs should be ordered by ExecutedAt descending");
    }

    [Fact]
    public async Task GetLogsAsync_WithSecondPage_ShouldReturnNextPageOfResults()
    {
        // Arrange
        var logs = CreateTestCommandLogs(15);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            Page = 2,
            PageSize = 5
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5, "page size is 5");
        result.Page.Should().Be(2);
        result.TotalCount.Should().Be(15, "there are 15 total logs");
    }

    [Fact]
    public async Task GetLogsAsync_WithGuildIdFilter_ShouldFilterByGuild()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, guildId: 111111111UL),
            CreateCommandLog(2, guildId: 111111111UL),
            CreateCommandLog(3, guildId: 222222222UL),
            CreateCommandLog(4, guildId: 111111111UL),
            CreateCommandLog(5, guildId: 333333333UL)
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            GuildId = 111111111UL,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 logs for guild 111111111");
        result.Items.Should().AllSatisfy(l => l.GuildId.Should().Be(111111111UL));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithUserIdFilter_ShouldFilterByUser()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, userId: 987654321UL),
            CreateCommandLog(2, userId: 123456789UL),
            CreateCommandLog(3, userId: 987654321UL),
            CreateCommandLog(4, userId: 987654321UL),
            CreateCommandLog(5, userId: 555555555UL)
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            UserId = 987654321UL,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 logs for user 987654321");
        result.Items.Should().AllSatisfy(l => l.UserId.Should().Be(987654321UL));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithCommandNameFilter_ShouldFilterByCommandName()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, commandName: "ping"),
            CreateCommandLog(2, commandName: "status"),
            CreateCommandLog(3, commandName: "ping"),
            CreateCommandLog(4, commandName: "help"),
            CreateCommandLog(5, commandName: "PING") // Should match case-insensitively
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            CommandName = "ping",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 logs for command 'ping' (case-insensitive)");
        result.Items.Should().AllSatisfy(l => l.CommandName.ToLower().Should().Be("ping"));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithStartDateFilter_ShouldFilterByStartDate()
    {
        // Arrange
        var baseDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, executedAt: baseDate.AddDays(-5)),
            CreateCommandLog(2, executedAt: baseDate.AddDays(-1)),
            CreateCommandLog(3, executedAt: baseDate),
            CreateCommandLog(4, executedAt: baseDate.AddDays(1)),
            CreateCommandLog(5, executedAt: baseDate.AddDays(5))
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            StartDate = baseDate,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 logs on or after the start date");
        result.Items.Should().AllSatisfy(l => l.ExecutedAt.Should().BeOnOrAfter(baseDate));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithEndDateFilter_ShouldFilterByEndDate()
    {
        // Arrange
        var baseDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, executedAt: baseDate.AddDays(-5)),
            CreateCommandLog(2, executedAt: baseDate.AddDays(-1)),
            CreateCommandLog(3, executedAt: baseDate),
            CreateCommandLog(4, executedAt: baseDate.AddDays(1)),
            CreateCommandLog(5, executedAt: baseDate.AddDays(5))
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            EndDate = baseDate,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 logs on or before the end date");
        result.Items.Should().AllSatisfy(l => l.ExecutedAt.Should().BeOnOrBefore(baseDate));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithSuccessOnlyFilter_ShouldFilterBySuccess()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, success: true),
            CreateCommandLog(2, success: false),
            CreateCommandLog(3, success: true),
            CreateCommandLog(4, success: true),
            CreateCommandLog(5, success: false)
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SuccessOnly = true,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "there are 3 successful logs");
        result.Items.Should().AllSatisfy(l => l.Success.Should().BeTrue());
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithMultipleFilters_ShouldApplyAllFilters()
    {
        // Arrange
        var baseDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, guildId: 111111111UL, userId: 987654321UL, commandName: "ping", executedAt: baseDate, success: true),
            CreateCommandLog(2, guildId: 111111111UL, userId: 987654321UL, commandName: "ping", executedAt: baseDate.AddDays(1), success: false),
            CreateCommandLog(3, guildId: 111111111UL, userId: 987654321UL, commandName: "ping", executedAt: baseDate.AddDays(2), success: true),
            CreateCommandLog(4, guildId: 111111111UL, userId: 123456789UL, commandName: "ping", executedAt: baseDate.AddDays(1), success: true),
            CreateCommandLog(5, guildId: 222222222UL, userId: 987654321UL, commandName: "ping", executedAt: baseDate.AddDays(1), success: true),
            CreateCommandLog(6, guildId: 111111111UL, userId: 987654321UL, commandName: "status", executedAt: baseDate.AddDays(1), success: true)
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            GuildId = 111111111UL,
            UserId = 987654321UL,
            CommandName = "ping",
            SuccessOnly = true,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 logs match all filters");
        result.Items.Should().AllSatisfy(l =>
        {
            l.GuildId.Should().Be(111111111UL);
            l.UserId.Should().Be(987654321UL);
            l.CommandName.ToLower().Should().Be("ping");
            l.Success.Should().BeTrue();
        });
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLogsAsync_WithInvalidPage_ShouldDefaultToPage1()
    {
        // Arrange
        var logs = CreateTestCommandLogs(10);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            Page = 0, // Invalid
            PageSize = 5
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().Be(1, "page should default to 1 when invalid");
    }

    [Fact]
    public async Task GetLogsAsync_WithInvalidPageSize_ShouldDefaultTo50()
    {
        // Arrange
        var logs = CreateTestCommandLogs(10);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var queryTooSmall = new CommandLogQueryDto
        {
            Page = 1,
            PageSize = 0 // Invalid
        };

        // Act
        var result = await _service.GetLogsAsync(queryTooSmall);

        // Assert
        result.Should().NotBeNull();
        result.PageSize.Should().Be(50, "page size should default to 50 when too small");
    }

    [Fact]
    public async Task GetLogsAsync_WithPageSizeOver100_ShouldDefaultTo50()
    {
        // Arrange
        var logs = CreateTestCommandLogs(10);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var queryTooLarge = new CommandLogQueryDto
        {
            Page = 1,
            PageSize = 200 // Too large
        };

        // Act
        var result = await _service.GetLogsAsync(queryTooLarge);

        // Assert
        result.Should().NotBeNull();
        result.PageSize.Should().Be(50, "page size should default to 50 when too large");
    }

    [Fact]
    public async Task GetCommandStatsAsync_ShouldReturnCommandCounts()
    {
        // Arrange
        var expectedStats = new Dictionary<string, int>
        {
            { "ping", 10 },
            { "status", 5 },
            { "help", 3 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetCommandStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 different commands");
        result["ping"].Should().Be(10);
        result["status"].Should().Be(5);
        result["help"].Should().Be(3);
    }

    [Fact]
    public async Task GetCommandStatsAsync_WithSinceDate_ShouldPassDateToRepository()
    {
        // Arrange
        var sinceDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedStats = new Dictionary<string, int> { { "ping", 5 } };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(sinceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetCommandStatsAsync(sinceDate);

        // Assert
        result.Should().NotBeNull();
        _mockCommandLogRepository.Verify(
            r => r.GetCommandUsageStatsAsync(sinceDate, It.IsAny<CancellationToken>()),
            Times.Once,
            "the since date should be passed to the repository");
    }

    [Fact]
    public async Task GetLogsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandLog>());

        var query = new CommandLogQueryDto { Page = 1, PageSize = 50 };

        // Act
        await _service.GetLogsAsync(query, cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the repository");
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermMatchingCommandName_ShouldFilterByCommandName()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, commandName: "ping"),
            CreateCommandLog(2, commandName: "status"),
            CreateCommandLog(3, commandName: "pingpong"),
            CreateCommandLog(4, commandName: "help")
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "ping",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "there are 2 commands containing 'ping'");
        result.Items.Should().AllSatisfy(l => l.CommandName.Should().Contain("ping", "search term should match command name"));
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermMatchingUsername_ShouldFilterByUsername()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, userId: 111UL, commandName: "ping"),
            CreateCommandLog(2, userId: 222UL, commandName: "status"),
            CreateCommandLog(3, userId: 333UL, commandName: "help")
        };

        // Modify usernames directly after creation
        logs[0].User.Username = "Alice";
        logs[1].User.Username = "Bob";
        logs[2].User.Username = "Charlie";

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "Alice",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1, "there is 1 user matching 'Alice'");
        result.Items.First().Username.Should().Be("Alice");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermMatchingGuildName_ShouldFilterByGuildName()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, guildId: 111UL, commandName: "ping"),
            CreateCommandLog(2, guildId: 222UL, commandName: "status"),
            CreateCommandLog(3, guildId: 333UL, commandName: "help")
        };

        // Modify guild names directly after creation
        logs[0].Guild!.Name = "Gaming Guild";
        logs[1].Guild!.Name = "Dev Team";
        logs[2].Guild!.Name = "Gaming Paradise";

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "Gaming",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "there are 2 guilds containing 'Gaming'");
        result.Items.Should().AllSatisfy(l => l.GuildName.Should().Contain("Gaming"));
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermCaseInsensitive_ShouldMatchIgnoringCase()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, commandName: "PING"),
            CreateCommandLog(2, commandName: "ping"),
            CreateCommandLog(3, commandName: "Ping"),
            CreateCommandLog(4, commandName: "status")
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "PiNg",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "search should be case-insensitive");
        result.Items.Should().AllSatisfy(l => l.CommandName.ToLower().Should().Contain("ping"));
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermMatchingMultipleFields_ShouldReturnAllMatches()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, commandName: "test", userId: 111UL),
            CreateCommandLog(2, commandName: "ping", guildId: 222UL),
            CreateCommandLog(3, commandName: "status", userId: 333UL)
        };

        // Set up so search term matches different fields
        logs[0].User.Username = "tester";  // Matches username
        logs[1].Guild!.Name = "Test Guild"; // Matches guild name
        logs[2].User.Username = "admin";    // Doesn't match

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "test",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "search should match across command name, username, and guild name");
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLogsAsync_WithNullSearchTerm_ShouldReturnAllLogs()
    {
        // Arrange
        var logs = CreateTestCommandLogs(5);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = null,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5, "null search term should return all logs");
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetLogsAsync_WithEmptySearchTerm_ShouldReturnAllLogs()
    {
        // Arrange
        var logs = CreateTestCommandLogs(5);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5, "empty search term should return all logs");
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetLogsAsync_WithWhitespaceSearchTerm_ShouldReturnAllLogs()
    {
        // Arrange
        var logs = CreateTestCommandLogs(5);

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "   ",
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5, "whitespace-only search term should return all logs");
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetLogsAsync_WithSearchTermAndOtherFilters_ShouldApplyAllFilters()
    {
        // Arrange
        var logs = new List<CommandLog>
        {
            CreateCommandLog(1, guildId: 111UL, commandName: "ping", success: true),
            CreateCommandLog(2, guildId: 111UL, commandName: "ping", success: false),
            CreateCommandLog(3, guildId: 222UL, commandName: "ping", success: true),
            CreateCommandLog(4, guildId: 111UL, commandName: "status", success: true)
        };

        _mockCommandLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var query = new CommandLogQueryDto
        {
            SearchTerm = "ping",
            GuildId = 111UL,
            SuccessOnly = true,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1, "only 1 log matches all filters (search term, guild ID, and success)");
        result.Items.First().CommandName.Should().Be("ping");
        result.Items.First().GuildId.Should().Be(111UL);
        result.Items.First().Success.Should().BeTrue();
        result.TotalCount.Should().Be(1);
    }

    // Helper methods for creating test data

    private List<CommandLog> CreateTestCommandLogs(int count)
    {
        var logs = new List<CommandLog>();
        var baseDate = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            logs.Add(CreateCommandLog(i, executedAt: baseDate.AddMinutes(-i)));
        }

        return logs;
    }

    private CommandLog CreateCommandLog(
        int id,
        ulong? guildId = null,
        ulong userId = 987654321UL,
        string commandName = "test",
        DateTime? executedAt = null,
        bool success = true)
    {
        var guild = guildId.HasValue ? new Guild
        {
            Id = guildId.Value,
            Name = $"Test Guild {guildId}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        } : null;

        var user = new User
        {
            Id = userId,
            Username = $"TestUser{userId}",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        return new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            Guild = guild,
            UserId = userId,
            User = user,
            CommandName = commandName,
            Parameters = null,
            ExecutedAt = executedAt ?? DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = success,
            ErrorMessage = success ? null : "Test error"
        };
    }
}
