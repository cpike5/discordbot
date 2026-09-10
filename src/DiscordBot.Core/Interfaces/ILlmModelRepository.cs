using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository for the local <see cref="LlmModel"/> catalog. The base <see cref="IRepository{T}"/>
/// members (<c>GetByIdAsync</c> by slug, <c>GetAllAsync</c>, <c>AddAsync</c>, <c>UpdateAsync</c>, ...)
/// cover the simple cases; the members here support the catalog service's refresh and filtered-list
/// operations.
/// </summary>
public interface ILlmModelRepository : IRepository<LlmModel>
{
    /// <summary>Gets every row currently enabled, for the per-mode model pickers.</summary>
    Task<IReadOnlyList<LlmModel>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the distinct set of vendors across the whole catalog (unfiltered), sorted alphabetically -
    /// for populating the vendor filter <c>&lt;select&gt;</c> without pulling every model column.
    /// </summary>
    Task<IReadOnlyList<string>> GetVendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets rows matching <paramref name="filter"/>'s search/vendor/flags, sorted as requested.</summary>
    Task<IReadOnlyList<LlmModel>> QueryAsync(
        LlmModelCatalogFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent <see cref="LlmModel.LastSeenAt"/> across every row, so the UI can show when the
    /// catalog was last refreshed. Null when the table is empty.
    /// </summary>
    Task<DateTime?> GetLastRefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every currently-available row whose slug is not in <paramref name="presentSlugs"/> as
    /// <see cref="LlmModel.IsAvailable"/> = false. Rows are never deleted. Returns the number of rows
    /// changed. A no-op (returns 0 without touching the database) when <paramref name="presentSlugs"/>
    /// is empty - callers refreshing from an empty fetched catalog should not reach this at all, but
    /// an empty set here must never be read as "nothing is present" and wipe the whole catalog.
    /// </summary>
    Task<int> MarkUnavailableExceptAsync(
        IReadOnlyCollection<string> presentSlugs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts every model in <paramref name="fetchedModels"/> in one unit of work: rows matching an
    /// existing slug are mutated on the tracked entity, unmatched slugs become new rows, every
    /// currently-available row whose slug is absent from the fetch is marked unavailable (same rule as
    /// <see cref="MarkUnavailableExceptAsync"/>), and the whole batch is persisted with a single
    /// <c>SaveChangesAsync</c> - not one round-trip per row. Callers must not pass an empty
    /// <paramref name="fetchedModels"/> (that must short-circuit before reaching here; see
    /// <see cref="LlmCatalogRefreshResult"/> callers).
    /// </summary>
    Task<(int Added, int Updated, int Removed)> UpsertRangeAsync(
        IReadOnlyCollection<LlmCatalogModel> fetchedModels,
        DateTime fetchedAt,
        CancellationToken cancellationToken = default);
}
