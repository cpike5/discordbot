// src/DiscordBot.Bot/ViewModels/Components/PaginationViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_Pagination</c>. The derived members below hold every rule about empty,
/// single-page and out-of-range states, so the partial only draws what they say and the rules
/// can be unit tested.
/// </summary>
public record PaginationViewModel
{
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;

    /// <summary>
    /// Total items across all pages. Leave it at 0 when the caller does not know it: with more than
    /// one page the summary then reads "Page X of Y" instead of an item range.
    /// </summary>
    public int TotalItems { get; init; } = 0;
    public int PageSize { get; init; } = 10;
    public int[] PageSizeOptions { get; init; } = new[] { 10, 25, 50, 100 };
    public PaginationStyle Style { get; init; } = PaginationStyle.Full;
    public bool ShowPageSizeSelector { get; init; } = false;
    public bool ShowItemCount { get; init; } = false;
    public bool ShowFirstLast { get; init; } = true;
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// The query parameter that carries the page. Never <c>page</c> for a Razor Page link built
    /// from route values (Razor Pages reserves it for the page name); the default is kept for
    /// callers that pass a prebuilt query string. Use <c>pageNumber</c>.
    /// </summary>
    public string PageParameterName { get; init; } = "page";
    public string PageSizeParameterName { get; init; } = "pageSize";

    /// <summary>The accessible name of the pagination landmark. Give each one on a page its own.</summary>
    public string AriaLabel { get; init; } = "Pagination";

    /// <summary>The page being shown, kept within 1 and the last page.</summary>
    public int EffectivePage => Math.Clamp(CurrentPage, 1, Math.Max(1, TotalPages));

    /// <summary>True when there is nothing to page: no items and at most one page.</summary>
    public bool IsEmpty => TotalItems <= 0 && TotalPages <= 1;

    /// <summary>True when there is more than one page to move between.</summary>
    public bool HasMultiplePages => TotalPages > 1;

    /// <summary>True when the item total is known, so a range can be shown.</summary>
    public bool HasKnownTotal => TotalItems > 0;

    public bool IsFirstPage => EffectivePage <= 1;
    public bool IsLastPage => EffectivePage >= TotalPages;

    /// <summary>1-based index of the first item on the page; 0 when there are no items.</summary>
    public int FirstItem => HasKnownTotal
        ? Math.Min((EffectivePage - 1) * Math.Max(1, PageSize) + 1, TotalItems)
        : 0;

    /// <summary>1-based index of the last item on the page; 0 when there are no items.</summary>
    public int LastItem => HasKnownTotal
        ? Math.Min(EffectivePage * Math.Max(1, PageSize), TotalItems)
        : 0;

    /// <summary>
    /// The page selector is only useful when the smallest page size would split the results.
    /// </summary>
    public bool ShowsPageSizeSelector =>
        ShowPageSizeSelector && PageSizeOptions.Length > 0 && !IsEmpty
        && (HasMultiplePages || TotalItems > PageSizeOptions.Min());

    /// <summary>
    /// The page numbers to draw, with null for an ellipsis: all of them up to 7 pages, else
    /// the first, the last and a window around the current page (<c>1 … 4 5 6 … 10</c>).
    /// </summary>
    public IReadOnlyList<int?> VisiblePages()
    {
        var pages = new List<int?>();
        var total = Math.Max(1, TotalPages);
        var current = EffectivePage;

        if (total <= 7)
        {
            for (var i = 1; i <= total; i++)
            {
                pages.Add(i);
            }

            return pages;
        }

        pages.Add(1);
        if (current <= 4)
        {
            for (var i = 2; i <= Math.Min(5, total - 1); i++)
            {
                pages.Add(i);
            }

            pages.Add(null);
        }
        else if (current >= total - 3)
        {
            pages.Add(null);
            for (var i = Math.Max(2, total - 4); i < total; i++)
            {
                pages.Add(i);
            }
        }
        else
        {
            pages.Add(null);
            for (var i = current - 1; i <= current + 1; i++)
            {
                pages.Add(i);
            }

            pages.Add(null);
        }

        pages.Add(total);
        return pages;
    }
}

public enum PaginationStyle
{
    Full,       // First, Prev, page numbers, Next, Last
    Simple,     // Just Prev/Next buttons
    Compact,    // Prev, Page X of Y, Next
    Bordered    // Connected button group style
}
