using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// "Delete all" on the notification list must remove what the list shows for its filters, and
/// nothing else: another user's notifications, ones outside the filters, or ones already dismissed.
/// </summary>
public class NotificationRepositoryDeleteMatchingTests : IDisposable
{
    private const string UserId = "user-a";
    private const string OtherUserId = "user-b";
    private const ulong GuildId = 123456789012345678UL;

    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly NotificationRepository _repository;

    public NotificationRepositoryDeleteMatchingTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();
        _repository = new NotificationRepository(
            _context,
            NullLogger<NotificationRepository>.Instance,
            NullLogger<Repository<UserNotification>>.Instance);

        foreach (var id in new[] { UserId, OtherUserId })
        {
            _context.Set<ApplicationUser>().Add(new ApplicationUser
            {
                Id = id,
                UserName = $"{id}@example.com",
                NormalizedUserName = $"{id}@EXAMPLE.COM",
                Email = $"{id}@example.com",
                IsActive = true
            });
        }

        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test Server", JoinedAt = DateTime.UtcNow, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    private async Task<UserNotification> AddAsync(
        string userId = UserId,
        NotificationType type = NotificationType.PerformanceAlert,
        AlertSeverity? severity = null,
        bool isRead = false,
        DateTime? createdAt = null,
        ulong? guildId = null,
        string title = "Title",
        DateTime? dismissedAt = null)
    {
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = "Message",
            IsRead = isRead,
            GuildId = guildId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            DismissedAt = dismissedAt
        };
        _context.Set<UserNotification>().Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    private async Task<List<Guid>> RemainingIdsAsync() =>
        await _context.Set<UserNotification>().AsNoTracking().Select(n => n.Id).ToListAsync();

    [Fact]
    public async Task DeleteMatchingAsync_ReadFilter_DeletesOnlyReadNotifications()
    {
        var read = await AddAsync(isRead: true);
        var unread = await AddAsync(isRead: false);

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto { IsRead = true });

        deleted.Should().Be(1);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { unread.Id });
        read.Id.Should().NotBe(unread.Id);
    }

    [Fact]
    public async Task DeleteMatchingAsync_DateRangeAndType_AppliesEveryFilter()
    {
        var now = DateTime.UtcNow;
        var inRange = await AddAsync(type: NotificationType.BotStatus, createdAt: now.AddDays(-2));
        var wrongType = await AddAsync(type: NotificationType.GuildEvent, createdAt: now.AddDays(-2));
        var tooOld = await AddAsync(type: NotificationType.BotStatus, createdAt: now.AddDays(-30));

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto
        {
            Type = NotificationType.BotStatus,
            StartDate = now.AddDays(-7),
            EndDate = now
        });

        deleted.Should().Be(1);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { wrongType.Id, tooOld.Id });
        inRange.Id.Should().NotBe(wrongType.Id);
    }

    [Fact]
    public async Task DeleteMatchingAsync_GuildAndSearchTerm_MatchTheListFilters()
    {
        var match = await AddAsync(guildId: GuildId, title: "Memory Usage Alert");
        var otherTitle = await AddAsync(guildId: GuildId, title: "Gateway Latency Alert");
        var noGuild = await AddAsync(guildId: null, title: "Memory Usage Alert");

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto
        {
            GuildId = GuildId,
            SearchTerm = "memory"
        });

        deleted.Should().Be(1);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { otherTitle.Id, noGuild.Id });
        match.Id.Should().NotBe(otherTitle.Id);
    }

    [Fact]
    public async Task DeleteMatchingAsync_NeverTouchesAnotherUsersNotifications()
    {
        await AddAsync(userId: UserId, isRead: true);
        var theirs = await AddAsync(userId: OtherUserId, isRead: true);

        await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto { IsRead = true });

        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { theirs.Id });
    }

    [Fact]
    public async Task DeleteMatchingAsync_LeavesDismissedNotificationsAlone()
    {
        var dismissed = await AddAsync(isRead: true, dismissedAt: DateTime.UtcNow);
        await AddAsync(isRead: true);

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto { IsRead = true });

        deleted.Should().Be(1);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { dismissed.Id });
    }

    [Fact]
    public async Task DeleteMatchingAsync_NoFilters_DeletesEveryVisibleNotificationOfTheUser()
    {
        await AddAsync();
        await AddAsync(isRead: true);
        var theirs = await AddAsync(userId: OtherUserId);

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto());

        deleted.Should().Be(2);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { theirs.Id });
    }

    [Fact]
    public async Task DeleteMatchingAsync_Before_LeavesNotificationsCreatedAfterTheBoundAlone()
    {
        var rendered = DateTime.UtcNow.AddMinutes(-5);
        var shown = await AddAsync(createdAt: rendered.AddMinutes(-10));
        var onTheBound = await AddAsync(createdAt: rendered);
        var arrivedLater = await AddAsync(createdAt: rendered.AddMinutes(1));

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto { Before = rendered });

        deleted.Should().Be(2);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { arrivedLater.Id });
        shown.Id.Should().NotBe(onTheBound.Id);
    }

    [Fact]
    public async Task DeleteMatchingAsync_BeforeCombinedWithAFilter_AppliesBoth()
    {
        var rendered = DateTime.UtcNow.AddMinutes(-5);
        var readBefore = await AddAsync(isRead: true, createdAt: rendered.AddMinutes(-1));
        var unreadBefore = await AddAsync(isRead: false, createdAt: rendered.AddMinutes(-1));
        var readAfter = await AddAsync(isRead: true, createdAt: rendered.AddMinutes(1));

        var deleted = await _repository.DeleteMatchingAsync(UserId, new NotificationQueryDto { IsRead = true, Before = rendered });

        deleted.Should().Be(1);
        (await RemainingIdsAsync()).Should().BeEquivalentTo(new[] { unreadBefore.Id, readAfter.Id });
        readBefore.Id.Should().NotBe(readAfter.Id);
    }
}
