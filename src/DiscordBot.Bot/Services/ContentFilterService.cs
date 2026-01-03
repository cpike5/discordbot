using System.Text.RegularExpressions;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Moderation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for filtering message content against blocklists and patterns.
/// Thread-safe singleton service with compiled regex caching.
/// </summary>
public class ContentFilterService : IContentFilterService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ContentFilterService> _logger;

    // Cache key pattern: contentfilter:{guildId}
    private const string CacheKeyPattern = "contentfilter:{0}";

    // Cache duration for compiled filters (10 minutes)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Represents compiled filters for a guild.
    /// </summary>
    private record CompiledFilters(
        List<Regex> ProhibitedWordPatterns,
        List<Regex> CustomPatterns,
        List<string> AllowedDomains,
        bool BlockUnlistedLinks,
        bool BlockInviteLinks);

    public ContentFilterService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<ContentFilterService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DetectionResultDto?> AnalyzeMessageAsync(
        ulong guildId,
        string content,
        ulong userId,
        ulong channelId,
        ulong messageId,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "content_filter",
            "analyze_message",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogDebug("Analyzing message {MessageId} from user {UserId} in guild {GuildId} for prohibited content",
                messageId, userId, guildId);

            // Get content filter configuration for guild using a scope for the scoped service
            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IGuildModerationConfigService>();
            var config = await configService.GetContentFilterConfigAsync(guildId, ct);

            // If content filtering is disabled, return null
            if (!config.Enabled)
            {
                _logger.LogTrace("Content filtering is disabled for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            // Load or get cached filters
            var filters = await GetOrLoadFiltersAsync(guildId, config, ct);

            // Check against prohibited word patterns (blocklist)
            foreach (var pattern in filters.ProhibitedWordPatterns)
            {
                var match = pattern.Match(content);
                if (match.Success)
                {
                    _logger.LogInformation("Prohibited word detected in message {MessageId} from user {UserId} in guild {GuildId}: pattern matched",
                        messageId, userId, guildId);

                    var result = new DetectionResultDto
                    {
                        RuleType = RuleType.Content,
                        Severity = Severity.High,
                        Description = "Prohibited word or phrase detected",
                        Evidence = new Dictionary<string, object>
                        {
                            ["matchedPattern"] = pattern.ToString(),
                            ["matchedText"] = match.Value,
                            ["messageId"] = messageId,
                            ["channelId"] = channelId,
                            ["userId"] = userId
                        },
                        ShouldAutoAction = config.AutoAction != AutoAction.None,
                        RecommendedAction = config.AutoAction
                    };

                    BotActivitySource.SetSuccess(activity);
                    return result;
                }
            }

            // Check against custom regex patterns
            foreach (var pattern in filters.CustomPatterns)
            {
                var match = pattern.Match(content);
                if (match.Success)
                {
                    _logger.LogInformation("Custom filter pattern matched in message {MessageId} from user {UserId} in guild {GuildId}",
                        messageId, userId, guildId);

                    var result = new DetectionResultDto
                    {
                        RuleType = RuleType.Content,
                        Severity = Severity.Medium,
                        Description = "Custom filter pattern matched",
                        Evidence = new Dictionary<string, object>
                        {
                            ["matchedPattern"] = pattern.ToString(),
                            ["matchedText"] = match.Value,
                            ["messageId"] = messageId,
                            ["channelId"] = channelId,
                            ["userId"] = userId
                        },
                        ShouldAutoAction = config.AutoAction != AutoAction.None,
                        RecommendedAction = config.AutoAction
                    };

                    BotActivitySource.SetSuccess(activity);
                    return result;
                }
            }

            // Check for invite links if enabled
            if (filters.BlockInviteLinks)
            {
                var invitePattern = ContentFilterTemplates.Templates["invites"].Patterns[0];
                var inviteRegex = new Regex(invitePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var match = inviteRegex.Match(content);

                if (match.Success)
                {
                    _logger.LogInformation("Discord invite link detected in message {MessageId} from user {UserId} in guild {GuildId}",
                        messageId, userId, guildId);

                    var result = new DetectionResultDto
                    {
                        RuleType = RuleType.Content,
                        Severity = Severity.Medium,
                        Description = "Discord invite link detected",
                        Evidence = new Dictionary<string, object>
                        {
                            ["matchedText"] = match.Value,
                            ["messageId"] = messageId,
                            ["channelId"] = channelId,
                            ["userId"] = userId
                        },
                        ShouldAutoAction = config.AutoAction != AutoAction.None,
                        RecommendedAction = config.AutoAction
                    };

                    BotActivitySource.SetSuccess(activity);
                    return result;
                }
            }

            // Check for unlisted links if enabled
            if (filters.BlockUnlistedLinks && filters.AllowedDomains.Any())
            {
                var linkPattern = ContentFilterTemplates.Templates["links"].Patterns[0];
                var linkRegex = new Regex(linkPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var matches = linkRegex.Matches(content);

                foreach (Match match in matches)
                {
                    var url = match.Value;
                    var isAllowed = filters.AllowedDomains.Any(domain =>
                        url.Contains(domain, StringComparison.OrdinalIgnoreCase));

                    if (!isAllowed)
                    {
                        _logger.LogInformation("Unlisted link detected in message {MessageId} from user {UserId} in guild {GuildId}: {Url}",
                            messageId, userId, guildId, url);

                        var result = new DetectionResultDto
                        {
                            RuleType = RuleType.Content,
                            Severity = Severity.Medium,
                            Description = "Link to non-whitelisted domain detected",
                            Evidence = new Dictionary<string, object>
                            {
                                ["matchedText"] = url,
                                ["messageId"] = messageId,
                                ["channelId"] = channelId,
                                ["userId"] = userId
                            },
                            ShouldAutoAction = config.AutoAction != AutoAction.None,
                            RecommendedAction = config.AutoAction
                        };

                        BotActivitySource.SetSuccess(activity);
                        return result;
                    }
                }
            }

            _logger.LogTrace("No prohibited content detected in message {MessageId}", messageId);
            BotActivitySource.SetSuccess(activity);
            return null;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task LoadGuildFiltersAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "content_filter",
            "load_guild_filters",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Loading content filters for guild {GuildId}", guildId);

            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IGuildModerationConfigService>();
            var config = await configService.GetContentFilterConfigAsync(guildId, ct);
            var filters = CompileFilters(config);

            var cacheKey = string.Format(CacheKeyPattern, guildId);
            _cache.Set(cacheKey, filters, CacheDuration);

            _logger.LogInformation("Loaded and cached {WordPatternCount} word patterns and {CustomPatternCount} custom patterns for guild {GuildId}",
                filters.ProhibitedWordPatterns.Count, filters.CustomPatterns.Count, guildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(ulong guildId)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "content_filter",
            "invalidate_cache",
            guildId: guildId);

        try
        {
            var cacheKey = string.Format(CacheKeyPattern, guildId);
            _cache.Remove(cacheKey);

            _logger.LogDebug("Invalidated content filter cache for guild {GuildId}", guildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets cached filters or loads them if not cached.
    /// </summary>
    private Task<CompiledFilters> GetOrLoadFiltersAsync(
        ulong guildId,
        ContentFilterConfigDto config,
        CancellationToken ct)
    {
        var cacheKey = string.Format(CacheKeyPattern, guildId);

        if (_cache.TryGetValue<CompiledFilters>(cacheKey, out var cachedFilters) && cachedFilters != null)
        {
            _logger.LogTrace("Using cached filters for guild {GuildId}", guildId);
            return Task.FromResult(cachedFilters);
        }

        _logger.LogDebug("Compiling and caching filters for guild {GuildId}", guildId);
        var filters = CompileFilters(config);
        _cache.Set(cacheKey, filters, CacheDuration);

        return Task.FromResult(filters);
    }

    /// <summary>
    /// Compiles the content filter configuration into executable regex patterns.
    /// </summary>
    private CompiledFilters CompileFilters(ContentFilterConfigDto config)
    {
        var prohibitedWordPatterns = new List<Regex>();
        var customPatterns = new List<Regex>();

        // Compile prohibited words as word-boundary regex patterns
        foreach (var word in config.ProhibitedWords)
        {
            try
            {
                // Treat as literal word with word boundaries
                var pattern = $@"\b{Regex.Escape(word)}\b";
                prohibitedWordPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compile prohibited word pattern: {Word}", word);
            }
        }

        // Add template patterns if needed (this can be extended to support template selection)
        // For now, we'll check templates in the analysis method

        return new CompiledFilters(
            ProhibitedWordPatterns: prohibitedWordPatterns,
            CustomPatterns: customPatterns,
            AllowedDomains: config.AllowedLinkDomains,
            BlockUnlistedLinks: config.BlockUnlistedLinks,
            BlockInviteLinks: config.BlockInviteLinks
        );
    }
}
