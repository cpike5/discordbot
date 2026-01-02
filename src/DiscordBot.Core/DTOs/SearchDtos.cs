using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for search query parameters.
/// </summary>
public class SearchQueryDto
{
    /// <summary>
    /// Gets or sets the search term to query across all categories.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of results to return per category.
    /// Default is 5.
    /// </summary>
    public int MaxResultsPerCategory { get; set; } = 5;

    /// <summary>
    /// Gets or sets an optional category filter to restrict search to specific categories.
    /// If null, searches all categories.
    /// </summary>
    public SearchCategory? CategoryFilter { get; set; }
}

/// <summary>
/// Data transfer object for unified search results across all categories.
/// </summary>
public class UnifiedSearchResultDto
{
    /// <summary>
    /// Gets or sets the search term that was used.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the results for the Guilds category.
    /// </summary>
    public SearchCategoryResult Guilds { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the CommandLogs category.
    /// </summary>
    public SearchCategoryResult CommandLogs { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the Users category.
    /// </summary>
    public SearchCategoryResult Users { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the Commands category.
    /// </summary>
    public SearchCategoryResult Commands { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the AuditLogs category.
    /// </summary>
    public SearchCategoryResult AuditLogs { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the MessageLogs category.
    /// </summary>
    public SearchCategoryResult MessageLogs { get; set; } = new();

    /// <summary>
    /// Gets or sets the results for the Pages category.
    /// </summary>
    public SearchCategoryResult Pages { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether any results were found across all categories.
    /// </summary>
    public bool HasResults =>
        Guilds.Items.Count > 0 ||
        CommandLogs.Items.Count > 0 ||
        Users.Items.Count > 0 ||
        Commands.Items.Count > 0 ||
        AuditLogs.Items.Count > 0 ||
        MessageLogs.Items.Count > 0 ||
        Pages.Items.Count > 0;

    /// <summary>
    /// Gets the total number of results across all categories.
    /// </summary>
    public int TotalResultCount =>
        Guilds.Items.Count +
        CommandLogs.Items.Count +
        Users.Items.Count +
        Commands.Items.Count +
        AuditLogs.Items.Count +
        MessageLogs.Items.Count +
        Pages.Items.Count;
}

/// <summary>
/// Data transfer object for search results within a specific category.
/// </summary>
public class SearchCategoryResult
{
    /// <summary>
    /// Gets or sets the category these results belong to.
    /// </summary>
    public SearchCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the display name for this category.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of search result items.
    /// </summary>
    public List<SearchResultItemDto> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the total count of matching items (may be greater than Items.Count if limited).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are more results available beyond what's shown.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets the URL to view all results in this category.
    /// </summary>
    public string? ViewAllUrl { get; set; }
}

/// <summary>
/// Data transfer object for a single search result item.
/// </summary>
public class SearchResultItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this item.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the search result.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subtitle (secondary text) for the search result.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the description or snippet for the search result.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the icon URL for the search result.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the badge text to display (e.g., status, count).
    /// </summary>
    public string? BadgeText { get; set; }

    /// <summary>
    /// Gets or sets the badge variant/color (e.g., "success", "warning", "danger").
    /// </summary>
    public string? BadgeVariant { get; set; }

    /// <summary>
    /// Gets or sets the URL to navigate to when clicking this result.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevance score for ranking (0-100, higher is more relevant).
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Gets or sets an optional timestamp associated with this result.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets additional metadata as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
