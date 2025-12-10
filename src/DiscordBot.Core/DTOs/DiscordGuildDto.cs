namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for Discord guild (server) information retrieved from the Discord API.
/// </summary>
public class DiscordGuildDto
{
    /// <summary>
    /// Discord guild ID (snowflake).
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Guild name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Icon hash from Discord. Used to construct icon URL.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Whether the user is the owner of this guild.
    /// </summary>
    public bool Owner { get; set; }

    /// <summary>
    /// Bitwise permission flags for the user in this guild.
    /// </summary>
    public long Permissions { get; set; }

    /// <summary>
    /// Full URL to the guild's icon image.
    /// Null if the guild has no custom icon.
    /// </summary>
    public string? IconUrl => string.IsNullOrEmpty(Icon)
        ? null
        : $"https://cdn.discordapp.com/icons/{Id}/{Icon}.png";
}
