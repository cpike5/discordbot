using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Reminders admin page.
/// Displays reminders for a guild with pagination and filtering.
/// </summary>
public record RemindersIndexViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Gets the list of reminders for this guild.
    /// </summary>
    public IReadOnlyList<ReminderItemViewModel> Reminders { get; init; } = Array.Empty<ReminderItemViewModel>();

    /// <summary>
    /// Gets the statistics for reminders in this guild.
    /// </summary>
    public ReminderStatsViewModel Stats { get; init; } = new();

    /// <summary>
    /// Gets the total number of reminders (matching current filter).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Gets the current status filter.
    /// </summary>
    public ReminderStatus? StatusFilter { get; init; }

    /// <summary>
    /// Gets the current user search filter.
    /// </summary>
    public string? UserSearch { get; init; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets whether there are more pages.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there are previous pages.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Creates a RemindersIndexViewModel from service data.
    /// </summary>
    public static RemindersIndexViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        IEnumerable<Reminder> reminders,
        int totalCount,
        ReminderStatsViewModel stats,
        int page,
        int pageSize,
        ReminderStatus? statusFilter = null,
        string? userSearch = null)
    {
        return new RemindersIndexViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            Reminders = reminders.Select(ReminderItemViewModel.FromEntity).ToList(),
            TotalCount = totalCount,
            Stats = stats,
            CurrentPage = page,
            PageSize = pageSize,
            StatusFilter = statusFilter,
            UserSearch = userSearch
        };
    }
}

/// <summary>
/// View model for a single reminder item.
/// </summary>
public record ReminderItemViewModel
{
    /// <summary>
    /// Gets the unique identifier for this reminder.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the user ID who set the reminder.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the username (populated by lookup).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the reminder message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets a truncated preview of the message.
    /// </summary>
    public string MessagePreview => Message.Length > 50
        ? Message[..50] + "..."
        : Message;

    /// <summary>
    /// Gets the trigger time (UTC).
    /// </summary>
    public DateTime TriggerAt { get; init; }

    /// <summary>
    /// Gets the trigger time in ISO format for client-side rendering.
    /// </summary>
    public string TriggerAtUtcIso => DateTime.SpecifyKind(TriggerAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets the creation time (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the creation time in ISO format for client-side rendering.
    /// </summary>
    public string CreatedAtUtcIso => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets the delivery time (UTC), if delivered.
    /// </summary>
    public DateTime? DeliveredAt { get; init; }

    /// <summary>
    /// Gets the delivery time in ISO format for client-side rendering.
    /// </summary>
    public string? DeliveredAtUtcIso => DeliveredAt.HasValue
        ? DateTime.SpecifyKind(DeliveredAt.Value, DateTimeKind.Utc).ToString("o")
        : null;

    /// <summary>
    /// Gets the current status of the reminder.
    /// </summary>
    public ReminderStatus Status { get; init; }

    /// <summary>
    /// Gets the status display text.
    /// </summary>
    public string StatusText => Status switch
    {
        ReminderStatus.Pending => "Pending",
        ReminderStatus.Delivered => "Delivered",
        ReminderStatus.Failed => "Failed",
        ReminderStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the number of delivery attempts.
    /// </summary>
    public int DeliveryAttempts { get; init; }

    /// <summary>
    /// Gets the last error message, if any.
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Gets whether this reminder can be cancelled.
    /// </summary>
    public bool CanCancel => Status == ReminderStatus.Pending;

    /// <summary>
    /// Creates a ReminderItemViewModel from an entity.
    /// Note: Username and AvatarUrl must be populated separately via Discord lookup.
    /// </summary>
    public static ReminderItemViewModel FromEntity(Reminder entity)
    {
        return new ReminderItemViewModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Message = entity.Message,
            TriggerAt = entity.TriggerAt,
            CreatedAt = entity.CreatedAt,
            DeliveredAt = entity.DeliveredAt,
            Status = entity.Status,
            DeliveryAttempts = entity.DeliveryAttempts,
            LastError = entity.LastError
        };
    }

    /// <summary>
    /// Creates a new instance with user information populated.
    /// </summary>
    public ReminderItemViewModel WithUserInfo(string username, string? avatarUrl)
    {
        return this with
        {
            Username = username,
            AvatarUrl = avatarUrl
        };
    }
}

/// <summary>
/// View model for reminder statistics.
/// </summary>
public record ReminderStatsViewModel
{
    /// <summary>
    /// Gets the total count of reminders.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the count of pending reminders.
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Gets the count of reminders delivered today.
    /// </summary>
    public int DeliveredTodayCount { get; init; }

    /// <summary>
    /// Gets the count of failed reminders.
    /// </summary>
    public int FailedCount { get; init; }
}
