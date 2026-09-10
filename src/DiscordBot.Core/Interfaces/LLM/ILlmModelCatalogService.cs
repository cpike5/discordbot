using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Owns the local <see cref="LlmModel"/> catalog: pulling it from OpenRouter, listing/filtering it
/// for the admin portal, and the enable/disable allowlist. A catalog refresh never changes
/// <see cref="LlmModel.IsEnabled"/> except for the one-time bootstrap on the very first refresh
/// (see <see cref="RefreshAsync"/>).
/// </summary>
public interface ILlmModelCatalogService
{
    /// <summary>
    /// Fetches the current OpenRouter catalog and upserts it by slug: new slugs are inserted with
    /// <c>IsEnabled = false</c>, existing slugs have their descriptive fields and
    /// <see cref="LlmModel.LastSeenAt"/> refreshed, and slugs no longer returned are marked
    /// <see cref="LlmModel.IsAvailable"/> = false (never deleted, never re-disabled if already
    /// enabled). <see cref="LlmModel.IsEnabled"/> is otherwise never touched by this method.
    /// <para>
    /// Exception: if the table was empty before this call (i.e. this is the first refresh ever),
    /// the slugs currently configured for the guild assistant, DM assistant, and feature-request
    /// modes are enabled, provided they appear in the fetched catalog - so upgrades keep working
    /// and the mode pickers are not empty. This bootstrap never runs again afterwards.
    /// </para>
    /// Audited under <c>AuditLogCategory.Configuration</c> / <c>AuditLogAction.LlmCatalogRefreshed</c>.
    /// </summary>
    /// <param name="userId">The admin who triggered the refresh, or null for a scheduled/system refresh.</param>
    Task<LlmCatalogRefreshResult> RefreshAsync(string? userId, CancellationToken cancellationToken = default);

    /// <summary>Lists the catalog filtered and sorted per <paramref name="filter"/>.</summary>
    Task<IReadOnlyList<LlmModel>> GetCatalogAsync(
        LlmModelCatalogFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Lists only enabled models, for the per-mode model pickers.</summary>
    Task<IReadOnlyList<LlmModel>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the distinct set of vendors across the whole catalog (unfiltered, alphabetical) - for the
    /// admin portal's vendor filter, without requiring a second unfiltered <see cref="GetCatalogAsync"/>
    /// call.
    /// </summary>
    Task<IReadOnlyList<string>> GetVendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables one model. Disabling is refused (a failed result, not an exception) when
    /// <paramref name="slug"/> is currently the configured default for any mode (guild assistant, DM
    /// assistant, feature requests) - the caller must switch that mode's default first. Audited under
    /// <c>AuditLogAction.LlmModelEnabled</c> / <c>LlmModelDisabled</c>.
    /// </summary>
    /// <param name="userId">The admin performing the change, or null for a system action.</param>
    Task<LlmModelEnableResult> SetEnabledAsync(
        string slug,
        bool enabled,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent catalog refresh time, or null if the catalog has never been refreshed.</summary>
    Task<DateTime?> GetLastRefreshAsync(CancellationToken cancellationToken = default);
}
