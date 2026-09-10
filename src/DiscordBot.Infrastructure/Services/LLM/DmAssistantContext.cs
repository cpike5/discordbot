using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// DM-scoped <see cref="IAssistantContext"/>: owner-only (never rate limited), seeded with a
/// sliding-window conversation history, and logs to the DM assistant's metrics/interaction-log
/// tables.
/// </summary>
public class DmAssistantContext : IAssistantContext
{
    public const string RateLimitPrefix = "dm_assistant_ratelimit:";

    private readonly ulong _userId;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IDmConversationMessageRepository _conversationRepo;
    private readonly IDmAssistantInteractionLogRepository _interactionLogRepo;
    private readonly IDmAssistantUsageMetricsRepository _metricsRepo;
    private readonly DmAssistantOptions _options;
    private readonly ILogger _logger;

    public DmAssistantContext(
        ulong userId,
        ulong? activeGuildId,
        IToolRegistry toolRegistry,
        List<LlmMessage> conversationHistory,
        IPromptTemplate promptTemplate,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        DmAssistantOptions options,
        ILogger logger)
    {
        _userId = userId;
        ToolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        ConversationHistory = conversationHistory ?? throw new ArgumentNullException(nameof(conversationHistory));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ExecutionContext = new ToolContext
        {
            UserId = userId,
            ActiveGuildId = activeGuildId
        };
    }

    public string RateLimitCacheKeyPrefix => RateLimitPrefix;
    public string RateLimitScopeKey => _userId.ToString();

    /// <summary>DM assistant is owner-only and is never rate limited.</summary>
    public int? RateLimit => null;
    public int RateLimitWindowMinutes => 0;

    public string? Model => _options.Model;
    public int MaxTokens => _options.MaxTokens;
    public double Temperature => _options.Temperature;
    public int MaxToolCallIterations => 10;

    public IToolRegistry? ToolRegistry { get; }
    public ToolContext ExecutionContext { get; }
    public List<LlmMessage> ConversationHistory { get; }

    public AssistantCostRates CostRates => new(
        _options.CostPerMillionInputTokens,
        _options.CostPerMillionOutputTokens,
        _options.CostPerMillionCachedTokens,
        _options.CostPerMillionCacheWriteTokens);

    public int MaxResponseLength => _options.MaxResponseLength;
    public string TruncationSuffix => _options.TruncationSuffix;

    /// <inheritdoc />
    public async Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _promptTemplate.LoadAsync(_options.OwnerSystemPromptPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load owner system prompt from {Path}, using fallback",
                _options.OwnerSystemPromptPath);
            return "You are a helpful AI assistant. Be concise and accurate.";
        }
    }

    /// <inheritdoc />
    public Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken)
        => Task.FromResult(rawMessage);

    /// <inheritdoc />
    public async Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken)
    {
        // Only a successful exchange is saved to history / counted in daily metrics;
        // a failed run is still worth an interaction-log entry for debugging.
        if (result.Success && !result.ConversationCleared)
        {
            var utcNow = DateTime.UtcNow;
            try
            {
                await _conversationRepo.AddAsync(new DmConversationMessage
                {
                    UserId = _userId,
                    Role = "user",
                    Content = inputMessage,
                    Timestamp = utcNow
                }, cancellationToken);

                await _conversationRepo.AddAsync(new DmConversationMessage
                {
                    UserId = _userId,
                    Role = "assistant",
                    Content = result.Response ?? string.Empty,
                    Timestamp = utcNow
                }, cancellationToken);

                await _conversationRepo.DeleteOldestByUserAsync(
                    _userId, _options.MaxConversationMessages, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save DM conversation turn for user {UserId}", _userId);
            }
        }

        if (_options.LogInteractions)
        {
            try
            {
                var log = new DmAssistantInteractionLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserId = _userId,
                    IsOwner = true,
                    Message = inputMessage.Length > 2000 ? inputMessage[..2000] : inputMessage,
                    Response = result.Response?.Length > 2000 ? result.Response[..2000] : result.Response,
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    CachedTokens = result.CachedTokens,
                    ToolCalls = result.ToolCalls,
                    ToolNames = result.ToolNames.Count > 0 ? string.Join(", ", result.ToolNames) : null,
                    LoopCount = result.LoopCount,
                    LatencyMs = result.LatencyMs,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    EstimatedCostUsd = result.EstimatedCostUsd
                };

                await _interactionLogRepo.AddAsync(log, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log DM assistant interaction for user {UserId}", _userId);
            }
        }

        if (result.Success && _options.EnableCostTracking)
        {
            await UpdateDailyMetricsAsync(result, cancellationToken);
        }
    }

    private async Task UpdateDailyMetricsAsync(AssistantPipelineResult result, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var metrics = await _metricsRepo.GetByUserAndDateAsync(_userId, today, cancellationToken);

            if (metrics == null)
            {
                metrics = new DmAssistantUsageMetrics
                {
                    UserId = _userId,
                    Date = today
                };
            }

            metrics.TotalMessages++;
            metrics.TotalInputTokens += result.InputTokens;
            metrics.TotalOutputTokens += result.OutputTokens;
            metrics.TotalCachedTokens += result.CachedTokens;
            metrics.EstimatedCostUsd += result.EstimatedCostUsd;
            if (!result.Success) metrics.FailedRequests++;

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
                await _metricsRepo.AddAsync(metrics, cancellationToken);
            }
            else
            {
                await _metricsRepo.UpdateAsync(metrics, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update DM assistant daily metrics for user {UserId}", _userId);
        }
    }
}
