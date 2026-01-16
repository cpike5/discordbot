using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a paginated list of notifications.
/// </summary>
public record NotificationListViewModel
{
    /// <summary>
    /// Gets the collection of notification items for the current page.
    /// </summary>
    public IReadOnlyList<NotificationListItem> Notifications { get; init; } = Array.Empty<NotificationListItem>();

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; init; } = 1;

    /// <summary>
    /// Gets the total number of notification entries across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the page size (number of items per page).
    /// </summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Gets whether there is a next page available.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets the filter options applied to the notification list.
    /// </summary>
    public NotificationFilterOptions Filters { get; init; } = new();

    /// <summary>
    /// Creates a <see cref="NotificationListViewModel"/> from a paginated response.
    /// </summary>
    /// <param name="paginatedResponse">The paginated notification response.</param>
    /// <param name="filters">Optional filter options.</param>
    /// <returns>A new <see cref="NotificationListViewModel"/> instance.</returns>
    public static NotificationListViewModel FromPaginatedDto(
        PaginatedResponseDto<UserNotificationDto> paginatedResponse,
        NotificationFilterOptions? filters = null)
    {
        return new NotificationListViewModel
        {
            Notifications = paginatedResponse.Items.Select(NotificationListItem.FromDto).ToList(),
            CurrentPage = paginatedResponse.Page,
            TotalPages = paginatedResponse.TotalPages,
            TotalCount = paginatedResponse.TotalCount,
            PageSize = paginatedResponse.PageSize,
            Filters = filters ?? new NotificationFilterOptions()
        };
    }
}

/// <summary>
/// Represents a notification item for list display.
/// </summary>
public record NotificationListItem
{
    /// <summary>
    /// Gets the unique identifier for the notification.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the notification type.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Gets the human-readable display name for the notification type.
    /// </summary>
    public string TypeDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the type badge.
    /// </summary>
    public string TypeBadgeClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the severity level for performance alerts.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Gets the human-readable severity display.
    /// </summary>
    public string? SeverityDisplay { get; init; }

    /// <summary>
    /// Gets the CSS class for the severity badge.
    /// </summary>
    public string? SeverityBadgeClass { get; init; }

    /// <summary>
    /// Gets the notification title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the notification message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional link URL for navigation.
    /// </summary>
    public string? LinkUrl { get; init; }

    /// <summary>
    /// Gets the guild ID if this is a guild-specific notification.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets the guild name for display purposes.
    /// </summary>
    public string? GuildName { get; init; }

    /// <summary>
    /// Gets whether the notification has been read.
    /// </summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the created at timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string CreatedAtUtcIso => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets the human-readable relative time since creation.
    /// </summary>
    public string TimeAgo { get; init; } = string.Empty;

    /// <summary>
    /// Creates a <see cref="NotificationListItem"/> from a <see cref="UserNotificationDto"/>.
    /// </summary>
    /// <param name="dto">The notification DTO to map from.</param>
    /// <returns>A new <see cref="NotificationListItem"/> instance.</returns>
    public static NotificationListItem FromDto(UserNotificationDto dto)
    {
        return new NotificationListItem
        {
            Id = dto.Id,
            Type = dto.Type,
            TypeDisplay = dto.TypeDisplay,
            TypeBadgeClass = GetTypeBadgeClass(dto.Type),
            Severity = dto.Severity,
            SeverityDisplay = dto.Severity?.ToString(),
            SeverityBadgeClass = GetSeverityBadgeClass(dto.Severity),
            Title = dto.Title,
            Message = dto.Message,
            LinkUrl = dto.LinkUrl,
            GuildId = dto.GuildId,
            GuildName = dto.GuildName,
            IsRead = dto.IsRead,
            CreatedAt = dto.CreatedAt,
            TimeAgo = dto.TimeAgo
        };
    }

    private static string GetTypeBadgeClass(NotificationType type) => type switch
    {
        NotificationType.PerformanceAlert => "bg-error text-white",
        NotificationType.BotStatus => "bg-accent-blue text-white",
        NotificationType.GuildEvent => "bg-accent-purple text-white",
        NotificationType.CommandError => "bg-accent-orange text-white",
        _ => "bg-bg-tertiary text-text-secondary"
    };

    private static string? GetSeverityBadgeClass(AlertSeverity? severity) => severity switch
    {
        AlertSeverity.Critical => "bg-error text-white",
        AlertSeverity.Warning => "bg-accent-orange text-white",
        AlertSeverity.Info => "bg-accent-blue text-white",
        _ => null
    };
}

/// <summary>
/// Represents filter options for notification queries.
/// </summary>
public record NotificationFilterOptions
{
    /// <summary>
    /// Gets the notification type filter. Null means no filter.
    /// </summary>
    public NotificationType? Type { get; init; }

    /// <summary>
    /// Gets the read status filter. Null = all, true = read only, false = unread only.
    /// </summary>
    public bool? IsRead { get; init; }

    /// <summary>
    /// Gets the severity filter. Null means no filter.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Gets the start date for date range filter. Null means no start date limit.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the end date for date range filter. Null means no end date limit.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets the search term for title/message search. Null or empty means no search filter.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets whether any filters are currently applied.
    /// </summary>
    public bool HasActiveFilters =>
        Type.HasValue ||
        IsRead.HasValue ||
        Severity.HasValue ||
        StartDate.HasValue ||
        EndDate.HasValue ||
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        GuildId.HasValue;
}
