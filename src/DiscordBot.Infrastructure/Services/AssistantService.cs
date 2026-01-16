using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for AI assistant operations.
/// Handles rate limiting, consent checking, and delegates to the agent runner for LLM interactions.
/// </summary>
/// <remarks>
/// Error Handling Strategy:
/// - Top-level errors: Catch, log, record to APM, return friendly error result (graceful degradation)
/// - Side-effect operations (metrics, logging): Catch, log, record to APM, swallow (user experience unaffected)
/// - Cancellation: Return early without error logging
/// </remarks>
public class AssistantService : IAssistantService
{
    private readonly ILogger<AssistantService> _logger;
    private readonly IAgentRunner _agentRunner;
    private readonly IToolRegistry _toolRegistry;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IConsentService _consentService;
    private readonly IGuildService _guildService;
    private readonly IAssistantGuildSettingsService _guildSettingsService;
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly IMemoryCache _cache;
    private readonly ISettingsService _settingsService;
    private readonly AssistantOptions _options;

    private const string RateLimitCacheKeyPrefix = "assistant_ratelimit:";

    /// <summary>
    /// Initializes a new instance of the AssistantService.
    /// </summary>
    public AssistantService(
        ILogger<AssistantService> logger,
        IAgentRunner agentRunner,
        IToolRegistry toolRegistry,
        IPromptTemplate promptTemplate,
        IConsentService consentService,
        IGuildService guildService,
        IAssistantGuildSettingsService guildSettingsService,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        IMemoryCache cache,
        ISettingsService settingsService,
        IOptions<AssistantOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRunner = agentRunner ?? throw new ArgumentNullException(nameof(agentRunner));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _guildSettingsService = guildSettingsService ?? throw new ArgumentNullException(nameof(guildSettingsService));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<AssistantResponseResult> AskQuestionAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        string question,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Processing assistant question from user {UserId} in guild {GuildId}, channel {ChannelId}",
            userId, guildId, channelId);

        try
        {
            // Validate question length
            if (string.IsNullOrWhiteSpace(question))
            {
                return AssistantResponseResult.ErrorResult("Question cannot be empty.");
            }

            if (question.Length > _options.MaxQuestionLength)
            {
                return AssistantResponseResult.ErrorResult(
                    $"Question is too long. Maximum length is {_options.MaxQuestionLength} characters.");
            }

            // Check if assistant is enabled for this guild
            if (!await IsEnabledForGuildAsync(guildId, cancellationToken))
            {
                return AssistantResponseResult.ErrorResult(
                    "The AI assistant is not enabled for this server.");
            }

            // Check if assistant is allowed in this channel
            if (!await IsAllowedInChannelAsync(guildId, channelId, cancellationToken))
            {
                return AssistantResponseResult.ErrorResult(
                    "The AI assistant is not allowed in this channel.");
            }

            // Check user consent
            if (_options.RequireExplicitConsent)
            {
                var hasConsent = await _consentService.HasConsentAsync(
                    userId, ConsentType.AssistantUsage, cancellationToken);

                if (!hasConsent)
                {
                    return AssistantResponseResult.ErrorResult(
                        "You need to grant consent before using the AI assistant. Use `/consent grant type:assistant` to enable this feature.");
                }
            }

            // Check rate limit
            var rateLimitResult = await CheckRateLimitAsync(guildId, userId, cancellationToken);
            if (!rateLimitResult.IsAllowed)
            {
                return AssistantResponseResult.ErrorResult(
                    rateLimitResult.Message ?? "You have exceeded your rate limit. Please try again later.");
            }

            // Build the agent context
            var systemPrompt = await LoadSystemPromptAsync(guildId, cancellationToken);

            var context = new AgentContext
            {
                SystemPrompt = systemPrompt,
                ToolRegistry = _options.EnableDocumentationTools ? _toolRegistry : null,
                ExecutionContext = new ToolContext
                {
                    UserId = userId,
                    GuildId = guildId,
                    ChannelId = channelId,
                    MessageId = messageId
                },
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                MaxToolCallIterations = _options.MaxToolCallsPerQuestion
            };

            // Format user message with guild context as documented
            var formattedMessage = await FormatUserMessageAsync(guildId, question, cancellationToken);

            // Run the agent
            var agentResult = await _agentRunner.RunAsync(formattedMessage, context, cancellationToken);

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            // Calculate cost
            var cost = CalculateCost(agentResult.TotalUsage);

            // Build response result
            var result = new AssistantResponseResult
            {
                Success = agentResult.Success,
                Response = agentResult.Success ? TruncateResponse(agentResult.Response) : null,
                ErrorMessage = agentResult.ErrorMessage,
                InputTokens = agentResult.TotalUsage.InputTokens,
                OutputTokens = agentResult.TotalUsage.OutputTokens,
                CachedTokens = agentResult.TotalUsage.CachedTokens,
                CacheCreationTokens = agentResult.TotalUsage.CacheWriteTokens,
                CacheHit = agentResult.TotalUsage.CachedTokens > 0,
                ToolCalls = agentResult.TotalToolCalls,
                LatencyMs = latencyMs,
                EstimatedCostUsd = cost
            };

            // Record rate limit usage (only on successful requests)
            if (agentResult.Success)
            {
                RecordRateLimitUsage(guildId, userId);
            }

            // Log metrics and interaction
            if (_options.EnableCostTracking)
            {
                await LogMetricsAsync(guildId, result, cancellationToken);
            }

            if (_options.LogInteractions)
            {
                await LogInteractionAsync(
                    guildId, channelId, userId, messageId,
                    question, result, cancellationToken);
            }

            _logger.LogInformation(
                "Assistant question processed. Success: {Success}, Latency: {LatencyMs}ms, Tokens: {TotalTokens}, Cost: ${Cost:F4}",
                result.Success, latencyMs, result.InputTokens + result.OutputTokens, cost);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation, not an error
            stopwatch.Stop();
            _logger.LogDebug(
                "Assistant question processing cancelled for user {UserId} in guild {GuildId}",
                userId, guildId);
            return AssistantResponseResult.ErrorResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Error processing assistant question from user {UserId} in guild {GuildId}",
                userId, guildId);

            // Log failed request metric
            if (_options.EnableCostTracking)
            {
                await _metricsRepository.IncrementFailedRequestAsync(
                    guildId, DateTime.UtcNow.Date, cancellationToken);
            }

            return AssistantResponseResult.ErrorResult(_options.ErrorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledForGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        // Check global setting first (runtime setting with fallback to config)
        var globallyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Assistant:GloballyEnabled", cancellationToken)
            ?? _options.GloballyEnabled;

        if (!globallyEnabled)
        {
            return false;
        }

        return await _guildSettingsService.IsEnabledAsync(guildId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedInChannelAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        return await _guildSettingsService.IsChannelAllowedAsync(guildId, channelId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitCheckResult> CheckRateLimitAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        // Get the rate limit for this guild
        var rateLimit = await _guildSettingsService.GetRateLimitAsync(guildId, cancellationToken);
        var windowMinutes = _options.RateLimitWindowMinutes;

        // Build cache key
        var cacheKey = $"{RateLimitCacheKeyPrefix}{guildId}:{userId}";

        // Get current usage from cache
        var usageEntry = _cache.Get<RateLimitUsageEntry>(cacheKey);

        if (usageEntry == null)
        {
            // No usage recorded, user is allowed
            return RateLimitCheckResult.Allowed(rateLimit);
        }

        // Check if the window has expired
        var windowExpiry = usageEntry.WindowStart.AddMinutes(windowMinutes);
        if (DateTime.UtcNow >= windowExpiry)
        {
            // Window expired, user is allowed with full quota
            _cache.Remove(cacheKey);
            return RateLimitCheckResult.Allowed(rateLimit);
        }

        // Check if user has exceeded rate limit
        if (usageEntry.Count >= rateLimit)
        {
            var retryAfter = windowExpiry - DateTime.UtcNow;
            var minutes = (int)Math.Ceiling(retryAfter.TotalMinutes);

            return RateLimitCheckResult.RateLimited(
                retryAfter,
                $"You've reached your question limit ({rateLimit} per {windowMinutes} minutes). Try again in {minutes} minute(s).");
        }

        // User still has remaining quota
        return RateLimitCheckResult.Allowed(rateLimit - usageEntry.Count);
    }

    /// <inheritdoc />
    public async Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsRepository.GetRangeAsync(
            guildId, date.Date, date.Date, cancellationToken);

        return metrics.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _metricsRepository.GetRangeAsync(
            guildId, startDate.Date, endDate.Date, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _interactionLogRepository.GetRecentByGuildAsync(
            guildId, limit, cancellationToken);
    }

    private async Task<string> LoadSystemPromptAsync(
        ulong guildId,
        CancellationToken cancellationToken)
    {
        var template = await _promptTemplate.LoadAsync(_options.AgentPromptPath, cancellationToken);

        var variables = new Dictionary<string, string>();

        if (_options.IncludeGuildContext)
        {
            variables["GUILD_ID"] = guildId.ToString();
        }

        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            variables["BASE_URL"] = _options.BaseUrl;
        }

        return _promptTemplate.Render(template, variables);
    }

    /// <summary>
    /// Formats the user message with guild context as documented in the agent prompt.
    /// Format: {GUILD_ID}\n{GUILD_NAME}\n---\n{USER_MESSAGE}
    /// </summary>
    private async Task<string> FormatUserMessageAsync(
        ulong guildId,
        string question,
        CancellationToken cancellationToken)
    {
        var guildName = "Unknown Guild";

        try
        {
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild != null)
            {
                guildName = guild.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get guild name for {GuildId}", guildId);
        }

        return $"{guildId}\n{guildName}\n---\n{question}";
    }

    private string TruncateResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return response;
        }

        if (response.Length <= _options.MaxResponseLength)
        {
            return response;
        }

        var truncateAt = _options.MaxResponseLength - _options.TruncationSuffix.Length;
        return response[..truncateAt] + _options.TruncationSuffix;
    }

    private decimal CalculateCost(LlmUsage usage)
    {
        var inputCost = usage.InputTokens * _options.CostPerMillionInputTokens / 1_000_000m;
        var outputCost = usage.OutputTokens * _options.CostPerMillionOutputTokens / 1_000_000m;
        var cachedCost = usage.CachedTokens * _options.CostPerMillionCachedTokens / 1_000_000m;
        var cacheWriteCost = usage.CacheWriteTokens * _options.CostPerMillionCacheWriteTokens / 1_000_000m;

        return inputCost + outputCost + cachedCost + cacheWriteCost;
    }

    private void RecordRateLimitUsage(ulong guildId, ulong userId)
    {
        var cacheKey = $"{RateLimitCacheKeyPrefix}{guildId}:{userId}";
        var windowMinutes = _options.RateLimitWindowMinutes;

        var entry = _cache.Get<RateLimitUsageEntry>(cacheKey);

        if (entry == null)
        {
            // Start a new window
            entry = new RateLimitUsageEntry
            {
                WindowStart = DateTime.UtcNow,
                Count = 1
            };
        }
        else
        {
            // Increment existing window
            entry.Count++;
        }

        // Cache entry with expiration at end of window
        var expiry = entry.WindowStart.AddMinutes(windowMinutes);
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiry);

        _cache.Set(cacheKey, entry, cacheOptions);
    }

    private async Task LogMetricsAsync(
        ulong guildId,
        AssistantResponseResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await _metricsRepository.IncrementMetricsAsync(
                guildId,
                DateTime.UtcNow.Date,
                result.InputTokens,
                result.OutputTokens,
                result.CachedTokens,
                result.CacheCreationTokens,
                result.CacheHit,
                result.ToolCalls,
                result.LatencyMs,
                result.EstimatedCostUsd,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log assistant metrics for guild {GuildId}", guildId);
        }
    }

    private async Task LogInteractionAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        string question,
        AssistantResponseResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = new AssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId,
                Question = question.Length > _options.MaxQuestionLength
                    ? question[.._options.MaxQuestionLength]
                    : question,
                Response = result.Response?.Length > _options.MaxResponseLength
                    ? result.Response[.._options.MaxResponseLength]
                    : result.Response,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                CachedTokens = result.CachedTokens,
                CacheCreationTokens = result.CacheCreationTokens,
                CacheHit = result.CacheHit,
                ToolCalls = result.ToolCalls,
                LatencyMs = result.LatencyMs,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                EstimatedCostUsd = result.EstimatedCostUsd
            };

            await _interactionLogRepository.AddAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log assistant interaction for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    /// Internal class for tracking rate limit usage in cache.
    /// </summary>
    private class RateLimitUsageEntry
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
    }
}
