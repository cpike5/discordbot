using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for the local <see cref="LlmModel"/> catalog.
/// </summary>
public class LlmModelRepository : Repository<LlmModel>, ILlmModelRepository
{
    private readonly ILogger<LlmModelRepository> _logger;

    public LlmModelRepository(
        BotDbContext context,
        ILogger<LlmModelRepository> logger,
        ILogger<Repository<LlmModel>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetVendorsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Select(m => m.Vendor)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModel>> QueryAsync(
        LlmModelCatalogFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(m =>
                EF.Functions.Like(m.Id, $"%{search}%") ||
                EF.Functions.Like(m.Name, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Vendor))
        {
            query = query.Where(m => m.Vendor == filter.Vendor);
        }

        if (filter.EnabledOnly)
        {
            query = query.Where(m => m.IsEnabled);
        }

        if (filter.AvailableOnly)
        {
            query = query.Where(m => m.IsAvailable);
        }

        if (filter.ToolsOnly)
        {
            query = query.Where(m => m.SupportsTools);
        }

        query = (filter.SortBy, filter.Descending) switch
        {
            (LlmModelSortBy.Name, false) => query.OrderBy(m => m.Name),
            (LlmModelSortBy.Name, true) => query.OrderByDescending(m => m.Name),
            (LlmModelSortBy.Vendor, false) => query.OrderBy(m => m.Vendor).ThenBy(m => m.Name),
            (LlmModelSortBy.Vendor, true) => query.OrderByDescending(m => m.Vendor).ThenBy(m => m.Name),
            (LlmModelSortBy.PromptPrice, false) => query.OrderBy(m => m.PromptPricePerMillion),
            (LlmModelSortBy.PromptPrice, true) => query.OrderByDescending(m => m.PromptPricePerMillion),
            (LlmModelSortBy.CompletionPrice, false) => query.OrderBy(m => m.CompletionPricePerMillion),
            (LlmModelSortBy.CompletionPrice, true) => query.OrderByDescending(m => m.CompletionPricePerMillion),
            (LlmModelSortBy.ContextLength, false) => query.OrderBy(m => m.ContextLength),
            (LlmModelSortBy.ContextLength, true) => query.OrderByDescending(m => m.ContextLength),
            (LlmModelSortBy.ReleasedAt, false) => query.OrderBy(m => m.ReleasedAt),
            (LlmModelSortBy.ReleasedAt, true) => query.OrderByDescending(m => m.ReleasedAt),
            _ => query.OrderBy(m => m.Name),
        };

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastRefreshAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Select(m => (DateTime?)m.LastSeenAt)
            .MaxAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> MarkUnavailableExceptAsync(
        IReadOnlyCollection<string> presentSlugs,
        CancellationToken cancellationToken = default)
    {
        if (presentSlugs.Count == 0)
        {
            // An empty set must never be read as "nothing is present" - that would mark the entire
            // catalog unavailable. Callers should not reach here with an empty fetched catalog at
            // all; this guard is the defensive backstop.
            _logger.LogDebug("MarkUnavailableExceptAsync called with an empty slug set; no-op");
            return 0;
        }

        var changed = await MarkUnavailableExceptCoreAsync(presentSlugs, cancellationToken);

        if (changed > 0)
        {
            await Context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Marked {Count} LLM model(s) unavailable", changed);
        return changed;
    }

    /// <inheritdoc />
    public async Task<(int Added, int Updated, int Removed)> UpsertRangeAsync(
        IReadOnlyCollection<LlmCatalogModel> fetchedModels,
        DateTime fetchedAt,
        CancellationToken cancellationToken = default)
    {
        var fetchedSlugs = fetchedModels.Select(m => m.Id).ToHashSet();

        var existingById = await DbSet
            .Where(m => fetchedSlugs.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var model in fetchedModels)
        {
            if (existingById.TryGetValue(model.Id, out var row))
            {
                ApplyCatalogFields(row, model, fetchedAt);
                row.IsAvailable = true;
                updated++;
            }
            else
            {
                var newRow = new LlmModel { Id = model.Id, FirstSeenAt = fetchedAt };
                ApplyCatalogFields(newRow, model, fetchedAt);
                DbSet.Add(newRow);
                added++;
            }
        }

        // Reuses the same "stale rows go unavailable" rule as MarkUnavailableExceptAsync, but without
        // its own SaveChangesAsync - everything above and below persists together in one round-trip.
        var removed = await MarkUnavailableExceptCoreAsync(fetchedSlugs, cancellationToken);

        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Upserted LLM catalog: {Added} added, {Updated} updated, {Removed} marked unavailable",
            added, updated, removed);

        return (added, updated, removed);
    }

    private async Task<int> MarkUnavailableExceptCoreAsync(
        IReadOnlyCollection<string> presentSlugs, CancellationToken cancellationToken)
    {
        var stale = await DbSet
            .Where(m => m.IsAvailable && !presentSlugs.Contains(m.Id))
            .ToListAsync(cancellationToken);

        foreach (var model in stale)
        {
            model.IsAvailable = false;
        }

        return stale.Count;
    }

    private static void ApplyCatalogFields(LlmModel row, LlmCatalogModel model, DateTime fetchedAt)
    {
        row.Name = model.Name;
        row.Description = model.Description;
        row.Vendor = model.Vendor;
        row.ContextLength = model.ContextLength;
        row.PromptPricePerMillion = model.PromptPricePerMillion;
        row.CompletionPricePerMillion = model.CompletionPricePerMillion;
        row.CacheReadPricePerMillion = model.CacheReadPricePerMillion;
        row.CacheWritePricePerMillion = model.CacheWritePricePerMillion;
        row.SupportsTools = model.SupportsTools;
        row.SupportsImages = model.SupportsImages;
        row.ReleasedAt = model.ReleasedAt;
        row.LastSeenAt = fetchedAt;
    }
}
