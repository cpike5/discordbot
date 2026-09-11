using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;

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
    private readonly string _resolvedModel;
    private readonly LlmCatalogPricing? _resolvedPricing;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly ISkillActivationState? _skills;
    private readonly IDmSkillActivationStore? _skillActivations;

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
        ILogger logger,
        string resolvedModel,
        LlmCatalogPricing? resolvedPricing = null,
        ILlmUsageRecorder? usageRecorder = null,
        ISkillActivationState? skills = null,
        IDmSkillActivationStore? skillActivations = null)
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
        _resolvedModel = resolvedModel ?? throw new ArgumentNullException(nameof(resolvedModel));
        _resolvedPricing = resolvedPricing;
        _usageRecorder = usageRecorder ?? NoOpUsageRecorder.Instance;
        _skills = skills;
        _skillActivations = skillActivations;

        // The DM assistant is owner-only — access is decided before a message ever reaches here,
        // so there is no narrower permission left to express.
        ExecutionContext = new ToolContext { UserId = userId, CanMutate = true };
        ExecutionContext.SetActiveGuildId(activeGuildId);

        // The loader tool reads the session from here; the loop reads it from AgentContext.Skills.
        ExecutionContext.SetSkills(skills);
    }

    public string RateLimitCacheKeyPrefix => RateLimitPrefix;
    public string RateLimitScopeKey => _userId.ToString();

    /// <summary>DM assistant is owner-only and is never rate limited.</summary>
    public int? RateLimit => null;
    public int RateLimitWindowMinutes => 0;

    public string? Model => _resolvedModel;
    public LlmMode Mode => LlmMode.DmAssistant;
    public int MaxTokens => _options.MaxTokens;
    public double Temperature => _options.Temperature;
    public int MaxToolCallIterations => 10;
    public int MaxToolResultChars => _options.MaxToolResultChars;
    public int ToolExecutionTimeoutMs => _options.ToolExecutionTimeoutMs;
    public int DuplicateToolCallLimit => _options.DuplicateToolCallLimit;

    public IToolRegistry? ToolRegistry { get; }
    public ToolContext ExecutionContext { get; }
    public List<LlmMessage> ConversationHistory { get; }

    /// <inheritdoc />
    /// <remarks>
    /// This surface has conversation state, so the factory replays the previous turn's activations
    /// into the session before the run starts. That is what makes a skill cost one round on the turn
    /// that loads it and nothing afterwards.
    /// </remarks>
    public ISkillActivationState? Skills => _skills;

    /// <summary>
    /// Per-million-token rates: catalog pricing for the resolved model wins when the catalog
    /// reports a price, falling back to the configured rate for any price it does not report
    /// (or when there is no catalog row at all).
    /// </summary>
    public AssistantCostRates CostRates => new(
        _resolvedPricing?.PromptPricePerMillion ?? _options.CostPerMillionInputTokens,
        _resolvedPricing?.CompletionPricePerMillion ?? _options.CostPerMillionOutputTokens,
        _resolvedPricing?.CacheReadPricePerMillion ?? _options.CostPerMillionCachedTokens,
        _resolvedPricing?.CacheWritePricePerMillion ?? _options.CostPerMillionCacheWriteTokens);

    public int MaxResponseLength => _options.MaxResponseLength;
    public string TruncationSuffix => _options.TruncationSuffix;

    /// <inheritdoc />
    public async Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken)
    {
        string prompt;

        try
        {
            prompt = await _promptTemplate.LoadAsync(_options.OwnerSystemPromptPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load owner system prompt from {Path}, using fallback",
                _options.OwnerSystemPromptPath);
            prompt = "You are a helpful AI assistant. Be concise and accurate.";
        }

        // The roster, plus the instructions of anything replayed from a previous turn: the tool
        // result that carried them the first time is not in the sliding-window history, so without
        // this the tools would come back on turn 2 without the instructions that explain them.
        return SkillRoster.Append(prompt, _skills);
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

        // Carry this turn's skills into the next one. Clearing the conversation clears them too:
        // the instructions they put in the prompt are part of what "start again" means.
        if (_skillActivations is not null)
        {
            try
            {
                if (result.ConversationCleared)
                {
                    _skillActivations.Clear(_userId);
                }
                else if (result.Success && _skills is not null)
                {
                    _skillActivations.Set(_userId, _skills.Activated.Select(skill => skill.Key));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record active skills for user {UserId}", _userId);
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
                    EstimatedCostUsd = result.EstimatedCostUsd,
                    Model = result.Model
                };

                await _interactionLogRepo.AddAsync(log, cancellationToken);

                if (result.UsageRecord != null)
                {
                    result.UsageRecord.InteractionLogId = log.Id;
                }
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

        if (result.UsageRecord != null)
        {
            result.UsageRecord.LatencyMs = result.LatencyMs;
            _usageRecorder.Record(result.UsageRecord);
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
