using System.Security.Claims;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for unified search operations across multiple categories.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Performs a unified search across all applicable categories based on the query parameters.
    /// </summary>
    /// <param name="query">The search query containing search term and filters.</param>
    /// <param name="user">The current user's claims principal for authorization checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unified search result containing results from all searched categories.</returns>
    Task<UnifiedSearchResultDto> SearchAsync(
        SearchQueryDto query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a search within a specific category.
    /// </summary>
    /// <param name="category">The category to search within.</param>
    /// <param name="searchTerm">The search term to query.</param>
    /// <param name="maxResults">The maximum number of results to return.</param>
    /// <param name="user">The current user's claims principal for authorization checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results for the specified category.</returns>
    Task<SearchCategoryResult> SearchCategoryAsync(
        SearchCategory category,
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
