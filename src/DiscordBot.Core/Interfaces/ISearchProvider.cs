using System.Security.Claims;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Contract for a single-category search provider.
/// Each implementation is responsible for searching one <see cref="SearchCategory"/>.
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// Gets the category this provider handles.
    /// </summary>
    SearchCategory Category { get; }

    /// <summary>
    /// Gets a value indicating whether this provider requires admin authorization.
    /// When <see langword="true"/>, the orchestrator will skip it for non-admin users.
    /// </summary>
    bool RequiresAdmin { get; }

    /// <summary>
    /// Searches this provider's category and returns the matching results.
    /// </summary>
    /// <param name="searchTerm">The pre-trimmed, lower-cased search term.</param>
    /// <param name="maxResults">Maximum number of items to return.</param>
    /// <param name="user">The current user's claims principal for per-item authorization checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SearchCategoryResult"/> with matching items.</returns>
    Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
