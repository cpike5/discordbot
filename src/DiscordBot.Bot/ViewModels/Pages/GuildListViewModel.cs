using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a list of guilds.
/// </summary>
public record GuildListViewModel
{
    /// <summary>
    /// Gets the collection of guild summary items.
    /// </summary>
    public IReadOnlyList<GuildSummaryItem> Guilds { get; init; } = Array.Empty<GuildSummaryItem>();

    /// <summary>
    /// Gets the total number of guilds.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Creates a <see cref="GuildListViewModel"/> from a collection of <see cref="GuildDto"/>.
    /// </summary>
    /// <param name="dtos">The guild DTOs to map from.</param>
    /// <returns>A new <see cref="GuildListViewModel"/> instance.</returns>
    public static GuildListViewModel FromDtos(IEnumerable<GuildDto> dtos)
    {
        var guildList = dtos.ToList();
        return new GuildListViewModel
        {
            Guilds = guildList.Select(GuildSummaryItem.FromDto).ToList(),
            TotalCount = guildList.Count
        };
    }

    /// <summary>
    /// Creates a <see cref="GuildListViewModel"/> from a paginated response.
    /// </summary>
    /// <param name="paginatedResponse">The paginated guild response.</param>
    /// <returns>A new <see cref="GuildListViewModel"/> instance.</returns>
    public static GuildListViewModel FromPaginatedDto(PaginatedResponseDto<GuildDto> paginatedResponse)
    {
        return new GuildListViewModel
        {
            Guilds = paginatedResponse.Items.Select(GuildSummaryItem.FromDto).ToList(),
            TotalCount = paginatedResponse.TotalCount
        };
    }
}

/// <summary>
/// Represents a summary of guild information for list display.
/// </summary>
public record GuildSummaryItem
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the member count.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets whether the guild is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the date when the bot joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>
    /// Creates a <see cref="GuildSummaryItem"/> from a <see cref="GuildDto"/>.
    /// </summary>
    /// <param name="dto">The guild DTO to map from.</param>
    /// <returns>A new <see cref="GuildSummaryItem"/> instance.</returns>
    public static GuildSummaryItem FromDto(GuildDto dto)
    {
        return new GuildSummaryItem
        {
            Id = dto.Id,
            Name = dto.Name,
            MemberCount = dto.MemberCount ?? 0,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive,
            JoinedAt = dto.JoinedAt
        };
    }
}
