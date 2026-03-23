namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for normalizing pagination query parameters.
/// </summary>
public static class PaginationQueryExtensions
{
    /// <summary>
    /// Normalizes pagination parameters, clamping page to a minimum of 1 and
    /// pageSize to a valid range between 1 and <paramref name="maxPageSize"/>.
    /// </summary>
    /// <param name="query">A tuple containing the raw page and pageSize values.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to 100.</param>
    /// <param name="defaultPageSize">The page size to use when the requested value is out of range. Defaults to 20.</param>
    /// <returns>A tuple with normalized (Page, PageSize) values.</returns>
    public static (int Page, int PageSize) Normalize(
        this (int page, int pageSize) query,
        int maxPageSize = 100,
        int defaultPageSize = 20)
    {
        var page = query.page < 1 ? 1 : query.page;
        var pageSize = query.pageSize < 1 || query.pageSize > maxPageSize
            ? defaultPageSize
            : query.pageSize;
        return (page, pageSize);
    }
}
