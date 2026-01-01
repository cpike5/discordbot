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

        return reminder;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Reminder> Items, int TotalCount)> GetUserRemindersAsync(
        ulong userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _reminderRepository.GetByUserAsync(
            userId,
            page,
            pageSize,
            pendingOnly: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Reminder?> CancelReminderAsync(
        Guid id,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var reminder = await _reminderRepository.GetByIdForUserAsync(id, userId, cancellationToken);

        if (reminder == null)
        {
            _logger.LogDebug(
                "Cancel reminder failed: reminder {ReminderId} not found or not owned by user {UserId}",
                id,
                userId);
            return null;
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogDebug(
                "Cancel reminder failed: reminder {ReminderId} is not pending (status: {Status})",
                id,
                reminder.Status);
            return null;
        }

        reminder.Status = ReminderStatus.Cancelled;
        await _reminderRepository.UpdateAsync(reminder, cancellationToken);

        _logger.LogInformation(
            "Reminder cancelled: ID {ReminderId} by user {UserId}",
            id,
            userId);

        return reminder;
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        return await _reminderRepository.GetPendingCountByUserAsync(userId, cancellationToken);
    }
}
