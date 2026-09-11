using System.Collections.Concurrent;
using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Agents.Configuration;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="ILlmModelResolver" />
/// <remarks>
/// Registered as a singleton (needs no API key - it only reads settings/options/catalog, never
/// calls OpenRouter). Per-mode results are cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// and invalidated whenever <see cref="ISettingsService.SettingsChanged"/> reports one of the
/// three mode setting keys among its <c>UpdatedKeys</c>, so a save through the AI Models tab takes
/// effect on the next message with no restart. Scoped services (<see cref="ILlmModelRepository"/>)
/// are resolved per call via <see cref="IServiceScopeFactory"/>, the same pattern
/// <c>SettingsService</c> uses to stay a singleton.
/// </remarks>
public class LlmModelResolver : ILlmModelResolver, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsService _settingsService;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly IOptions<DmAssistantOptions> _dmAssistantOptions;
    private readonly IOptions<FeatureRequestsOptions> _featureRequestsOptions;
    private readonly IOptions<OpenRouterOptions> _openRouterOptions;
    private readonly ILogger<LlmModelResolver> _logger;

    private readonly ConcurrentDictionary<LlmMode, LlmResolvedModel> _cache = new();

    /// <summary>Slugs already warned about being resolved-but-not-enabled, so the warning logs once per slug.</summary>
    private readonly ConcurrentDictionary<string, byte> _warnedSlugs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Slugs already warned about being unknown to the catalog, so that warning also logs once per slug.</summary>
    private readonly ConcurrentDictionary<string, byte> _warnedUnknownSlugs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set true after the first repository failure, so later failures for any slug log at Debug only.</summary>
    private int _catalogLookupFailureWarned;

    /// <summary>
    /// Bumped on every settings-changed invalidation. Read before a resolve starts and compared
    /// after it finishes so a resolve that was invalidated mid-flight by a concurrent settings
    /// change never re-populates the cache with a stale value (see <see cref="ResolveAsync"/>).
    /// </summary>
    private long _generation;

    public LlmModelResolver(
        IServiceScopeFactory scopeFactory,
        ISettingsService settingsService,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<DmAssistantOptions> dmAssistantOptions,
        IOptions<FeatureRequestsOptions> featureRequestsOptions,
        IOptions<OpenRouterOptions> openRouterOptions,
        ILogger<LlmModelResolver> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _assistantOptions = assistantOptions ?? throw new ArgumentNullException(nameof(assistantOptions));
        _dmAssistantOptions = dmAssistantOptions ?? throw new ArgumentNullException(nameof(dmAssistantOptions));
        _featureRequestsOptions = featureRequestsOptions
            ?? throw new ArgumentNullException(nameof(featureRequestsOptions));
        _openRouterOptions = openRouterOptions ?? throw new ArgumentNullException(nameof(openRouterOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <inheritdoc />
    public async Task<LlmResolvedModel> ResolveAsync(LlmMode mode, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(mode, out var cached))
        {
            return cached;
        }

        // Snapshot the generation before doing any awaited work. If a settings change invalidates
        // the cache while this resolve is in flight (e.g. an admin saves a new default between the
        // GetStoredValueAsync call below and this method returning), the generation will have moved
        // on by the time we get here, and the result below - built from data that was already stale
        // the moment it was read - must not be written back into the cache: that would silently
        // resurrect a value the invalidation was meant to evict, until the next unrelated change.
        var generationAtStart = Interlocked.Read(ref _generation);

        var resolved = await ResolveUncachedAsync(mode, cancellationToken);

        if (Interlocked.Read(ref _generation) == generationAtStart)
        {
            _cache[mode] = resolved;
        }

        return resolved;
    }

    private async Task<LlmResolvedModel> ResolveUncachedAsync(LlmMode mode, CancellationToken cancellationToken)
    {
        var key = LlmModeSettings.KeyFor(mode);
        var configuredSlug = GetConfiguredSlug(mode);

        // The "configured" slug regardless of a DB override: the bound options value, or the
        // last-resort OpenRouter:DefaultModel fallback when nothing is configured. Computed up
        // front so it is populated on LlmResolvedModel even when a DB row wins below.
        var effectiveConfiguredSlug = string.IsNullOrWhiteSpace(configuredSlug)
            ? _openRouterOptions.Value.DefaultModel
            : configuredSlug;

        string slug;
        LlmModelResolutionSource source;

        var storedValue = await _settingsService.GetStoredValueAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(storedValue))
        {
            slug = storedValue;
            source = LlmModelResolutionSource.Database;
        }
        else if (!string.IsNullOrWhiteSpace(configuredSlug))
        {
            slug = configuredSlug;
            source = LlmModelResolutionSource.Configuration;
        }
        else
        {
            slug = _openRouterOptions.Value.DefaultModel;
            source = LlmModelResolutionSource.Fallback;
        }

        var (isEnabled, isAvailable, pricing) = await LookupCatalogAsync(slug, cancellationToken);

        if (isEnabled == false && _warnedSlugs.TryAdd(slug, 0))
        {
            _logger.LogWarning(
                "LLM mode {Mode} resolved to slug {Slug} (source {Source}), which is not enabled in the " +
                "catalog allowlist. The request will still be sent; an admin should enable it or change " +
                "the mode's default from the AI Models tab.",
                mode, slug, source);
        }
        else if (isEnabled == null && _warnedUnknownSlugs.TryAdd(slug, 0))
        {
            _logger.LogWarning(
                "LLM mode {Mode} resolved to slug {Slug} (source {Source}), which is unknown to the local " +
                "catalog (never seen by a refresh, or the catalog lookup failed). The request will still be " +
                "sent.",
                mode, slug, source);
        }

        return new LlmResolvedModel
        {
            Slug = slug,
            Source = source,
            ConfiguredSlug = effectiveConfiguredSlug,
            IsEnabled = isEnabled,
            IsAvailable = isAvailable,
            Pricing = pricing
        };
    }

    private string? GetConfiguredSlug(LlmMode mode) => mode switch
    {
        LlmMode.GuildAssistant => _assistantOptions.Value.Sampling.Model,
        LlmMode.DmAssistant => _dmAssistantOptions.Value.Model,
        LlmMode.FeatureRequests => _featureRequestsOptions.Value.RequirementsGatheringModel,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown LLM mode.")
    };

    private async Task<(bool? IsEnabled, bool? IsAvailable, LlmCatalogPricing? Pricing)> LookupCatalogAsync(
        string slug, CancellationToken cancellationToken)
    {
        LlmModel? model;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILlmModelRepository>();
            model = await repository.GetByIdAsync(slug, cancellationToken);
        }
        catch (Exception ex)
        {
            // The catalog is a nice-to-have (enabled state, pricing) on top of the slug decision
            // above, which is already made. A DB hiccup here must not stop the mode's message from
            // sending - fall back to "unknown" catalog state and let the caller use its configured
            // cost rates. Warn once for the first failure, then drop to Debug so a sustained outage
            // doesn't spam the log on every message.
            if (Interlocked.CompareExchange(ref _catalogLookupFailureWarned, 1, 0) == 0)
            {
                _logger.LogWarning(ex,
                    "LLM catalog lookup failed for slug {Slug}; resolving without catalog state. " +
                    "Further catalog lookup failures will be logged at Debug.", slug);
            }
            else
            {
                _logger.LogDebug(ex, "LLM catalog lookup failed for slug {Slug}; resolving without catalog state.", slug);
            }

            return (null, null, null);
        }

        if (model is null)
        {
            return (null, null, null);
        }

        LlmCatalogPricing? pricing = null;
        if (model.PromptPricePerMillion is not null || model.CompletionPricePerMillion is not null
            || model.CacheReadPricePerMillion is not null || model.CacheWritePricePerMillion is not null)
        {
            pricing = new LlmCatalogPricing
            {
                PromptPricePerMillion = model.PromptPricePerMillion,
                CompletionPricePerMillion = model.CompletionPricePerMillion,
                CacheReadPricePerMillion = model.CacheReadPricePerMillion,
                CacheWritePricePerMillion = model.CacheWritePricePerMillion
            };
        }

        return (model.IsEnabled, model.IsAvailable, pricing);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        foreach (var mode in LlmModeSettings.All)
        {
            if (e.UpdatedKeys.Contains(LlmModeSettings.KeyFor(mode)))
            {
                Interlocked.Increment(ref _generation);
                _cache.TryRemove(mode, out _);
            }
        }
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
