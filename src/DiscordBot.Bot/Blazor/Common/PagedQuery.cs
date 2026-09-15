namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Standardised paging/sort state for a guild list page (Phase 4 cluster 4b,
/// docs/plans/blazor-port-plan.md "Standardise on one pagination state type here"). A page binds
/// <see cref="PageNumber"/>/<see cref="PageSize"/>/<see cref="SortBy"/>/<see cref="SortDescending"/>
/// via <c>[SupplyParameterFromQuery]</c> using the query names <see cref="ToQueryString"/> writes
/// (<c>pageNumber</c>, <c>pageSize</c>, <c>sortBy</c>, <c>sortDescending</c> by default), builds a
/// <see cref="PagedQuery"/> with <see cref="FromQuery"/> once per load, and passes
/// <see cref="ToQueryString"/>'s result to <c>Blazor/Shared/Navigation/Pagination.razor</c>'s
/// <c>BaseUrl</c> in link mode. Filters (a status enum, a search term) are not part of this type -
/// each page keeps those as its own <c>[SupplyParameterFromQuery]</c> parameters and folds them
/// into the query string itself, the same way <c>Blazor/Pages/Admin/Users/Index.razor.cs</c>
/// builds its filtered URL today.
/// </summary>
/// <param name="PageNumber">1-based page number, always &gt;= 1 after <see cref="FromQuery"/> clamping.</param>
/// <param name="PageSize">Items per page, clamped to [1, 100] by <see cref="FromQuery"/>.</param>
/// <param name="SortBy">Optional sort column/field name.</param>
/// <param name="SortDescending">Whether <see cref="SortBy"/> sorts descending.</param>
public sealed record PagedQuery(int PageNumber, int PageSize, string? SortBy = null, bool SortDescending = false)
{
    /// <summary>Default page size used by <see cref="FromQuery"/> when the caller doesn't pass one and the query supplied none.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Hard upper bound on <see cref="PageSize"/>, matching every legacy page model's own "page size can't exceed 100" clamp.</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Builds a <see cref="PagedQuery"/> from raw, possibly-absent or out-of-range query values,
    /// clamping <paramref name="pageNumber"/> up to 1 and <paramref name="pageSize"/> into
    /// [1, <see cref="MaxPageSize"/>] (falling back to <paramref name="defaultPageSize"/> when
    /// unset or non-positive) - the same validation every ported page's <c>OnGetAsync</c> used to
    /// do inline (<c>if (page &lt; 1) page = 1; if (pageSize &lt; 1 || pageSize &gt; 100) pageSize = 20;</c>).
    /// </summary>
    /// <param name="pageNumber">The raw <c>pageNumber</c> query value, or <see langword="null"/> when absent.</param>
    /// <param name="pageSize">The raw <c>pageSize</c> query value, or <see langword="null"/> when absent.</param>
    /// <param name="sortBy">Optional sort field, passed through unchanged.</param>
    /// <param name="sortDescending">Optional sort direction, passed through unchanged.</param>
    /// <param name="defaultPageSize">The page size to use when <paramref name="pageSize"/> is absent or invalid.</param>
    /// <param name="legacyPage">
    /// A fallback 1-based page number from the legacy <c>?page=</c> query name (bookmarks and the
    /// <c>Guilds/Details</c> widget links predate the <c>pageNumber</c> convention). Used only when
    /// <paramref name="pageNumber"/> itself is absent.
    /// </param>
    public static PagedQuery FromQuery(
        int? pageNumber,
        int? pageSize,
        string? sortBy = null,
        bool sortDescending = false,
        int defaultPageSize = DefaultPageSize,
        int? legacyPage = null)
    {
        var page = pageNumber ?? legacyPage ?? 1;
        if (page < 1)
        {
            page = 1;
        }

        var size = pageSize ?? defaultPageSize;
        if (size < 1 || size > MaxPageSize)
        {
            size = defaultPageSize;
        }

        return new PagedQuery(page, size, sortBy, sortDescending);
    }

    /// <summary>
    /// Builds the query-string portion (including the leading <c>?</c>, or <see cref="string.Empty"/>
    /// when every value is at its default) for this state, using the standard parameter names
    /// unless overridden. Omits a value that's at its implicit default (<see cref="PageNumber"/> ==
    /// 1, <see cref="PageSize"/> == <paramref name="defaultPageSize"/>, no <see cref="SortBy"/>,
    /// <see cref="SortDescending"/> == <see langword="false"/>) to keep URLs short, matching every
    /// legacy page's own "only emit a route value that differs from the default" convention.
    /// </summary>
    public string ToQueryString(
        string pageParameterName = "pageNumber",
        string pageSizeParameterName = "pageSize",
        string sortByParameterName = "sortBy",
        string sortDescendingParameterName = "sortDescending",
        int defaultPageSize = DefaultPageSize)
    {
        var parts = new List<string>();

        if (PageNumber > 1)
        {
            parts.Add($"{pageParameterName}={PageNumber}");
        }

        if (PageSize != defaultPageSize)
        {
            parts.Add($"{pageSizeParameterName}={PageSize}");
        }

        if (!string.IsNullOrEmpty(SortBy))
        {
            parts.Add($"{sortByParameterName}={Uri.EscapeDataString(SortBy)}");
        }

        if (SortDescending)
        {
            parts.Add($"{sortDescendingParameterName}=true");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
