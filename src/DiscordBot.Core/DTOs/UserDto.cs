namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for user information in listings and details views.
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }

    // Discord linking
    public bool IsDiscordLinked { get; set; }
    public ulong? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }

    // Roles
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    // Computed properties
    public string HighestRole => Roles.OrderByDescending(RolePriority).FirstOrDefault() ?? "None";

    private static int RolePriority(string role) => role switch
    {
        "SuperAdmin" => 4,
        "Admin" => 3,
        "Moderator" => 2,
        "Viewer" => 1,
        _ => 0
    };
}
