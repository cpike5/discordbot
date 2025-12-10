namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for updating an existing user.
/// </summary>
public class UserUpdateDto
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}
