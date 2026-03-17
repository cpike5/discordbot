using Discord;
using DiscordBot.Bot.Components;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides utility methods for Discord command module pagination,
/// including page calculation and pagination button building.
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Calculates the current page and total number of pages for a paginated list.
    /// </summary>
    /// <param name="totalItems">Total number of items across all pages.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="requestedPage">The requested page number (1-based).</param>
    /// <returns>
    /// A tuple containing the clamped current page and the total number of pages.
    /// Both values are at minimum 1.
    /// </returns>
    public static (int Page, int TotalPages) CalculatePages(int totalItems, int pageSize, int requestedPage)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize));
        var page = Math.Clamp(requestedPage, 1, totalPages);
        return (page, totalPages);
    }

    /// <summary>
    /// Builds a <see cref="ComponentBuilder"/> containing Previous and Next pagination buttons.
    /// Button custom IDs follow the codebase's <see cref="ComponentIdBuilder"/> pattern:
    /// <c>{handlerPrefix}:page:{userId}:{correlationId}:{prevPage|nextPage}</c>.
    /// </summary>
    /// <param name="handlerPrefix">The handler name used to route the component interaction (e.g., "modlog").</param>
    /// <param name="currentPage">The currently displayed page number (1-based).</param>
    /// <param name="totalPages">The total number of pages.</param>
    /// <param name="userId">The Discord user ID permitted to interact with the buttons.</param>
    /// <param name="correlationId">
    /// An optional correlation ID for state lookup via <c>IInteractionStateService</c>.
    /// When <c>null</c>, a new <see cref="Guid"/> is generated.
    /// </param>
    /// <returns>A <see cref="ComponentBuilder"/> with Previous and Next buttons.</returns>
    public static ComponentBuilder BuildPaginationButtons(
        string handlerPrefix,
        int currentPage,
        int totalPages,
        ulong userId,
        string? correlationId = null)
    {
        var resolvedCorrelationId = correlationId ?? Guid.NewGuid().ToString("N");

        var prevButtonId = ComponentIdBuilder.Build(
            handlerPrefix,
            "page",
            userId,
            resolvedCorrelationId,
            Math.Max(1, currentPage - 1).ToString());

        var nextButtonId = ComponentIdBuilder.Build(
            handlerPrefix,
            "page",
            userId,
            resolvedCorrelationId,
            Math.Min(totalPages, currentPage + 1).ToString());

        return new ComponentBuilder()
            .WithButton("◀ Previous", prevButtonId, ButtonStyle.Secondary, disabled: currentPage <= 1)
            .WithButton("Next ▶", nextButtonId, ButtonStyle.Secondary, disabled: currentPage >= totalPages);
    }
}
