using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the guild settings edit form.
/// </summary>
public class GuildEditViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name (display only).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (display only).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the guild is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creates a GuildEditViewModel from a GuildDto.
    /// </summary>
    public static GuildEditViewModel FromDto(GuildDto dto)
    {
        return new GuildEditViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive
        };
    }
}
