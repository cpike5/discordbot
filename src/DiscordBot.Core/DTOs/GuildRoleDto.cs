namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing a Discord guild role.
/// </summary>
public class GuildRoleDto
{
    /// <summary>
    /// Gets or sets the Discord role snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role color as an integer (RGB).
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    /// Gets or sets the role's position in the hierarchy.
    /// </summary>
    public int Position { get; set; }
}
