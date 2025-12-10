using Microsoft.AspNetCore.Mvc.Rendering;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for user create and edit forms.
/// </summary>
public class UserFormViewModel
{
    // For both create and edit
    public string? UserId { get; set; } // Null for create
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Password { get; set; } // Only for create
    public string? ConfirmPassword { get; set; } // Only for create
    public string Role { get; set; } = "Viewer";
    public bool IsActive { get; set; } = true;

    // Options
    public IReadOnlyList<SelectListItem> AvailableRoles { get; set; } = Array.Empty<SelectListItem>();

    // Edit-only display fields
    public bool IsDiscordLinked { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }

    // Permissions
    public bool CanChangeRole { get; set; }
    public bool CanChangeActiveStatus { get; set; }
    public bool IsSelf { get; set; }
}
