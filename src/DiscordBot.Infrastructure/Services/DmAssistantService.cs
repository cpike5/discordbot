using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for DM-based AI assistant operations.
/// Routes messages through the AgentRunner agentic loop with DM-scoped tools.
/// </summary>
/// <remarks>
/// Error Handling Strategy (matches guild assistant):
/// - Top-level errors: Catch, log, return friendly error result
/// - Side-effect operations (metrics, logging): Catch, log, swallow
/// - Cancellation: Return early without error logging
/// </remarks>
public class DmAssistantService : IDmAssistantService
{
    private readonly ILogger<DmAssistantService> _logger;
    private readonly IAgentRunner _agentRunner;
    private readonly IEnumerable<IDmToolProvider> _dmToolProviders;
    private readonly IMemoryCache _memoryCache;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDmConversationMessageRepository _conversationRepo;
    private readonly IDmAssistantInteractionLogRepository _interactionLogRepo;
    private readonly IDmAssistantUsageMetricsRepository _metricsRepo;
    private readonly IBotOwnerResolver _ownerResolver;
    private readonly IPromptTemplate _promptTemplate;
    private readonly DmAssistantOptions _options;

    public const string ActiveGuildCacheKeyPrefix = "dm_active_guild:";

    public DmAssistantService(
        ILogger<DmAssistantService> logger,
        IAgentRunner agentRunner,
        IEnumerable<IDmToolProvider> dmToolProviders,
        IMemoryCache memoryCache,
        ILoggerFactory loggerFactory,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        IBotOwnerResolver ownerResolver,
        IPromptTemplate promptTemplate,
        IOptions<DmAssistantOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRunner = agentRunner ?? throw new ArgumentNullException(nameof(agentRunner));
        _dmToolProviders = dmToolProviders ?? throw new ArgumentNullException(nameof(dmToolProviders));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<DmAssistantResponse> ProcessMessageAsync(
        ulong userId, string message, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Processing DM assistant message from user {UserId}", userId);

        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return DmAssistantResponse.ErrorResult("Message cannot be empty.");
            }

            // Discord messages are capped at 4000 chars; truncate if somehow longer
            if (message.Length > 4000)
            {
                message = message[..4000];
            }

            var isOwner = await IsOwnerAsync(userId);

            // Non-owner: return placeholder and log interaction
            if (!isOwner)
            {
                _logger.LogDebug("User {UserId} is not owner, returning placeholder", userId);

                var placeholderResponse = DmAssistantResponse.PlaceholderResult(_options.PlaceholderMessage);
                stopwatch.Stop();
                placeholderResponse.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

                if (_options.LogInteractions)
                {
                    await LogInteractionAsync(userId, false, message, placeholderResponse, ct);
                }

                return placeholderResponse;
            }

            // Load system prompt (cached in memory)
            var systemPrompt = await LoadSystemPromptAsync(ct);

            // Load conversation history
            var history = await _conversationRepo.GetRecentByUserAsync(
                userId, _options.MaxConversationMessages, ct);

            // Build message list from history
            var conversationHistory = new List<LlmMessage>();
            foreach (var historyMsg in history)
            {
                conversationHistory.Add(new LlmMessage
                {
                    Role = historyMsg.Role == "assistant" ? LlmRole.Assistant : LlmRole.User,
                    Content = historyMsg.Content
                });
            }

            // Build local ToolRegistry with only DM tool providers
            var toolRegistry = BuildDmToolRegistry();

            // Read active guild from cache
            var activeGuildId = _memoryCache.Get<ulong?>(ActiveGuildCacheKeyPrefix + userId);

            // Build AgentContext
            var context = new AgentContext
            {
                SystemPrompt = systemPrompt,
                ToolRegistry = toolRegistry,
                ExecutionContext = new ToolContext
                {
                    UserId = userId,
                    ActiveGuildId = activeGuildId
                },
                ConversationHistory = conversationHistory,
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                MaxToolCallIterations = 10
            };

            // Call AgentRunner (replaces direct ILlmClient.CompleteAsync)
            var agentResult = await _agentRunner.RunAsync(message, context, ct);

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            if (!agentResult.Success || string.IsNullOrWhiteSpace(agentResult.Response))
            {
                _logger.LogWarning("Agent run failed for user {UserId}: {Error}",
                    userId, agentResult.ErrorMessage ?? "Empty response");

                var errorResponse = DmAssistantResponse.ErrorResult(
                    agentResult.ErrorMessage ?? _options.ErrorMessage);
                errorResponse.LatencyMs = latencyMs;

                if (_options.LogInteractions)
                {
                    await LogInteractionAsync(userId, true, message, errorResponse, ct,
                        agentResult.TotalToolCalls, agentResult.LoopCount, agentResult.ToolNames);
                }

                return errorResponse;
            }

            var responseText = TruncateResponse(agentResult.Response);

            // Save conversation messages (skip if conversation was cleared to avoid leaking context)
            if (!agentResult.ConversationCleared)
            {
                var utcNow = DateTime.UtcNow;
                await _conversationRepo.AddAsync(new DmConversationMessage
                {
                    UserId = userId,
                    Role = "user",
                    Content = message,
                    Timestamp = utcNow
                }, ct);

                await _conversationRepo.AddAsync(new DmConversationMessage
                {
                    UserId = userId,
                    Role = "assistant",
                    Content = responseText,
                    Timestamp = utcNow
                }, ct);

                // Trim conversation history to sliding window
                await _conversationRepo.DeleteOldestByUserAsync(
                    userId, _options.MaxConversationMessages, ct);
            }

            // Calculate cost from aggregated usage
            var cost = CalculateCost(agentResult.TotalUsage);

            var result = new DmAssistantResponse
            {
                Success = true,
                Response = responseText,
                IsOwner = true,
                InputTokens = agentResult.TotalUsage.InputTokens,
                OutputTokens = agentResult.TotalUsage.OutputTokens,
                CachedTokens = agentResult.TotalUsage.CachedTokens,
                EstimatedCostUsd = cost,
                LatencyMs = latencyMs
            };

            // Log interaction (swallow errors)
            if (_options.LogInteractions)
            {
                await LogInteractionAsync(userId, true, message, result, ct,
                    agentResult.TotalToolCalls, agentResult.LoopCount, agentResult.ToolNames);
            }

            // Update daily metrics (swallow errors)
            if (_options.EnableCostTracking)
            {
                await UpdateDailyMetricsAsync(userId, result, ct);
            }

            _logger.LogInformation(
                "DM assistant response sent to user {UserId}. " +
                "Tokens: {InputTokens} in / {OutputTokens} out / {CachedTokens} cached. " +
                "Cost: ${Cost:F4}. Latency: {LatencyMs}ms. ToolCalls: {ToolCalls}. Loops: {Loops}",
                userId, result.InputTokens, result.OutputTokens, result.CachedTokens,
                result.EstimatedCostUsd, result.LatencyMs, agentResult.TotalToolCalls, agentResult.LoopCount);

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("DM assistant processing cancelled for user {UserId}", userId);
            return DmAssistantResponse.ErrorResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing DM assistant message from user {UserId}", userId);
            return DmAssistantResponse.ErrorResult(_options.ErrorMessage);
        }
    }

    private IToolRegistry BuildDmToolRegistry()
    {
        var registry = new ToolRegistry(
            _loggerFactory.CreateLogger<ToolRegistry>(),
            Enumerable.Empty<IToolProvider>());

        foreach (var provider in _dmToolProviders)
        {
            registry.RegisterProvider(provider);
        }

        return registry;
    }

    private async Task<bool> IsOwnerAsync(ulong userId)
    {
        try
        {
            var ownerId = await _ownerResolver.GetOwnerIdAsync();
            return userId == ownerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get application owner info");
            return false;
        }
    }

    private async Task<string> LoadSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            return await _promptTemplate.LoadAsync(_options.OwnerSystemPromptPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load owner system prompt from {Path}, using fallback",
                _options.OwnerSystemPromptPath);
            return "You are a helpful AI assistant. Be concise and accurate.";
        }
    }

    private string TruncateResponse(string response)
    {
        if (string.IsNullOrEmpty(response) || response.Length <= _options.MaxResponseLength)
        {
            return response;
        }

        var truncateAt = _options.MaxResponseLength - _options.TruncationSuffix.Length;
        return response[..truncateAt] + _options.TruncationSuffix;
    }

    /// <summary>
    /// Cost of a run in USD. OpenRouter reports what it actually billed, so that figure wins when
    /// present; the configured per-million rates are the fallback for a response that carried no
    /// cost (a BYOK call, or a provider that doesn't report one).
    /// </summary>
    private decimal CalculateCost(LlmUsage usage)
    {
        if (usage.EstimatedCost.HasValue)
        {
            return usage.EstimatedCost.Value;
        }

        var inputCost = usage.InputTokens * _options.CostPerMillionInputTokens / 1_000_000m;
        var outputCost = usage.OutputTokens * _options.CostPerMillionOutputTokens / 1_000_000m;
        var cachedCost = usage.CachedTokens * _options.CostPerMillionCachedTokens / 1_000_000m;
        var cacheWriteCost = usage.CacheWriteTokens * _options.CostPerMillionCacheWriteTokens / 1_000_000m;

        return inputCost + outputCost + cachedCost + cacheWriteCost;
    }

    private async Task LogInteractionAsync(
        ulong userId, bool isOwner, string message,
        DmAssistantResponse result, CancellationToken ct,
        int toolCalls = 0, int loopCount = 0, List<string>? toolNames = null)
    {
        try
        {
            var log = new DmAssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                IsOwner = isOwner,
                Message = message.Length > 2000 ? message[..2000] : message,
                Response = result.Response?.Length > 2000 ? result.Response[..2000] : result.Response,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                CachedTokens = result.CachedTokens,
                ToolCalls = toolCalls,
                ToolNames = toolNames?.Count > 0 ? string.Join(", ", toolNames) : null,
                LoopCount = loopCount,
                LatencyMs = result.LatencyMs,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                EstimatedCostUsd = result.EstimatedCostUsd
            };

            await _interactionLogRepo.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log DM assistant interaction for user {UserId}", userId);
        }
    }

    private async Task UpdateDailyMetricsAsync(
        ulong userId, DmAssistantResponse result, CancellationToken ct)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var metrics = await _metricsRepo.GetByUserAndDateAsync(userId, today, ct);

            if (metrics == null)
            {
                metrics = new DmAssistantUsageMetrics
                {
                    UserId = userId,
                    Date = today
                };
            }

            metrics.TotalMessages++;
            metrics.TotalInputTokens += result.InputTokens;
            metrics.TotalOutputTokens += result.OutputTokens;
            metrics.TotalCachedTokens += result.CachedTokens;
            metrics.EstimatedCostUsd += result.EstimatedCostUsd;
            if (!result.Success) metrics.FailedRequests++;

            // Rolling average latency
            if (metrics.TotalMessages == 1)
            {
                metrics.AverageLatencyMs = result.LatencyMs;
            }
            else
            {
                metrics.AverageLatencyMs = (int)(
                    (metrics.AverageLatencyMs * (metrics.TotalMessages - 1) + result.LatencyMs)
                    / metrics.TotalMessages);
            }

            metrics.UpdatedAt = DateTime.UtcNow;

            if (metrics.Id == 0)
            {
                await _metricsRepo.AddAsync(metrics, ct);
            }
            else
            {
                await _metricsRepo.UpdateAsync(metrics, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update DM assistant daily metrics for user {UserId}", userId);
        }
    }
}
