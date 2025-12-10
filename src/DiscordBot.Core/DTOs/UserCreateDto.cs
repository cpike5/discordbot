namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new user.
/// </summary>
public class UserCreateDto
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public bool SendWelcomeEmail { get; set; } = true;
}
