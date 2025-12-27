using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for ScheduledMessageRepository.
/// </summary>
public class ScheduledMessageRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ScheduledMessageRepository _repository;
    private readonly Mock<ILogger<ScheduledMessageRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<ScheduledMessage>>> _mockBaseLogger;

    public ScheduledMessageRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<ScheduledMessageRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<ScheduledMessage>>>();
        _repository = new ScheduledMessageRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    #region Helper Methods

    private async Task<Guild> CreateTestGuildAsync(ulong guildId = 123456789)
    {
        var guild = new Guild
        {
            Id = guildId,
            Name = $"Test Guild {guildId}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();
        return guild;
    }

    private ScheduledMessage CreateScheduledMessage(
        ulong guildId = 123456789,
        ulong channelId = 987654321,
        string title = "Test Message",
        string content = "Test Content",
        ScheduleFrequency frequency = ScheduleFrequency.Daily,
        bool isEnabled = true,
        DateTime? nextExecutionAt = null,
        DateTime? lastExecutedAt = null)
    {
        return new ScheduledMessage
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            Title = title,
            Content = content,
            Frequency = frequency,
            IsEnabled = isEnabled,
            NextExecutionAt = nextExecutionAt,
            LastExecutedAt = lastExecutedAt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user",
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region CRUD Operations (Inherited from Repository<T>)

    [Fact]
    public async Task AddAsync_CreatesScheduledMessage()
    {
        // Arrange
        await CreateTestGuildAsync();
        var message = CreateScheduledMessage();

        // Act
        var result = await _repository.AddAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.GuildId.Should().Be(123456789);
        result.Title.Should().Be("Test Message");

        // Verify it was saved to the database
        var savedMessage = await _context.ScheduledMessages.FindAsync(result.Id);
        savedMessage.Should().NotBeNull();
        savedMessage!.Title.Should().Be("Test Message");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingMessage_ReturnsScheduledMessage()
    {
        // Arrange
        await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(message.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(message.Id);
        result.Title.Should().Be("Test Message");
        result.Content.Should().Be("Test Content");
    }

    [Fact]
    public async Task GetByIdAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(message.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Guild.Should().NotBeNull();
        result.Guild!.Id.Should().Be(guild.Id);
        result.Guild.Name.Should().Be(guild.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentMessage_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidIdType_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(123); // Invalid type - should be Guid

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesScheduledMessage()
    {
        // Arrange
        await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Detach the entity to simulate a fresh update
        _context.Entry(message).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // Modify the message
        message.Title = "Updated Title";
        message.Content = "Updated Content";
        message.IsEnabled = false;

        // Act
        await _repository.UpdateAsync(message);

        // Assert
        // Reload the message from database to verify changes
        var savedMessage = await _context.ScheduledMessages.FindAsync(message.Id);
        savedMessage.Should().NotBeNull();
        savedMessage!.Title.Should().Be("Updated Title");
        savedMessage.Content.Should().Be("Updated Content");
        savedMessage.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesScheduledMessage()
    {
        // Arrange
        await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(message);

        // Assert
        var deletedMessage = await _context.ScheduledMessages.FindAsync(message.Id);
        deletedMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingMessage_ReturnsTrue()
    {
        // Arrange
        await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(m => m.Id == message.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentMessage_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.ExistsAsync(m => m.Id == nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetByGuildIdAsync Tests

    [Fact]
    public async Task GetByGuildIdAsync_ReturnsScheduledMessagesForGuild()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var message1 = CreateScheduledMessage(guildId: 123456789, title: "Message 1");
        var message2 = CreateScheduledMessage(guildId: 123456789, title: "Message 2");
        var message3 = CreateScheduledMessage(guildId: 111111111, title: "Message 3");

        await _context.ScheduledMessages.AddRangeAsync(message1, message2, message3);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(m => m.GuildId.Should().Be(123456789));
        items.Should().Contain(m => m.Title == "Message 1");
        items.Should().Contain(m => m.Title == "Message 2");
        items.Should().NotContain(m => m.Title == "Message 3");
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithNoMessages_ReturnsEmpty()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetByGuildIdAsync_SupportsPagination_FirstPage()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Create 10 messages with different timestamps to ensure consistent ordering
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            var message = CreateScheduledMessage(title: $"Message {i:D2}");
            message.CreatedAt = now.AddMinutes(-i); // Older messages have earlier CreatedAt
            await _context.ScheduledMessages.AddAsync(message);
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(10);
        // Should return the 5 most recent messages (Message 00 to Message 04)
        items.First().Title.Should().Be("Message 00");
        items.Last().Title.Should().Be("Message 04");
    }

    [Fact]
    public async Task GetByGuildIdAsync_SupportsPagination_SecondPage()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            var message = CreateScheduledMessage(title: $"Message {i:D2}");
            message.CreatedAt = now.AddMinutes(-i);
            await _context.ScheduledMessages.AddAsync(message);
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 2, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(10);
        // Should return the next 5 messages (Message 05 to Message 09)
        items.First().Title.Should().Be("Message 05");
        items.Last().Title.Should().Be("Message 09");
    }

    [Fact]
    public async Task GetByGuildIdAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var message1 = CreateScheduledMessage(title: "Oldest");
        message1.CreatedAt = now.AddDays(-3);

        var message2 = CreateScheduledMessage(title: "Middle");
        message2.CreatedAt = now.AddDays(-2);

        var message3 = CreateScheduledMessage(title: "Newest");
        message3.CreatedAt = now.AddDays(-1);

        await _context.ScheduledMessages.AddRangeAsync(message1, message2, message3);
        await _context.SaveChangesAsync();

        // Act
        var (items, _) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        var itemsList = items.ToList();
        itemsList.Should().HaveCount(3);
        itemsList[0].Title.Should().Be("Newest");
        itemsList[1].Title.Should().Be("Middle");
        itemsList[2].Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetByGuildIdAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var (items, _) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        var itemsList = items.ToList();
        itemsList.Should().HaveCount(1);
        itemsList[0].Guild.Should().NotBeNull();
        itemsList[0].Guild!.Name.Should().Be(guild.Name);
    }

    #endregion

    #region GetDueMessagesAsync Tests

    [Fact]
    public async Task GetDueMessagesAsync_ReturnsDueEnabledMessages()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var dueMessage = CreateScheduledMessage(
            title: "Due Message",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-5)); // Past time - should be due

        await _context.ScheduledMessages.AddAsync(dueMessage);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Title.Should().Be("Due Message");
    }

    [Fact]
    public async Task GetDueMessagesAsync_DoesNotReturnDisabledMessages()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var disabledMessage = CreateScheduledMessage(
            title: "Disabled Message",
            isEnabled: false,
            nextExecutionAt: now.AddMinutes(-5)); // Past time but disabled

        await _context.ScheduledMessages.AddAsync(disabledMessage);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueMessagesAsync_DoesNotReturnMessagesWithNullNextExecutionAt()
    {
        // Arrange
        await CreateTestGuildAsync();

        var messageWithNullExecution = CreateScheduledMessage(
            title: "No Execution Time",
            isEnabled: true,
            nextExecutionAt: null); // No execution time set

        await _context.ScheduledMessages.AddAsync(messageWithNullExecution);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueMessagesAsync_DoesNotReturnFutureMessages()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var futureMessage = CreateScheduledMessage(
            title: "Future Message",
            isEnabled: true,
            nextExecutionAt: now.AddHours(1)); // Future time - not due yet

        await _context.ScheduledMessages.AddAsync(futureMessage);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueMessagesAsync_OrdersByNextExecutionAtAscending()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var message1 = CreateScheduledMessage(
            title: "Third",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-5));

        var message2 = CreateScheduledMessage(
            title: "First",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-30));

        var message3 = CreateScheduledMessage(
            title: "Second",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-10));

        await _context.ScheduledMessages.AddRangeAsync(message1, message2, message3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].Title.Should().Be("First");  // Oldest NextExecutionAt
        resultList[1].Title.Should().Be("Second");
        resultList[2].Title.Should().Be("Third");  // Most recent NextExecutionAt
    }

    [Fact]
    public async Task GetDueMessagesAsync_ReturnsMultipleDueMessagesFromDifferentGuilds()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var now = DateTime.UtcNow;
        var message1 = CreateScheduledMessage(
            guildId: 123456789,
            title: "Guild 1 Message",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-5));

        var message2 = CreateScheduledMessage(
            guildId: 111111111,
            title: "Guild 2 Message",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-3));

        await _context.ScheduledMessages.AddRangeAsync(message1, message2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(m => m.Title == "Guild 1 Message");
        resultList.Should().Contain(m => m.Title == "Guild 2 Message");
    }

    [Fact]
    public async Task GetDueMessagesAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var message = CreateScheduledMessage(
            title: "Due Message",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-5));

        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Guild.Should().NotBeNull();
        resultList[0].Guild!.Name.Should().Be(guild.Name);
    }

    [Fact]
    public async Task GetDueMessagesAsync_WithMixedConditions_ReturnsOnlyValidMessages()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;

        // This should be returned - enabled and due
        var validMessage = CreateScheduledMessage(
            title: "Valid",
            isEnabled: true,
            nextExecutionAt: now.AddMinutes(-5));

        // Should NOT be returned - disabled
        var disabledMessage = CreateScheduledMessage(
            title: "Disabled",
            isEnabled: false,
            nextExecutionAt: now.AddMinutes(-5));

        // Should NOT be returned - future execution
        var futureMessage = CreateScheduledMessage(
            title: "Future",
            isEnabled: true,
            nextExecutionAt: now.AddHours(1));

        // Should NOT be returned - null execution time
        var nullMessage = CreateScheduledMessage(
            title: "Null",
            isEnabled: true,
            nextExecutionAt: null);

        await _context.ScheduledMessages.AddRangeAsync(
            validMessage, disabledMessage, futureMessage, nullMessage);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Title.Should().Be("Valid");
    }

    #endregion

    #region GetByIdWithGuildAsync Tests

    [Fact]
    public async Task GetByIdWithGuildAsync_WithExistingMessage_ReturnsMessageWithGuild()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();
        var message = CreateScheduledMessage();
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdWithGuildAsync(message.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(message.Id);
        result.Title.Should().Be("Test Message");
        result.Guild.Should().NotBeNull();
        result.Guild!.Id.Should().Be(guild.Id);
        result.Guild.Name.Should().Be(guild.Name);
    }

    [Fact]
    public async Task GetByIdWithGuildAsync_WithNonExistentMessage_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdWithGuildAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithGuildAsync_WithMultipleMessages_ReturnsCorrectMessage()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var message1 = CreateScheduledMessage(guildId: 123456789, title: "Message 1");
        var message2 = CreateScheduledMessage(guildId: 111111111, title: "Message 2");

        await _context.ScheduledMessages.AddRangeAsync(message1, message2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdWithGuildAsync(message2.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(message2.Id);
        result.Title.Should().Be("Message 2");
        result.Guild.Should().NotBeNull();
        result.Guild!.Id.Should().Be(111111111);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public async Task AddAsync_WithAllPropertiesSet_CreatesCompleteScheduledMessage()
    {
        // Arrange
        await CreateTestGuildAsync();

        var message = new ScheduledMessage
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            ChannelId = 987654321,
            Title = "Complete Message",
            Content = "Complete Content",
            CronExpression = "0 0 * * *",
            Frequency = ScheduleFrequency.Custom,
            IsEnabled = true,
            LastExecutedAt = DateTime.UtcNow.AddDays(-1),
            NextExecutionAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin-user",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Complete Message");
        result.CronExpression.Should().Be("0 0 * * *");
        result.Frequency.Should().Be(ScheduleFrequency.Custom);
        result.LastExecutedAt.Should().NotBeNull();
        result.NextExecutionAt.Should().NotBeNull();
        result.CreatedBy.Should().Be("admin-user");
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithDifferentFrequencies_ReturnsAllMessages()
    {
        // Arrange
        await CreateTestGuildAsync();

        var hourlyMessage = CreateScheduledMessage(title: "Hourly", frequency: ScheduleFrequency.Hourly);
        var dailyMessage = CreateScheduledMessage(title: "Daily", frequency: ScheduleFrequency.Daily);
        var weeklyMessage = CreateScheduledMessage(title: "Weekly", frequency: ScheduleFrequency.Weekly);
        var monthlyMessage = CreateScheduledMessage(title: "Monthly", frequency: ScheduleFrequency.Monthly);
        var customMessage = CreateScheduledMessage(title: "Custom", frequency: ScheduleFrequency.Custom);

        await _context.ScheduledMessages.AddRangeAsync(
            hourlyMessage, dailyMessage, weeklyMessage, monthlyMessage, customMessage);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(5);
        items.Should().Contain(m => m.Frequency == ScheduleFrequency.Hourly);
        items.Should().Contain(m => m.Frequency == ScheduleFrequency.Daily);
        items.Should().Contain(m => m.Frequency == ScheduleFrequency.Weekly);
        items.Should().Contain(m => m.Frequency == ScheduleFrequency.Monthly);
        items.Should().Contain(m => m.Frequency == ScheduleFrequency.Custom);
    }

    [Fact]
    public async Task GetDueMessagesAsync_WithExactCurrentTime_ReturnsMessage()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Use a fixed time that's in the past to avoid timing issues
        var executionTime = DateTime.UtcNow.AddSeconds(-1);
        var message = CreateScheduledMessage(
            title: "Exact Time",
            isEnabled: true,
            nextExecutionAt: executionTime);

        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueMessagesAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Exact Time");
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithEmptyGuild_ReturnsEmptyWithZeroCount()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        // Add messages only to guild 111111111
        var message = CreateScheduledMessage(guildId: 111111111);
        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act - Query empty guild
        var (items, totalCount) = await _repository.GetByGuildIdAsync(123456789, page: 1, pageSize: 10);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WithExecutionTimes_UpdatesCorrectly()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var message = CreateScheduledMessage(
            nextExecutionAt: now.AddHours(1),
            lastExecutedAt: null);

        await _context.ScheduledMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Detach and update
        _context.Entry(message).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        message.LastExecutedAt = now;
        message.NextExecutionAt = now.AddDays(1);

        // Act
        await _repository.UpdateAsync(message);

        // Assert - Verify persistence
        var savedMessage = await _context.ScheduledMessages.FindAsync(message.Id);
        savedMessage.Should().NotBeNull();
        savedMessage!.LastExecutedAt.Should().NotBeNull();
        savedMessage.LastExecutedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        savedMessage.NextExecutionAt.Should().NotBeNull();
        savedMessage.NextExecutionAt.Should().BeCloseTo(now.AddDays(1), TimeSpan.FromSeconds(1));
    }

    #endregion
}
