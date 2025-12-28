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
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; } = 10;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets whether there is a next page available.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets or sets the search term for filtering guilds.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets or sets the status filter (null for all, true for active, false for inactive).
    /// </summary>
    public bool? StatusFilter { get; init; }

    /// <summary>
    /// Gets or sets the field to sort by.
    /// </summary>
    public string SortBy { get; init; } = "Name";

    /// <summary>
    /// Gets or sets whether to sort in descending order.
    /// </summary>
    public bool SortDescending { get; init; }

    /// <summary>
    /// Gets the available sort options for the dropdown.
    /// </summary>
    public static IReadOnlyList<(string Value, string Display)> SortOptions { get; } = new List<(string, string)>
    {
        ("Name", "Name"),
        ("MemberCount", "Member Count"),
        ("JoinedAt", "Join Date")
    };

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
            TotalCount = paginatedResponse.TotalCount,
            CurrentPage = paginatedResponse.Page,
            PageSize = paginatedResponse.PageSize
        };
    }

    /// <summary>
    /// Creates a <see cref="GuildListViewModel"/> from a paginated response with query parameters.
    /// </summary>
    /// <param name="paginatedResponse">The paginated guild response.</param>
    /// <param name="query">The search query used to generate this response.</param>
    /// <returns>A new <see cref="GuildListViewModel"/> instance.</returns>
    public static GuildListViewModel FromPaginatedDto(
        PaginatedResponseDto<GuildDto> paginatedResponse,
        GuildSearchQueryDto query)
    {
        return new GuildListViewModel
        {
            Guilds = paginatedResponse.Items.Select(GuildSummaryItem.FromDto).ToList(),
            TotalCount = paginatedResponse.TotalCount,
            CurrentPage = paginatedResponse.Page,
            PageSize = paginatedResponse.PageSize,
            SearchTerm = query.SearchTerm,
            StatusFilter = query.IsActive,
            SortBy = query.SortBy,
            SortDescending = query.SortDescending
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
    /// Gets the joined at timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string JoinedAtUtcIso => JoinedAt.ToString("o");

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
