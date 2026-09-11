using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IToolAccessResolver" />
public class ToolAccessResolver : IToolAccessResolver
{
    /// <summary>Cache key prefix for a guild's resolved tool set.</summary>
    public const string CacheKeyPrefix = "assistant_tool_access:";

    /// <summary>
    /// Backstop expiry. Saves invalidate explicitly, so this only covers a write that bypassed
    /// <c>IAssistantGuildSettingsService</c> (a direct database edit, say).
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IAssistantGuildSettingsRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ToolAccessResolver> _logger;

    public ToolAccessResolver(
        IAssistantGuildSettingsRepository repository,
        IMemoryCache cache,
        ILogger<ToolAccessResolver> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> ResolveAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var key = CacheKeyPrefix + guildId;

        if (_cache.TryGetValue<IReadOnlySet<string>>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);
        var selected = settings?.GetEnabledToolsList() ?? new List<string>();

        // An empty selection is "the house default set", not "no tools" - a guild that has never
        // opened the page must keep working, and the settings UI says so in as many words.
        var resolved = selected.Count == 0
            ? ToolCatalog.DefaultsForScope(ToolScopes.Guild)
            : selected.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug(
            "Resolved {ToolCount} allowed tools for guild {GuildId} ({Source})",
            resolved.Count, guildId, selected.Count == 0 ? "house default" : "guild allow-list");

        _cache.Set(key, resolved, CacheDuration);
        return resolved;
    }

    /// <inheritdoc />
    public void Invalidate(ulong guildId)
    {
        _cache.Remove(CacheKeyPrefix + guildId);
        _logger.LogDebug("Invalidated cached tool access for guild {GuildId}", guildId);
    }
}
