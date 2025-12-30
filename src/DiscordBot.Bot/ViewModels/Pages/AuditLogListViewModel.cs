using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using System.Text.Json;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a paginated list of audit log entries.
/// </summary>
public record AuditLogListViewModel
{
    /// <summary>
    /// Gets the collection of audit log items for the current page.
    /// </summary>
    public IReadOnlyList<AuditLogListItem> Logs { get; init; } = Array.Empty<AuditLogListItem>();

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; init; } = 1;

    /// <summary>
    /// Gets the total number of audit log entries across all pages.
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
    /// Gets the filter options applied to the audit log list.
    /// </summary>
    public AuditLogFilterOptions Filters { get; init; } = new();

    /// <summary>
    /// Creates an <see cref="AuditLogListViewModel"/> from a paginated response.
    /// </summary>
    /// <param name="paginatedResponse">The paginated audit log response.</param>
    /// <param name="filters">Optional filter options.</param>
    /// <returns>A new <see cref="AuditLogListViewModel"/> instance.</returns>
    public static AuditLogListViewModel FromPaginatedDto(
        PaginatedResponseDto<AuditLogDto> paginatedResponse,
        AuditLogFilterOptions? filters = null)
    {
        return new AuditLogListViewModel
        {
            Logs = paginatedResponse.Items.Select(AuditLogListItem.FromDto).ToList(),
            CurrentPage = paginatedResponse.Page,
            TotalPages = paginatedResponse.TotalPages,
            TotalCount = paginatedResponse.TotalCount,
            PageSize = paginatedResponse.PageSize,
            Filters = filters ?? new AuditLogFilterOptions()
        };
    }
}

/// <summary>
/// Represents an audit log entry for list display.
/// </summary>
public record AuditLogListItem
{
    /// <summary>
    /// Gets the unique identifier for the audit log entry.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the timestamp when the action occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the UTC timestamp in ISO 8601 format for client-side timezone conversion.
    /// Use with data-utc attribute in HTML elements.
    /// </summary>
    public string TimestampUtcIso { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category of the audit log entry.
    /// </summary>
    public AuditLogCategory CategoryEnum { get; init; }

    /// <summary>
    /// Gets the category name for display.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the category badge.
    /// </summary>
    public string CategoryBadgeClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the action of the audit log entry.
    /// </summary>
    public AuditLogAction ActionEnum { get; init; }

    /// <summary>
    /// Gets the action name for display.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the action badge.
    /// </summary>
    public string ActionBadgeClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the action border color (used in expandable rows).
    /// </summary>
    public string ActionBorderClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of actor who performed the action.
    /// </summary>
    public AuditLogActorType ActorType { get; init; }

    /// <summary>
    /// Gets the unique identifier of the actor (user ID or null).
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Gets the display name of the actor (if available).
    /// </summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>
    /// Gets the display name of the actor who performed the action.
    /// </summary>
    public string ActorName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the initials of the actor for avatar display.
    /// </summary>
    public string ActorInitials { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the actor avatar background color.
    /// </summary>
    public string ActorAvatarClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type name of the entity that was affected.
    /// </summary>
    public string TargetType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the entity that was affected.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild name for display.
    /// </summary>
    public string? GuildName { get; init; }

    /// <summary>
    /// Gets the truncated details summary for table display.
    /// </summary>
    public string DetailsSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw details JSON string.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the formatted details for display (pretty-printed JSON).
    /// </summary>
    public string? FormattedDetails { get; init; }

    /// <summary>
    /// Gets whether the entry has details.
    /// </summary>
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    /// <summary>
    /// Gets the correlation ID to group related audit log entries.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; init; }

    #region Actor Display Helper Properties

    /// <summary>
    /// Gets whether the actor is a user type.
    /// </summary>
    public bool IsUserActor => ActorType == AuditLogActorType.User;

    /// <summary>
    /// Gets whether the actor is a system type.
    /// </summary>
    public bool IsSystemActor => ActorType == AuditLogActorType.System;

    /// <summary>
    /// Gets whether the actor is a bot type.
    /// </summary>
    public bool IsBotActor => ActorType == AuditLogActorType.Bot;

    /// <summary>
    /// Gets whether the actor has a display name (not just a GUID).
    /// </summary>
    public bool HasActorDisplayName => !string.IsNullOrWhiteSpace(ActorDisplayName);

    /// <summary>
    /// Gets whether the actor ID is a valid GUID.
    /// </summary>
    public bool HasActorGuid => !string.IsNullOrWhiteSpace(ActorId) && Guid.TryParse(ActorId, out _);

    /// <summary>
    /// Gets the truncated actor ID (first 8 characters) for display.
    /// </summary>
    public string TruncatedActorId => !string.IsNullOrWhiteSpace(ActorId) && ActorId.Length >= 8
        ? ActorId.Substring(0, 8)
        : ActorId ?? string.Empty;

    /// <summary>
    /// Gets the URL to view user details for GUID-only actors.
    /// </summary>
    public string? ActorLinkUrl => IsUserActor && HasActorGuid && !HasActorDisplayName
        ? $"/Admin/Users/Details?id={ActorId}"
        : null;

    #endregion

    #region Guild Display Helper Methods

    /// <summary>
    /// Gets the display text for the guild column.
    /// Shows guild name if available, "System" for system-wide actions, or "Unknown" otherwise.
    /// </summary>
    /// <returns>The guild display text.</returns>
    public string GetGuildDisplay()
    {
        if (!string.IsNullOrEmpty(GuildName))
            return GuildName;

        // System-wide actions that don't have a guild
        return IsSystemWideAction() ? "System" : "Unknown";
    }

    /// <summary>
    /// Determines if this audit log entry represents a system-wide action.
    /// </summary>
    private bool IsSystemWideAction()
    {
        // Check if this is a system-wide action based on action type or category
        return ActionEnum == AuditLogAction.Login
            || ActionEnum == AuditLogAction.Logout
            || ActionEnum == AuditLogAction.SettingChanged
            || CategoryEnum == AuditLogCategory.System
            || CategoryEnum == AuditLogCategory.Security;
    }

    #endregion

    /// <summary>
    /// Creates an <see cref="AuditLogListItem"/> from an <see cref="AuditLogDto"/>.
    /// </summary>
    /// <param name="dto">The audit log DTO to map from.</param>
    /// <returns>A new <see cref="AuditLogListItem"/> instance.</returns>
    public static AuditLogListItem FromDto(AuditLogDto dto)
    {
        var actorName = dto.ActorDisplayName ?? dto.ActorId ?? "Unknown";
        var actorInitials = GetInitials(actorName, dto.ActorType, dto.ActorDisplayName);
        var actorAvatarClass = GetActorAvatarClass(dto.ActorType);
        var categoryBadgeClass = GetCategoryBadgeClass(dto.Category);
        var actionBadgeClass = GetActionBadgeClass(dto.Action);
        var actionBorderClass = GetActionBorderClass(dto.Action);
        var detailsSummary = GetDetailsSummary(dto.Details);
        var (details, formattedDetails) = FormatDetails(dto.Details);

        return new AuditLogListItem
        {
            Id = dto.Id,
            Timestamp = dto.Timestamp,
            TimestampUtcIso = DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc).ToString("o"),
            CategoryEnum = dto.Category,
            Category = dto.CategoryName,
            CategoryBadgeClass = categoryBadgeClass,
            ActionEnum = dto.Action,
            Action = dto.ActionName,
            ActionBadgeClass = actionBadgeClass,
            ActionBorderClass = actionBorderClass,
            ActorType = dto.ActorType,
            ActorId = dto.ActorId,
            ActorDisplayName = dto.ActorDisplayName,
            ActorName = actorName,
            ActorInitials = actorInitials,
            ActorAvatarClass = actorAvatarClass,
            TargetType = dto.TargetType ?? string.Empty,
            TargetId = dto.TargetId ?? string.Empty,
            GuildName = dto.GuildName,
            DetailsSummary = detailsSummary,
            Details = details,
            FormattedDetails = formattedDetails,
            CorrelationId = dto.CorrelationId,
            IpAddress = dto.IpAddress
        };
    }

    /// <summary>
    /// Generates initials from a name for avatar display.
    /// For System/Bot actors, returns empty string (icon will be used instead).
    /// For users without a display name (GUID only), returns empty string (icon will be used instead).
    /// </summary>
    /// <param name="name">The name to generate initials from.</param>
    /// <param name="actorType">The type of actor.</param>
    /// <param name="displayName">The actor's display name (null if only GUID is available).</param>
    private static string GetInitials(string name, AuditLogActorType actorType, string? displayName)
    {
        // System and Bot actors use icons instead of initials
        if (actorType == AuditLogActorType.System || actorType == AuditLogActorType.Bot)
            return string.Empty;

        // Users without a display name (GUID only) use icon instead of initials
        if (actorType == AuditLogActorType.User && string.IsNullOrWhiteSpace(displayName))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();

        return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
    }

    /// <summary>
    /// Gets the CSS class for actor avatar based on actor type.
    /// </summary>
    private static string GetActorAvatarClass(AuditLogActorType actorType)
    {
        return actorType switch
        {
            AuditLogActorType.System => "bg-bg-tertiary text-text-secondary",
            AuditLogActorType.Bot => "bg-bg-tertiary text-text-secondary",
            AuditLogActorType.User => "bg-accent-blue text-white",
            _ => "bg-bg-tertiary text-text-secondary"
        };
    }

    /// <summary>
    /// Gets the CSS class for category badge based on category.
    /// </summary>
    private static string GetCategoryBadgeClass(AuditLogCategory category)
    {
        return category switch
        {
            AuditLogCategory.User => "bg-accent-blue text-white",
            AuditLogCategory.Guild => "bg-accent-purple text-white",
            AuditLogCategory.Configuration => "bg-accent-orange text-white",
            AuditLogCategory.Security => "bg-error text-white",
            AuditLogCategory.Command => "bg-success text-white",
            AuditLogCategory.Message => "bg-accent-blue text-white",
            AuditLogCategory.System => "bg-bg-tertiary text-text-secondary",
            _ => "bg-bg-tertiary text-text-secondary"
        };
    }

    /// <summary>
    /// Gets the CSS class for action badge based on action.
    /// </summary>
    private static string GetActionBadgeClass(AuditLogAction action)
    {
        return action switch
        {
            AuditLogAction.Created => "bg-success text-white",
            AuditLogAction.Updated => "bg-accent-blue text-white",
            AuditLogAction.Deleted => "bg-error text-white",
            AuditLogAction.Login => "bg-success text-white",
            AuditLogAction.Logout => "bg-bg-tertiary text-text-secondary",
            AuditLogAction.PermissionChanged => "bg-accent-orange text-white",
            AuditLogAction.SettingChanged => "bg-accent-orange text-white",
            AuditLogAction.CommandExecuted => "bg-accent-blue text-white",
            AuditLogAction.MessageDeleted => "bg-error text-white",
            AuditLogAction.MessageEdited => "bg-accent-blue text-white",
            AuditLogAction.UserBanned => "bg-error text-white",
            AuditLogAction.UserUnbanned => "bg-success text-white",
            AuditLogAction.UserKicked => "bg-accent-orange text-white",
            AuditLogAction.RoleAssigned => "bg-success text-white",
            AuditLogAction.RoleRemoved => "bg-accent-orange text-white",
            _ => "bg-bg-tertiary text-text-secondary"
        };
    }

    /// <summary>
    /// Gets the CSS border class for an action (used in expandable row styling).
    /// </summary>
    private static string GetActionBorderClass(AuditLogAction action)
    {
        return action switch
        {
            AuditLogAction.Created => "border-success",
            AuditLogAction.Updated => "border-accent-blue",
            AuditLogAction.Deleted => "border-error",
            AuditLogAction.Login => "border-success",
            AuditLogAction.Logout => "border-border-primary",
            AuditLogAction.PermissionChanged => "border-accent-orange",
            AuditLogAction.SettingChanged => "border-accent-orange",
            AuditLogAction.CommandExecuted => "border-accent-blue",
            AuditLogAction.MessageDeleted => "border-error",
            AuditLogAction.MessageEdited => "border-accent-blue",
            AuditLogAction.UserBanned => "border-error",
            AuditLogAction.UserUnbanned => "border-success",
            AuditLogAction.UserKicked => "border-accent-orange",
            AuditLogAction.RoleAssigned => "border-success",
            AuditLogAction.RoleRemoved => "border-accent-orange",
            _ => "border-border-primary"
        };
    }

    /// <summary>
    /// Gets a truncated summary of the details JSON for table display.
    /// </summary>
    private static string GetDetailsSummary(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return string.Empty;

        const int maxLength = 80;
        var cleaned = details.Replace("\r", "").Replace("\n", " ").Trim();

        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Formats the details JSON for display.
    /// </summary>
    /// <param name="details">The raw details JSON string.</param>
    /// <returns>A tuple containing the raw details and the formatted (pretty-printed) details.</returns>
    private static (string? Details, string? FormattedDetails) FormatDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return (null, null);

        try
        {
            // Pretty-print JSON
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(details);
            var formattedDetails = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
            return (details, formattedDetails);
        }
        catch
        {
            // If not valid JSON, use as-is
            return (details, details);
        }
    }
}

/// <summary>
/// Represents filter options for audit log queries.
/// </summary>
public record AuditLogFilterOptions
{
    /// <summary>
    /// Gets the category filter. Null means no filter.
    /// </summary>
    public AuditLogCategory? Category { get; init; }

    /// <summary>
    /// Gets the action filter. Null means no filter.
    /// </summary>
    public AuditLogAction? Action { get; init; }

    /// <summary>
    /// Gets the actor ID filter. Null or empty means no filter.
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Gets the actor type filter. Null means no filter.
    /// </summary>
    public AuditLogActorType? ActorType { get; init; }

    /// <summary>
    /// Gets the target type filter. Null or empty means no filter.
    /// </summary>
    public string? TargetType { get; init; }

    /// <summary>
    /// Gets the target ID filter. Null or empty means no filter.
    /// </summary>
    public string? TargetId { get; init; }

    /// <summary>
    /// Gets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets the start date for date range filter. Null means no start date limit.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the end date for date range filter. Null means no end date limit.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets the search term for multi-field search. Null or empty means no search filter.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets whether to show the filter panel.
    /// </summary>
    public bool ShowFilters { get; init; } = true;

    /// <summary>
    /// Gets whether any filters are currently applied.
    /// </summary>
    public bool HasActiveFilters =>
        Category.HasValue ||
        Action.HasValue ||
        !string.IsNullOrWhiteSpace(ActorId) ||
        ActorType.HasValue ||
        !string.IsNullOrWhiteSpace(TargetType) ||
        !string.IsNullOrWhiteSpace(TargetId) ||
        GuildId.HasValue ||
        StartDate.HasValue ||
        EndDate.HasValue ||
        !string.IsNullOrWhiteSpace(SearchTerm);
}
