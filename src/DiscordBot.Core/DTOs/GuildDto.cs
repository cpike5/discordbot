namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object containing full guild information.
/// </summary>
public class GuildDto
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the bot joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the guild is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the custom command prefix for the guild.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the guild settings as JSON.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Gets or sets the member count from live Discord data.
    /// </summary>
    public int? MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the guild icon URL from live Discord data.
    /// </summary>
    public string? IconUrl { get; set; }
}
