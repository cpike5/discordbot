using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a paginated list of users with search and filter capabilities.
/// </summary>
public class UserListViewModel
{
    public IReadOnlyList<UserDto> Users { get; set; } = Array.Empty<UserDto>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    // Search/filter state
    public string? SearchTerm { get; set; }
    public string? RoleFilter { get; set; }
    public bool? ActiveFilter { get; set; }
    public bool? DiscordLinkedFilter { get; set; }

    // Available options for filters
    public IReadOnlyList<string> AvailableRoles { get; set; } = Array.Empty<string>();

    // Current user info for permission checks
    public string CurrentUserId { get; set; } = string.Empty;
    public bool CanCreateUsers { get; set; }
}
