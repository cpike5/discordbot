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
/// Unit tests for ReminderRepository.
/// </summary>
public class ReminderRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ReminderRepository _repository;
    private readonly Mock<ILogger<ReminderRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<Reminder>>> _mockBaseLogger;

    public ReminderRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<ReminderRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<Reminder>>>();
        _repository = new ReminderRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
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

    private Reminder CreateReminder(
        ulong guildId = 123456789,
        ulong channelId = 987654321,
        ulong userId = 111111111,
        string message = "Test Reminder",
        ReminderStatus status = ReminderStatus.Pending,
        DateTime? triggerAt = null,
        DateTime? createdAt = null)
    {
        return new Reminder
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            UserId = userId,
            Message = message,
            TriggerAt = triggerAt ?? DateTime.UtcNow.AddHours(1),
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Status = status,
            DeliveryAttempts = 0
        };
    }

    #endregion

    #region CRUD Operations (Inherited from Repository<T>)

    [Fact]
    public async Task AddAsync_CreatesReminder()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder();

        // Act
        var result = await _repository.AddAsync(reminder);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.GuildId.Should().Be(123456789);
        result.Message.Should().Be("Test Reminder");

        // Verify it was saved to the database
        var savedReminder = await _context.Reminders.FindAsync(result.Id);
        savedReminder.Should().NotBeNull();
        savedReminder!.Message.Should().Be("Test Reminder");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingReminder_ReturnsReminder()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder();
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(reminder.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(reminder.Id);
        result.Message.Should().Be("Test Reminder");
    }

    [Fact]
    public async Task GetByIdAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();
        var reminder = CreateReminder();
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(reminder.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Guild.Should().NotBeNull();
        result.Guild!.Id.Should().Be(guild.Id);
        result.Guild.Name.Should().Be(guild.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentReminder_ReturnsNull()
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
    public async Task UpdateAsync_ModifiesReminder()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder();
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Detach the entity to simulate a fresh update
        _context.Entry(reminder).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // Modify the reminder
        reminder.Message = "Updated Message";
        reminder.Status = ReminderStatus.Delivered;
        reminder.DeliveredAt = DateTime.UtcNow;

        // Act
        await _repository.UpdateAsync(reminder);

        // Assert
        var savedReminder = await _context.Reminders.FindAsync(reminder.Id);
        savedReminder.Should().NotBeNull();
        savedReminder!.Message.Should().Be("Updated Message");
        savedReminder.Status.Should().Be(ReminderStatus.Delivered);
        savedReminder.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesReminder()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder();
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(reminder);

        // Assert
        var deletedReminder = await _context.Reminders.FindAsync(reminder.Id);
        deletedReminder.Should().BeNull();
    }

    #endregion

    #region GetDueRemindersAsync Tests

    [Fact]
    public async Task GetDueRemindersAsync_ReturnsPendingDueReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var dueReminder = CreateReminder(
            message: "Due Reminder",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-5));

        await _context.Reminders.AddAsync(dueReminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Message.Should().Be("Due Reminder");
    }

    [Fact]
    public async Task GetDueRemindersAsync_DoesNotReturnDeliveredReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var deliveredReminder = CreateReminder(
            message: "Delivered Reminder",
            status: ReminderStatus.Delivered,
            triggerAt: now.AddMinutes(-5));

        await _context.Reminders.AddAsync(deliveredReminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueRemindersAsync_DoesNotReturnCancelledReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var cancelledReminder = CreateReminder(
            message: "Cancelled Reminder",
            status: ReminderStatus.Cancelled,
            triggerAt: now.AddMinutes(-5));

        await _context.Reminders.AddAsync(cancelledReminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueRemindersAsync_DoesNotReturnFutureReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var futureReminder = CreateReminder(
            message: "Future Reminder",
            status: ReminderStatus.Pending,
            triggerAt: now.AddHours(1));

        await _context.Reminders.AddAsync(futureReminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDueRemindersAsync_OrdersByTriggerAtAscending()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var reminder1 = CreateReminder(
            message: "Third",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-5));

        var reminder2 = CreateReminder(
            message: "First",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-30));

        var reminder3 = CreateReminder(
            message: "Second",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-10));

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].Message.Should().Be("First");
        resultList[1].Message.Should().Be("Second");
        resultList[2].Message.Should().Be("Third");
    }

    [Fact]
    public async Task GetDueRemindersAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var reminder = CreateReminder(
            message: "Due Reminder",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-5));

        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Guild.Should().NotBeNull();
        resultList[0].Guild!.Name.Should().Be(guild.Name);
    }

    #endregion

    #region GetByUserAsync Tests

    [Fact]
    public async Task GetByUserAsync_ReturnsRemindersForUser()
    {
        // Arrange
        await CreateTestGuildAsync();

        var reminder1 = CreateReminder(userId: 111111111, message: "User 1 Reminder 1");
        var reminder2 = CreateReminder(userId: 111111111, message: "User 1 Reminder 2");
        var reminder3 = CreateReminder(userId: 222222222, message: "User 2 Reminder");

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(111111111, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(r => r.UserId.Should().Be(111111111));
    }

    [Fact]
    public async Task GetByUserAsync_WithPendingOnly_ReturnsOnlyPendingReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pendingReminder = CreateReminder(userId: 111111111, message: "Pending", status: ReminderStatus.Pending);
        var deliveredReminder = CreateReminder(userId: 111111111, message: "Delivered", status: ReminderStatus.Delivered);
        var cancelledReminder = CreateReminder(userId: 111111111, message: "Cancelled", status: ReminderStatus.Cancelled);

        await _context.Reminders.AddRangeAsync(pendingReminder, deliveredReminder, cancelledReminder);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(111111111, page: 1, pageSize: 10, pendingOnly: true);

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items.First().Message.Should().Be("Pending");
    }

    [Fact]
    public async Task GetByUserAsync_WithPendingOnlyFalse_ReturnsAllReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pendingReminder = CreateReminder(userId: 111111111, message: "Pending", status: ReminderStatus.Pending);
        var deliveredReminder = CreateReminder(userId: 111111111, message: "Delivered", status: ReminderStatus.Delivered);

        await _context.Reminders.AddRangeAsync(pendingReminder, deliveredReminder);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(111111111, page: 1, pageSize: 10, pendingOnly: false);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserAsync_SupportsPagination()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            var reminder = CreateReminder(
                userId: 111111111,
                message: $"Reminder {i:D2}",
                triggerAt: now.AddMinutes(i)); // Earlier trigger time = earlier in order
            await _context.Reminders.AddAsync(reminder);
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(111111111, page: 2, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(10);
    }

    [Fact]
    public async Task GetByUserAsync_OrdersByTriggerAtAscending()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var reminder1 = CreateReminder(userId: 111111111, message: "Later", triggerAt: now.AddHours(3));
        var reminder2 = CreateReminder(userId: 111111111, message: "Earliest", triggerAt: now.AddHours(1));
        var reminder3 = CreateReminder(userId: 111111111, message: "Middle", triggerAt: now.AddHours(2));

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3);
        await _context.SaveChangesAsync();

        // Act
        var (items, _) = await _repository.GetByUserAsync(111111111, page: 1, pageSize: 10);

        // Assert
        var itemsList = items.ToList();
        itemsList[0].Message.Should().Be("Earliest");
        itemsList[1].Message.Should().Be("Middle");
        itemsList[2].Message.Should().Be("Later");
    }

    #endregion

    #region GetPendingCountByUserAsync Tests

    [Fact]
    public async Task GetPendingCountByUserAsync_ReturnsCorrectCount()
    {
        // Arrange
        await CreateTestGuildAsync();

        var reminder1 = CreateReminder(userId: 111111111, status: ReminderStatus.Pending);
        var reminder2 = CreateReminder(userId: 111111111, status: ReminderStatus.Pending);
        var reminder3 = CreateReminder(userId: 111111111, status: ReminderStatus.Delivered);
        var reminder4 = CreateReminder(userId: 222222222, status: ReminderStatus.Pending);

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3, reminder4);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetPendingCountByUserAsync(111111111);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingCountByUserAsync_WithNoReminders_ReturnsZero()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Act
        var count = await _repository.GetPendingCountByUserAsync(111111111);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingCountByUserAsync_ExcludesNonPendingStatuses()
    {
        // Arrange
        await CreateTestGuildAsync();

        var delivered = CreateReminder(userId: 111111111, status: ReminderStatus.Delivered);
        var failed = CreateReminder(userId: 111111111, status: ReminderStatus.Failed);
        var cancelled = CreateReminder(userId: 111111111, status: ReminderStatus.Cancelled);

        await _context.Reminders.AddRangeAsync(delivered, failed, cancelled);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetPendingCountByUserAsync(111111111);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetByGuildAsync Tests

    [Fact]
    public async Task GetByGuildAsync_ReturnsRemindersForGuild()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var reminder1 = CreateReminder(guildId: 123456789, message: "Guild 1 Reminder 1");
        var reminder2 = CreateReminder(guildId: 123456789, message: "Guild 1 Reminder 2");
        var reminder3 = CreateReminder(guildId: 111111111, message: "Guild 2 Reminder");

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildAsync(123456789, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(r => r.GuildId.Should().Be(123456789));
    }

    [Fact]
    public async Task GetByGuildAsync_WithStatusFilter_ReturnsFilteredReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pendingReminder = CreateReminder(message: "Pending", status: ReminderStatus.Pending);
        var deliveredReminder = CreateReminder(message: "Delivered", status: ReminderStatus.Delivered);
        var failedReminder = CreateReminder(message: "Failed", status: ReminderStatus.Failed);

        await _context.Reminders.AddRangeAsync(pendingReminder, deliveredReminder, failedReminder);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildAsync(
            123456789, page: 1, pageSize: 10, status: ReminderStatus.Pending);

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items.First().Message.Should().Be("Pending");
    }

    [Fact]
    public async Task GetByGuildAsync_WithNullStatusFilter_ReturnsAllReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pendingReminder = CreateReminder(message: "Pending", status: ReminderStatus.Pending);
        var deliveredReminder = CreateReminder(message: "Delivered", status: ReminderStatus.Delivered);

        await _context.Reminders.AddRangeAsync(pendingReminder, deliveredReminder);
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildAsync(
            123456789, page: 1, pageSize: 10, status: null);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByGuildAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var reminder1 = CreateReminder(message: "Oldest", createdAt: now.AddDays(-3));
        var reminder2 = CreateReminder(message: "Middle", createdAt: now.AddDays(-2));
        var reminder3 = CreateReminder(message: "Newest", createdAt: now.AddDays(-1));

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3);
        await _context.SaveChangesAsync();

        // Act
        var (items, _) = await _repository.GetByGuildAsync(123456789, page: 1, pageSize: 10);

        // Assert
        var itemsList = items.ToList();
        itemsList[0].Message.Should().Be("Newest");
        itemsList[1].Message.Should().Be("Middle");
        itemsList[2].Message.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetByGuildAsync_SupportsPagination()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            var reminder = CreateReminder(
                message: $"Reminder {i:D2}",
                createdAt: now.AddMinutes(-i));
            await _context.Reminders.AddAsync(reminder);
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetByGuildAsync(123456789, page: 2, pageSize: 5);

        // Assert
        items.Should().HaveCount(5);
        totalCount.Should().Be(10);
    }

    #endregion

    #region GetByIdForUserAsync Tests

    [Fact]
    public async Task GetByIdForUserAsync_WithMatchingIdAndUser_ReturnsReminder()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder(userId: 111111111, message: "User Reminder");
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdForUserAsync(reminder.Id, 111111111);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(reminder.Id);
        result.Message.Should().Be("User Reminder");
    }

    [Fact]
    public async Task GetByIdForUserAsync_WithNonMatchingUser_ReturnsNull()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder(userId: 111111111, message: "User 1 Reminder");
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdForUserAsync(reminder.Id, 222222222);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdForUserAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Act
        var result = await _repository.GetByIdForUserAsync(Guid.NewGuid(), 111111111);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdForUserAsync_IncludesGuildNavigationProperty()
    {
        // Arrange
        var guild = await CreateTestGuildAsync();
        var reminder = CreateReminder(userId: 111111111);
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdForUserAsync(reminder.Id, 111111111);

        // Assert
        result.Should().NotBeNull();
        result!.Guild.Should().NotBeNull();
        result.Guild!.Name.Should().Be(guild.Name);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public async Task AddAsync_WithAllPropertiesSet_CreatesCompleteReminder()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789,
            ChannelId = 987654321,
            UserId = 111111111,
            Message = "Complete Reminder",
            TriggerAt = now.AddHours(1),
            CreatedAt = now,
            DeliveredAt = null,
            Status = ReminderStatus.Pending,
            DeliveryAttempts = 0,
            LastError = null
        };

        // Act
        var result = await _repository.AddAsync(reminder);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Complete Reminder");
        result.Status.Should().Be(ReminderStatus.Pending);
        result.DeliveryAttempts.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WithDeliveryFailure_UpdatesCorrectly()
    {
        // Arrange
        await CreateTestGuildAsync();
        var reminder = CreateReminder();
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Detach and update
        _context.Entry(reminder).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        reminder.DeliveryAttempts = 3;
        reminder.Status = ReminderStatus.Failed;
        reminder.LastError = "User has DMs disabled";

        // Act
        await _repository.UpdateAsync(reminder);

        // Assert
        var savedReminder = await _context.Reminders.FindAsync(reminder.Id);
        savedReminder.Should().NotBeNull();
        savedReminder!.DeliveryAttempts.Should().Be(3);
        savedReminder.Status.Should().Be(ReminderStatus.Failed);
        savedReminder.LastError.Should().Be("User has DMs disabled");
    }

    [Fact]
    public async Task GetDueRemindersAsync_WithMixedConditions_ReturnsOnlyValidReminders()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;

        // Should be returned - pending and due
        var validReminder = CreateReminder(
            message: "Valid",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-5));

        // Should NOT be returned - delivered
        var deliveredReminder = CreateReminder(
            message: "Delivered",
            status: ReminderStatus.Delivered,
            triggerAt: now.AddMinutes(-5));

        // Should NOT be returned - future
        var futureReminder = CreateReminder(
            message: "Future",
            status: ReminderStatus.Pending,
            triggerAt: now.AddHours(1));

        // Should NOT be returned - cancelled
        var cancelledReminder = CreateReminder(
            message: "Cancelled",
            status: ReminderStatus.Cancelled,
            triggerAt: now.AddMinutes(-5));

        await _context.Reminders.AddRangeAsync(
            validReminder, deliveredReminder, futureReminder, cancelledReminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Message.Should().Be("Valid");
    }

    [Fact]
    public async Task GetDueRemindersAsync_ReturnsMultipleDueRemindersFromDifferentGuilds()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var now = DateTime.UtcNow;
        var reminder1 = CreateReminder(
            guildId: 123456789,
            message: "Guild 1 Reminder",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-5));

        var reminder2 = CreateReminder(
            guildId: 111111111,
            message: "Guild 2 Reminder",
            status: ReminderStatus.Pending,
            triggerAt: now.AddMinutes(-3));

        await _context.Reminders.AddRangeAsync(reminder1, reminder2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDueRemindersAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(r => r.Message == "Guild 1 Reminder");
        resultList.Should().Contain(r => r.Message == "Guild 2 Reminder");
    }

    #endregion

    #region GetGuildStatsAsync Tests

    [Fact]
    public async Task GetGuildStatsAsync_ReturnsCorrectTotalCount()
    {
        // Arrange
        await CreateTestGuildAsync();

        var reminder1 = CreateReminder(status: ReminderStatus.Pending);
        var reminder2 = CreateReminder(status: ReminderStatus.Delivered);
        var reminder3 = CreateReminder(status: ReminderStatus.Failed);
        var reminder4 = CreateReminder(status: ReminderStatus.Cancelled);

        await _context.Reminders.AddRangeAsync(reminder1, reminder2, reminder3, reminder4);
        await _context.SaveChangesAsync();

        // Act
        var (totalCount, _, _, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        totalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetGuildStatsAsync_ReturnsCorrectPendingCount()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pending1 = CreateReminder(status: ReminderStatus.Pending);
        var pending2 = CreateReminder(status: ReminderStatus.Pending);
        var delivered = CreateReminder(status: ReminderStatus.Delivered);
        var failed = CreateReminder(status: ReminderStatus.Failed);

        await _context.Reminders.AddRangeAsync(pending1, pending2, delivered, failed);
        await _context.SaveChangesAsync();

        // Act
        var (_, pendingCount, _, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        pendingCount.Should().Be(2);
    }

    [Fact]
    public async Task GetGuildStatsAsync_ReturnsCorrectFailedCount()
    {
        // Arrange
        await CreateTestGuildAsync();

        var pending = CreateReminder(status: ReminderStatus.Pending);
        var failed1 = CreateReminder(status: ReminderStatus.Failed);
        var failed2 = CreateReminder(status: ReminderStatus.Failed);
        var delivered = CreateReminder(status: ReminderStatus.Delivered);

        await _context.Reminders.AddRangeAsync(pending, failed1, failed2, delivered);
        await _context.SaveChangesAsync();

        // Act
        var (_, _, _, failedCount) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        failedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetGuildStatsAsync_ReturnsCorrectDeliveredTodayCount()
    {
        // Arrange
        await CreateTestGuildAsync();

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Delivered today
        var deliveredToday1 = CreateReminder(status: ReminderStatus.Delivered);
        deliveredToday1.DeliveredAt = today.AddHours(2);

        var deliveredToday2 = CreateReminder(status: ReminderStatus.Delivered);
        deliveredToday2.DeliveredAt = today.AddHours(5);

        // Delivered yesterday (should not count)
        var deliveredYesterday = CreateReminder(status: ReminderStatus.Delivered);
        deliveredYesterday.DeliveredAt = today.AddDays(-1).AddHours(10);

        // Pending (should not count)
        var pending = CreateReminder(status: ReminderStatus.Pending);

        await _context.Reminders.AddRangeAsync(deliveredToday1, deliveredToday2, deliveredYesterday, pending);
        await _context.SaveChangesAsync();

        // Act
        var (_, _, deliveredTodayCount, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        deliveredTodayCount.Should().Be(2);
    }

    [Fact]
    public async Task GetGuildStatsAsync_ExcludesRemindersFromOtherGuilds()
    {
        // Arrange
        await CreateTestGuildAsync(123456789);
        await CreateTestGuildAsync(111111111);

        var guild1Reminder = CreateReminder(guildId: 123456789, status: ReminderStatus.Pending);
        var guild2Reminder1 = CreateReminder(guildId: 111111111, status: ReminderStatus.Pending);
        var guild2Reminder2 = CreateReminder(guildId: 111111111, status: ReminderStatus.Pending);

        await _context.Reminders.AddRangeAsync(guild1Reminder, guild2Reminder1, guild2Reminder2);
        await _context.SaveChangesAsync();

        // Act
        var (totalCount, pendingCount, _, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        totalCount.Should().Be(1);
        pendingCount.Should().Be(1);
    }

    [Fact]
    public async Task GetGuildStatsAsync_WithNoReminders_ReturnsZeros()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Act
        var (totalCount, pendingCount, deliveredTodayCount, failedCount) =
            await _repository.GetGuildStatsAsync(123456789);

        // Assert
        totalCount.Should().Be(0);
        pendingCount.Should().Be(0);
        deliveredTodayCount.Should().Be(0);
        failedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetGuildStatsAsync_DeliveredTodayCountHandlesBoundaryCorrectly()
    {
        // Arrange
        await CreateTestGuildAsync();

        var today = DateTime.UtcNow.Date;

        // At midnight today (should count)
        var atMidnight = CreateReminder(status: ReminderStatus.Delivered);
        atMidnight.DeliveredAt = today;

        // Just before midnight tomorrow (should count)
        var justBeforeTomorrow = CreateReminder(status: ReminderStatus.Delivered);
        justBeforeTomorrow.DeliveredAt = today.AddDays(1).AddSeconds(-1);

        // Exactly at midnight tomorrow (should NOT count)
        var atTomorrow = CreateReminder(status: ReminderStatus.Delivered);
        atTomorrow.DeliveredAt = today.AddDays(1);

        await _context.Reminders.AddRangeAsync(atMidnight, justBeforeTomorrow, atTomorrow);
        await _context.SaveChangesAsync();

        // Act
        var (_, _, deliveredTodayCount, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        deliveredTodayCount.Should().Be(2, "should include reminders delivered today but exclude tomorrow");
    }

    [Fact]
    public async Task GetGuildStatsAsync_DeliveredWithNullDeliveredAt_NotCountedAsDeliveredToday()
    {
        // Arrange
        await CreateTestGuildAsync();

        // Edge case: Delivered status but null DeliveredAt (shouldn't happen, but handle gracefully)
        var deliveredNoDate = CreateReminder(status: ReminderStatus.Delivered);
        deliveredNoDate.DeliveredAt = null;

        await _context.Reminders.AddAsync(deliveredNoDate);
        await _context.SaveChangesAsync();

        // Act
        var (totalCount, _, deliveredTodayCount, _) = await _repository.GetGuildStatsAsync(123456789);

        // Assert
        totalCount.Should().Be(1);
        deliveredTodayCount.Should().Be(0, "null DeliveredAt should not count as delivered today");
    }

    #endregion
}
