using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a paginated list of command logs.
/// </summary>
public record CommandLogListViewModel
{
    /// <summary>
    /// Gets the collection of command log items for the current page.
    /// </summary>
    public IReadOnlyList<CommandLogListItem> Logs { get; init; } = Array.Empty<CommandLogListItem>();

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; init; } = 1;

    /// <summary>
    /// Gets the total number of command log entries across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the page size (number of items per page).
    /// </summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Gets whether there is a next page available.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets the filter options applied to the command log list.
    /// </summary>
    public CommandLogFilterOptions Filters { get; init; } = new();

    /// <summary>
    /// Creates a <see cref="CommandLogListViewModel"/> from a paginated response.
    /// </summary>
    /// <param name="paginatedResponse">The paginated command log response.</param>
    /// <param name="filters">Optional filter options.</param>
    /// <returns>A new <see cref="CommandLogListViewModel"/> instance.</returns>
    public static CommandLogListViewModel FromPaginatedDto(
        PaginatedResponseDto<CommandLogDto> paginatedResponse,
        CommandLogFilterOptions? filters = null)
    {
        return new CommandLogListViewModel
        {
            Logs = paginatedResponse.Items.Select(CommandLogListItem.FromDto).ToList(),
            CurrentPage = paginatedResponse.Page,
            TotalPages = paginatedResponse.TotalPages,
            TotalCount = paginatedResponse.TotalCount,
            PageSize = paginatedResponse.PageSize,
            Filters = filters ?? new CommandLogFilterOptions()
        };
    }
}

/// <summary>
/// Represents a command log entry for list display.
/// </summary>
public record CommandLogListItem
{
    /// <summary>
    /// Gets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the guild name where the command was executed.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the username of the user who executed the command.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Gets whether the command executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a <see cref="CommandLogListItem"/> from a <see cref="CommandLogDto"/>.
    /// </summary>
    /// <param name="dto">The command log DTO to map from.</param>
    /// <returns>A new <see cref="CommandLogListItem"/> instance.</returns>
    public static CommandLogListItem FromDto(CommandLogDto dto)
    {
        return new CommandLogListItem
        {
            Id = dto.Id,
            GuildName = dto.GuildName ?? "Direct Message",
            Username = dto.Username ?? "Unknown",
            CommandName = dto.CommandName,
            ExecutedAt = dto.ExecutedAt,
            Success = dto.Success,
            ResponseTimeMs = dto.ResponseTimeMs,
            ErrorMessage = dto.ErrorMessage
        };
    }
}

/// <summary>
/// Represents filter options for command log queries.
/// </summary>
public record CommandLogFilterOptions
{
    /// <summary>
    /// Gets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets the user ID filter. Null means no filter.
    /// </summary>
    public ulong? UserId { get; init; }

    /// <summary>
    /// Gets the command name filter. Null or empty means no filter.
    /// </summary>
    public string? CommandName { get; init; }

    /// <summary>
    /// Gets the start date for date range filter. Null means no start date limit.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the end date for date range filter. Null means no end date limit.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets whether to filter for successful commands only. Null means no success filter.
    /// </summary>
    public bool? SuccessOnly { get; init; }

    /// <summary>
    /// Gets whether any filters are currently applied.
    /// </summary>
    public bool HasActiveFilters =>
        GuildId.HasValue ||
        UserId.HasValue ||
        !string.IsNullOrWhiteSpace(CommandName) ||
        StartDate.HasValue ||
        EndDate.HasValue ||
        SuccessOnly.HasValue;
}
