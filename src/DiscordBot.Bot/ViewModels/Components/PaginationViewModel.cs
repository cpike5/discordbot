// src/DiscordBot.Bot/ViewModels/Components/PaginationViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record PaginationViewModel
{
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalItems { get; init; } = 0;
    public int PageSize { get; init; } = 10;
    public int[] PageSizeOptions { get; init; } = new[] { 10, 25, 50, 100 };
    public PaginationStyle Style { get; init; } = PaginationStyle.Full;
    public bool ShowPageSizeSelector { get; init; } = false;
    public bool ShowItemCount { get; init; } = false;
    public bool ShowFirstLast { get; init; } = true;
    public string BaseUrl { get; init; } = string.Empty;
    public string PageParameterName { get; init; } = "page";
    public string PageSizeParameterName { get; init; } = "pageSize";
}

public enum PaginationStyle
{
    Full,       // First, Prev, page numbers, Next, Last
    Simple,     // Just Prev/Next buttons
    Compact,    // Prev, Page X of Y, Next
    Bordered    // Connected button group style
}
