using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the member directory page.
/// </summary>
public record MemberDirectoryViewModel
{
    /// <summary>
    /// The Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// The guild name for display.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// The list of members for the current page.
    /// </summary>
    public List<MemberListItemViewModel> Members { get; init; } = new();

    /// <summary>
    /// Total count of members matching current filters.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total unfiltered member count for the badge.
    /// </summary>
    public int TotalMemberCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Whether any filters are currently active.
    /// </summary>
    public bool HasActiveFilters { get; init; }

    /// <summary>
    /// Count of active filters.
    /// </summary>
    public int ActiveFilterCount { get; init; }

    // Filter values (for maintaining state)
    public string? SearchTerm { get; init; }
    public List<ulong> SelectedRoles { get; init; } = new();
    public DateTime? JoinedAfter { get; init; }
    public DateTime? JoinedBefore { get; init; }
    public string? ActivityFilter { get; init; }
    public string SortBy { get; init; } = "JoinedAt";
    public bool SortDescending { get; init; }

    /// <summary>
    /// Available roles for the filter dropdown.
    /// </summary>
    public List<GuildRoleDto> AvailableRoles { get; init; } = new();
}

/// <summary>
/// View model for a member list item (table row or card).
/// </summary>
public record MemberListItemViewModel
{
    /// <summary>
    /// The Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// The effective display name (nickname or global display name or username).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// The Discord username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The guild-specific nickname, if set.
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// The global display name, if set.
    /// </summary>
    public string? GlobalDisplayName { get; init; }

    /// <summary>
    /// The avatar URL, or null for default.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// When the user joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>
    /// ISO 8601 formatted join date for JavaScript.
    /// </summary>
    public string JoinedAtUtcIso => JoinedAt.ToString("o");

    /// <summary>
    /// When the user was last active.
    /// </summary>
    public DateTime? LastActiveAt { get; init; }

    /// <summary>
    /// ISO 8601 formatted last active date for JavaScript.
    /// </summary>
    public string? LastActiveAtUtcIso => LastActiveAt?.ToString("o");

    /// <summary>
    /// When the Discord account was created.
    /// </summary>
    public DateTime? AccountCreatedAt { get; init; }

    /// <summary>
    /// ISO 8601 formatted account created date for JavaScript.
    /// </summary>
    public string? AccountCreatedAtUtcIso => AccountCreatedAt?.ToString("o");

    /// <summary>
    /// The member's roles.
    /// </summary>
    public List<RoleViewModel> Roles { get; init; } = new();
}

/// <summary>
/// View model for a Discord role.
/// </summary>
public record RoleViewModel
{
    /// <summary>
    /// The Discord role snowflake ID.
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// The role name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The role color as a hex string (e.g., "#FF5733").
    /// </summary>
    public string ColorHex { get; init; } = "#99aab5";
}
