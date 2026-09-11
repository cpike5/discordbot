namespace DiscordBot.Core.DTOs;

/// <summary>
/// One page of results plus the totals a pager needs.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public record PagedResult<T>
{
    /// <summary>Items on the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Total items matching the query across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Current page number, 1-based.</summary>
    public int Page { get; init; }

    /// <summary>Items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages available.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
