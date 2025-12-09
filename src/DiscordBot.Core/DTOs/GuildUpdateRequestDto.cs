namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for updating guild settings.
/// </summary>
public class GuildUpdateRequestDto
{
    /// <summary>
    /// Gets or sets the custom command prefix. Null means no change.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the guild settings as JSON. Null means no change.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Gets or sets whether the guild is active. Null means no change.
    /// </summary>
    public bool? IsActive { get; set; }
}
