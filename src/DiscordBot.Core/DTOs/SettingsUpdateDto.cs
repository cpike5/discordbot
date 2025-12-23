namespace DiscordBot.Core.DTOs;

/// <summary>
/// Request DTO for updating multiple settings at once.
/// </summary>
public record SettingsUpdateDto
{
    /// <summary>
    /// Dictionary of setting keys to new values.
    /// </summary>
    public Dictionary<string, string> Settings { get; init; } = new();
}
