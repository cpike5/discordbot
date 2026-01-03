using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing personal reminders.
/// </summary>
public class ReminderService : IReminderService
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ReminderOptions _options;
    private readonly ILogger<ReminderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderService"/> class.
    /// </summary>
    public ReminderService(
        IReminderRepository reminderRepository,
        IOptions<ReminderOptions> options,
        ILogger<ReminderService> logger)
    {
        _reminderRepository = reminderRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Reminder> CreateReminderAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        string message,
        DateTime triggerAt,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "reminder",
            "create",
            userId: userId,
            guildId: guildId);

        try
        {
            var reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                ChannelId = channelId,
                UserId = userId,
                Message = message,
                TriggerAt = triggerAt,
                CreatedAt = DateTime.UtcNow,
                Status = ReminderStatus.Pending,
                DeliveryAttempts = 0
            };

            await _reminderRepository.AddAsync(reminder, cancellationToken);

            _logger.LogInformation(
                "Reminder created: ID {ReminderId} for user {UserId} in guild {GuildId}, triggers at {TriggerAt}",
                reminder.Id,
                userId,
                guildId,
                triggerAt);

            BotActivitySource.SetSuccess(activity);
            return reminder;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Reminder> Items, int TotalCount)> GetUserRemindersAsync(
        ulong userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "reminder",
            "get_user_reminders",
            userId: userId);

        try
        {
            var result = await _reminderRepository.GetByUserAsync(
                userId,
                page,
                pageSize,
                pendingOnly: true,
                cancellationToken);

            BotActivitySource.SetRecordsReturned(activity, result.Items.Count());
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Reminder?> CancelReminderAsync(
        Guid id,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "reminder",
            "cancel",
            userId: userId,
            entityId: id.ToString());

        try
        {
            var reminder = await _reminderRepository.GetByIdForUserAsync(id, userId, cancellationToken);

            if (reminder == null)
            {
                _logger.LogDebug(
                    "Cancel reminder failed: reminder {ReminderId} not found or not owned by user {UserId}",
                    id,
                    userId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            if (reminder.Status != ReminderStatus.Pending)
            {
                _logger.LogDebug(
                    "Cancel reminder failed: reminder {ReminderId} is not pending (status: {Status})",
                    id,
                    reminder.Status);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            reminder.Status = ReminderStatus.Cancelled;
            await _reminderRepository.UpdateAsync(reminder, cancellationToken);

            _logger.LogInformation(
                "Reminder cancelled: ID {ReminderId} by user {UserId}",
                id,
                userId);

            BotActivitySource.SetSuccess(activity);
            return reminder;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "reminder",
            "get_pending_count",
            userId: userId);

        try
        {
            var count = await _reminderRepository.GetPendingCountByUserAsync(userId, cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
