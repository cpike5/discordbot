using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// ViewModel for the Rat Watch Incidents browser page.
/// </summary>
public record RatWatchIncidentsViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Gets the current filter state for form pre-population.
    /// </summary>
    public RatWatchIncidentFilterState Filters { get; init; } = new();

    /// <summary>
    /// Gets the list of incidents matching the current filters.
    /// </summary>
    public IReadOnlyList<RatWatchItemViewModel> Incidents { get; init; } = Array.Empty<RatWatchItemViewModel>();

    /// <summary>
    /// Gets the total count of incidents matching the filter (across all pages).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => TotalCount > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;

    /// <summary>
    /// Gets whether there are more pages after the current page.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there are pages before the current page.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets the number of active filters for UI badge display.
    /// </summary>
    public int ActiveFilterCount { get; init; }

    /// <summary>
    /// Gets the voting duration in minutes from guild settings, needed for VoteEndsAt calculation.
    /// </summary>
    public int VotingDurationMinutes { get; init; }

    /// <summary>
    /// Gets all available status options for filter checkboxes.
    /// </summary>
    public static IReadOnlyList<RatWatchStatus> AllStatuses { get; } =
        Enum.GetValues<RatWatchStatus>().ToList();

    /// <summary>
    /// Factory method to create ViewModel from service results.
    /// </summary>
    /// <param name="guildId">The guild Discord snowflake ID.</param>
    /// <param name="guildName">The guild name.</param>
    /// <param name="guildIconUrl">The guild icon URL.</param>
    /// <param name="incidents">The incidents to display on the current page.</param>
    /// <param name="totalCount">The total count of incidents matching the filter.</param>
    /// <param name="filters">The current filter state.</param>
    /// <param name="page">The current page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="votingDurationMinutes">The voting duration from guild settings.</param>
    /// <returns>A new RatWatchIncidentsViewModel instance.</returns>
    public static RatWatchIncidentsViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        IEnumerable<RatWatchDto> incidents,
        int totalCount,
        RatWatchIncidentFilterState filters,
        int page,
        int pageSize,
        int votingDurationMinutes)
    {
        var incidentList = incidents
            .Select(dto => RatWatchItemViewModel.FromDto(dto, votingDurationMinutes))
            .ToList();

        return new RatWatchIncidentsViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            Incidents = incidentList,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            Filters = filters,
            ActiveFilterCount = filters.GetActiveFilterCount(),
            VotingDurationMinutes = votingDurationMinutes
        };
    }
}

/// <summary>
/// Represents the current state of filters for the incidents browser.
/// </summary>
public record RatWatchIncidentFilterState
{
    /// <summary>
    /// Gets the selected status filters. Empty list means all statuses.
    /// </summary>
    public IReadOnlyList<RatWatchStatus> Statuses { get; init; } = Array.Empty<RatWatchStatus>();

    /// <summary>
    /// Gets the start date filter (inclusive). Null means no start date filter.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the end date filter (inclusive). Null means no end date filter.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets the accused user filter. Null or empty means no filter on accused user.
    /// </summary>
    public string? AccusedUser { get; init; }

    /// <summary>
    /// Gets the initiator user filter. Null or empty means no filter on initiator user.
    /// </summary>
    public string? InitiatorUser { get; init; }

    /// <summary>
    /// Gets the minimum vote count filter. Null means no minimum vote requirement.
    /// </summary>
    public int? MinVoteCount { get; init; }

    /// <summary>
    /// Gets the keyword search filter. Null or empty means no keyword filter.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// Gets the sort column name. Default is "ScheduledAt".
    /// </summary>
    public string SortBy { get; init; } = "ScheduledAt";

    /// <summary>
    /// Gets whether to sort in descending order. Default is true (newest first).
    /// </summary>
    public bool SortDescending { get; init; } = true;

    /// <summary>
    /// Calculates the number of active (non-default) filters.
    /// </summary>
    /// <returns>The count of active filters.</returns>
    public int GetActiveFilterCount()
    {
        int count = 0;
        if (Statuses.Count > 0 && Statuses.Count < 7) count++; // Not all statuses selected
        if (StartDate.HasValue) count++;
        if (EndDate.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(AccusedUser)) count++;
        if (!string.IsNullOrWhiteSpace(InitiatorUser)) count++;
        if (MinVoteCount.HasValue && MinVoteCount > 0) count++;
        if (!string.IsNullOrWhiteSpace(Keyword)) count++;
        return count;
    }

    /// <summary>
    /// Creates a filter state from the DTO.
    /// </summary>
    /// <param name="dto">The filter DTO from the service.</param>
    /// <returns>A new RatWatchIncidentFilterState instance.</returns>
    public static RatWatchIncidentFilterState FromDto(RatWatchIncidentFilterDto dto)
    {
        return new RatWatchIncidentFilterState
        {
            Statuses = dto.Statuses?.ToList() ?? new List<RatWatchStatus>(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AccusedUser = dto.AccusedUser,
            InitiatorUser = dto.InitiatorUser,
            MinVoteCount = dto.MinVoteCount,
            Keyword = dto.Keyword,
            SortBy = dto.SortBy,
            SortDescending = dto.SortDescending
        };
    }
}
