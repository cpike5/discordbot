namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object containing guild information.
/// </summary>
public class GuildInfoDto
{
    /// <summary>
    /// Gets or sets the guild ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of members in the guild.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the URL to the guild's icon.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the date when the bot joined the guild.
    /// </summary>
    public DateTime? JoinedAt { get; set; }
}
