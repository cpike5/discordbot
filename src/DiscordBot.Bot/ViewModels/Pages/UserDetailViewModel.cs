using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying detailed user information and activity history.
/// </summary>
public class UserDetailViewModel
{
    public UserDto User { get; set; } = null!;
    public IReadOnlyList<UserActivityLogDto> RecentActivity { get; set; } = Array.Empty<UserActivityLogDto>();

    // Permission flags for current user
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanResetPassword { get; set; }
    public bool CanChangeRole { get; set; }
    public bool CanUnlinkDiscord { get; set; }
    public bool IsSelf { get; set; }
}
