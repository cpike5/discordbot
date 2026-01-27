using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Reminder entities with reminder-specific operations.
/// </summary>
public class ReminderRepository : Repository<Reminder>, IReminderRepository
{
    private readonly ILogger<ReminderRepository> _logger;

    public ReminderRepository(
        BotDbContext context,
        ILogger<ReminderRepository> logger,
        ILogger<Repository<Reminder>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<Reminder?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving reminder by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for Reminder: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .FirstOrDefaultAsync(r => r.Id == guidId, cancellationToken);

        _logger.LogDebug("Reminder {Id} found: {Found}", id, result != null);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Reminder>> GetDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _logger.LogDebug("Retrieving reminders due for delivery at {CurrentTime}", now);

        var dueReminders = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Where(r => r.Status == ReminderStatus.Pending && r.TriggerAt <= now)
            .OrderBy(r => r.TriggerAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} reminders due for delivery", dueReminders.Count);
        return dueReminders;
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByUserAsync(
        ulong userId,
        int page,
        int pageSize,
        bool pendingOnly = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving reminders for user {UserId}, page {Page}, pageSize {PageSize}, pendingOnly {PendingOnly}",
            userId, page, pageSize, pendingOnly);

        var query = DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Where(r => r.UserId == userId);

        if (pendingOnly)
        {
            query = query.Where(r => r.Status == ReminderStatus.Pending);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderBy(r => r.TriggerAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} reminders for user {UserId} out of {TotalCount} total",
            items.Count, userId, totalCount);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetPendingCountByUserAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting pending reminders for user {UserId}", userId);

        var count = await DbSet
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.Status == ReminderStatus.Pending, cancellationToken);

        _logger.LogDebug("User {UserId} has {Count} pending reminders", userId, count);
        return count;
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        ReminderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving reminders for guild {GuildId}, page {Page}, pageSize {PageSize}, status {Status}",
            guildId, page, pageSize, status);

        var query = DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Where(r => r.GuildId == guildId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} reminders for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<Reminder?> GetByIdForUserAsync(Guid id, ulong userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving reminder {Id} for user {UserId}", id, userId);

        var result = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

        _logger.LogDebug("Reminder {Id} for user {UserId} found: {Found}", id, userId, result != null);
        return result;
    }

    /// <inheritdoc/>
    public async Task<(int TotalCount, int PendingCount, int DeliveredTodayCount, int FailedCount)> GetGuildStatsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving reminder stats for guild {GuildId}", guildId);

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var query = DbSet.AsNoTracking().Where(r => r.GuildId == guildId);

        var totalCount = await query.CountAsync(cancellationToken);
        var pendingCount = await query.CountAsync(r => r.Status == ReminderStatus.Pending, cancellationToken);
        var deliveredTodayCount = await query.CountAsync(
            r => r.Status == ReminderStatus.Delivered &&
                 r.DeliveredAt.HasValue &&
                 r.DeliveredAt.Value >= today &&
                 r.DeliveredAt.Value < tomorrow,
            cancellationToken);
        var failedCount = await query.CountAsync(r => r.Status == ReminderStatus.Failed, cancellationToken);

        _logger.LogDebug(
            "Guild {GuildId} reminder stats: Total={Total}, Pending={Pending}, DeliveredToday={DeliveredToday}, Failed={Failed}",
            guildId, totalCount, pendingCount, deliveredTodayCount, failedCount);

        return (totalCount, pendingCount, deliveredTodayCount, failedCount);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UpcomingReminderDto>> GetUpcomingAsync(
        ulong guildId,
        int count,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Count} upcoming reminders for guild {GuildId}", count, guildId);

        var now = DateTime.UtcNow;

        var upcomingReminders = await (
            from r in DbSet.AsNoTracking()
            where r.GuildId == guildId && r.Status == ReminderStatus.Pending && r.TriggerAt > now
            orderby r.TriggerAt
            join u in Context.Users.AsNoTracking() on r.UserId equals u.Id into uGroup
            from u in uGroup.DefaultIfEmpty()
            select new UpcomingReminderDto
            {
                Id = r.Id,
                UserId = r.UserId,
                Username = u != null && u.Username != null ? u.Username : r.UserId.ToString(),
                MessagePreview = r.Message.Length > 50
                    ? r.Message.Substring(0, 50) + "..."
                    : r.Message,
                TriggerAt = r.TriggerAt
            }
        )
        .Take(count)
        .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} upcoming reminders for guild {GuildId}", upcomingReminders.Count, guildId);

        return upcomingReminders;
    }
}
