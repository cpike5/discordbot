using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Owns the local <see cref="LlmModel"/> catalog: refreshing it from OpenRouter, listing/filtering it,
/// and the admin enable/disable allowlist. See <see cref="ILlmModelCatalogService"/> for the contract.
/// </summary>
/// <remarks>
/// Bootstrap seeding and the "refuse to disable a current mode default" check both need each
/// mode's currently-configured slug. Both go through <see cref="ILlmModelResolver"/> - the same
/// single resolution path message-send time uses - rather than reading
/// <c>ISettingsService</c>/<c>IOptions&lt;T&gt;</c> directly a second time. This is not circular:
/// <see cref="LlmModelResolver"/> depends only on <see cref="IServiceScopeFactory"/> (to reach
/// <see cref="ILlmModelRepository"/> per call) and settings/options, never on this service.
/// </remarks>
public class LlmModelCatalogService : ILlmModelCatalogService
{
    private readonly ILlmModelRepository _repository;
    private readonly IOpenRouterModelCatalogClient _catalogClient;
    private readonly IAuditLogService _auditLogService;
    private readonly ILlmModelResolver _modelResolver;
    private readonly ILogger<LlmModelCatalogService> _logger;

    public LlmModelCatalogService(
        ILlmModelRepository repository,
        IOpenRouterModelCatalogClient catalogClient,
        IAuditLogService auditLogService,
        ILlmModelResolver modelResolver,
        ILogger<LlmModelCatalogService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _modelResolver = modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<LlmCatalogRefreshResult> RefreshAsync(
        string? userId, CancellationToken cancellationToken = default)
    {
        var fetched = await _catalogClient.GetModelsAsync(cancellationToken);
        var fetchedAt = DateTime.UtcNow;

        if (fetched.Count == 0)
        {
            // An empty {"data":[]} from OpenRouter must never be read as "nothing exists" - that
            // would mark the whole local catalog unavailable. Bail out before any upsert or
            // mark-unavailable step; leave the existing catalog exactly as it was.
            _logger.LogWarning(
                "OpenRouter catalog fetch returned zero models; leaving the existing catalog untouched");
            return new LlmCatalogRefreshResult
            {
                Added = 0,
                Updated = 0,
                Removed = 0,
                FetchedAt = fetchedAt,
            };
        }

        var wasEmpty = await _repository.GetLastRefreshAsync(cancellationToken) is null;

        var (added, updated, removed) = await _repository.UpsertRangeAsync(fetched, fetchedAt, cancellationToken);

        if (wasEmpty)
        {
            var fetchedSlugs = fetched.Select(m => m.Id).ToHashSet();
            await BootstrapEnableConfiguredModelsAsync(fetchedSlugs, cancellationToken);
        }

        var result = new LlmCatalogRefreshResult
        {
            Added = added,
            Updated = updated,
            Removed = removed,
            FetchedAt = fetchedAt,
        };

        var refreshBuilder = _auditLogService.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.LlmCatalogRefreshed);
        refreshBuilder = string.IsNullOrWhiteSpace(userId) ? refreshBuilder.BySystem() : refreshBuilder.ByUser(userId);
        await refreshBuilder
            .WithDetails(new { added, updated, removed, fetchedAt })
            .LogAsync(cancellationToken);

        _logger.LogInformation(
            "LLM catalog refresh: {Added} added, {Updated} updated, {Removed} marked unavailable",
            added, updated, removed);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModel>> GetCatalogAsync(
        LlmModelCatalogFilter filter, CancellationToken cancellationToken = default)
    {
        return await _repository.QueryAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetEnabledAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetVendorsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetVendorsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LlmModelEnableResult> SetEnabledAsync(
        string slug, bool enabled, string? userId, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAsync(slug, cancellationToken);
        if (model is null)
        {
            return LlmModelEnableResult.Fail($"Model '{slug}' is not in the catalog.");
        }

        if (model.IsEnabled == enabled)
        {
            // No-op: nothing changed, so nothing to audit.
            return LlmModelEnableResult.Ok();
        }

        if (!enabled)
        {
            var modeDefaults = await GetCurrentModeDefaultsAsync(cancellationToken);
            if (modeDefaults.Contains(slug))
            {
                return LlmModelEnableResult.Fail(
                    $"'{slug}' is the current default model for one or more modes. Change that mode's " +
                    "default before disabling it.");
            }
        }

        model.IsEnabled = enabled;
        if (enabled)
        {
            model.EnabledAt = DateTime.UtcNow;
            model.EnabledBy = userId;
        }
        else
        {
            model.EnabledAt = null;
            model.EnabledBy = null;
        }

        await _repository.UpdateAsync(model, cancellationToken);

        var enableBuilder = _auditLogService.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(enabled ? AuditLogAction.LlmModelEnabled : AuditLogAction.LlmModelDisabled);
        enableBuilder = string.IsNullOrWhiteSpace(userId) ? enableBuilder.BySystem() : enableBuilder.ByUser(userId);
        await enableBuilder
            .OnTarget("LlmModel", slug)
            .LogAsync(cancellationToken);

        return LlmModelEnableResult.Ok();
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastRefreshAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetLastRefreshAsync(cancellationToken);
    }

    /// <summary>
    /// One-time bootstrap on the very first refresh: enables the slugs currently configured for the
    /// three modes, provided they were actually returned by this refresh. Never runs again - later
    /// refreshes leave newly-arriving slugs disabled.
    /// </summary>
    private async Task BootstrapEnableConfiguredModelsAsync(
        HashSet<string> fetchedSlugs, CancellationToken cancellationToken)
    {
        var configuredSlugs = new List<string>();
        foreach (var mode in LlmModeSettings.All)
        {
            var resolved = await _modelResolver.ResolveAsync(mode, cancellationToken);
            configuredSlugs.Add(resolved.Slug);
        }

        foreach (var slug in configuredSlugs.Distinct())
        {
            if (string.IsNullOrWhiteSpace(slug) || !fetchedSlugs.Contains(slug))
            {
                continue;
            }

            var model = await _repository.GetByIdAsync(slug, cancellationToken);
            if (model is null || model.IsEnabled)
            {
                continue;
            }

            model.IsEnabled = true;
            model.EnabledAt = DateTime.UtcNow;
            model.EnabledBy = null; // System bootstrap, not an admin action.
            await _repository.UpdateAsync(model, cancellationToken);

            _logger.LogInformation("Bootstrap-enabled configured LLM model {Slug} on first catalog refresh", slug);
        }
    }

    /// <summary>
    /// Resolves the three modes' current model slugs via <see cref="ILlmModelResolver"/> - the
    /// same single resolution path (DB override, then configuration, then fallback) used at
    /// message-send time.
    /// </summary>
    private async Task<HashSet<string>> GetCurrentModeDefaultsAsync(CancellationToken cancellationToken)
    {
        var results = new HashSet<string>();
        foreach (var mode in LlmModeSettings.All)
        {
            var resolved = await _modelResolver.ResolveAsync(mode, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved.Slug))
            {
                results.Add(resolved.Slug);
            }
        }

        return results;
    }
}
