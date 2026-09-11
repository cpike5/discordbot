using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Guild-scoped <see cref="IAssistantContext"/>: rate limited per guild+user, no conversation
/// history, and logs to the guild assistant's metrics/interaction-log tables.
/// </summary>
public class GuildAssistantContext : IAssistantContext
{
    public const string RateLimitPrefix = "assistant_ratelimit:";

    private readonly ulong _guildId;
    private readonly ulong _channelId;
    private readonly ulong _userId;
    private readonly ulong _messageId;
    private readonly string _question;
    private readonly IGuildService _guildService;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly AssistantOptions _options;
    private readonly ILogger _logger;
    private readonly string _resolvedModel;
    private readonly LlmCatalogPricing? _resolvedPricing;
    private readonly ILlmUsageRecorder _usageRecorder;

    public GuildAssistantContext(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        bool callerCanMutate,
        IToolRegistry? toolRegistry,
        IGuildService guildService,
        IPromptTemplate promptTemplate,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        AssistantOptions options,
        ILogger logger,
        string resolvedModel,
        LlmCatalogPricing? resolvedPricing = null,
        ILlmUsageRecorder? usageRecorder = null)
    {
        _guildId = guildId;
        _channelId = channelId;
        _userId = userId;
        _messageId = messageId;
        _question = question;
        ToolRegistry = toolRegistry;
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolvedModel = resolvedModel ?? throw new ArgumentNullException(nameof(resolvedModel));
        _resolvedPricing = resolvedPricing;
        _usageRecorder = usageRecorder ?? NoOpUsageRecorder.Instance;
        RateLimit = rateLimit;

        ExecutionContext = new ToolContext
        {
            UserId = userId,
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId,
            CanMutate = callerCanMutate
        };
    }

    public string RateLimitCacheKeyPrefix => RateLimitPrefix;
    public string RateLimitScopeKey => $"{_guildId}:{_userId}";
    public int? RateLimit { get; }
    public int RateLimitWindowMinutes => _options.RateLimits.RateLimitWindowMinutes;

    public string? Model => _resolvedModel;
    public LlmMode Mode => LlmMode.GuildAssistant;
    public int MaxTokens => _options.Sampling.MaxTokens;
    public double Temperature => _options.Sampling.Temperature;
    public int MaxToolCallIterations => _options.Tools.MaxToolRounds;
    public int MaxToolResultChars => _options.Tools.MaxToolResultChars;
    public int ToolExecutionTimeoutMs => _options.Tools.ToolExecutionTimeoutMs;
    public int DuplicateToolCallLimit => _options.Tools.DuplicateToolCallLimit;

    public IToolRegistry? ToolRegistry { get; }
    public ToolContext ExecutionContext { get; }
    public List<LlmMessage> ConversationHistory { get; } = new();

    /// <summary>
    /// Per-million-token rates: catalog pricing for the resolved model wins when the catalog
    /// reports a price, falling back to the configured rate for any price it does not report
    /// (or when there is no catalog row at all). Billed <c>usage.cost</c> still wins over either
    /// when OpenRouter reports one - see <c>AssistantMessagePipeline.CalculateCost</c>.
    /// </summary>
    public AssistantCostRates CostRates => new(
        _resolvedPricing?.PromptPricePerMillion ?? _options.Cost.CostPerMillionInputTokens,
        _resolvedPricing?.CompletionPricePerMillion ?? _options.Cost.CostPerMillionOutputTokens,
        _resolvedPricing?.CacheReadPricePerMillion ?? _options.Cost.CostPerMillionCachedTokens,
        _resolvedPricing?.CacheWritePricePerMillion ?? _options.Cost.CostPerMillionCacheWriteTokens);

    public int MaxResponseLength => _options.Messages.MaxResponseLength;
    public string TruncationSuffix => _options.Messages.TruncationSuffix;

    /// <inheritdoc />
    public async Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken)
    {
        var template = await _promptTemplate.LoadAsync(_options.Tools.AgentPromptPath, cancellationToken);

        var variables = new Dictionary<string, string>();

        if (_options.IncludeGuildContext)
        {
            variables["GUILD_ID"] = _guildId.ToString();
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
    public async Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken)
    {
        var guildName = "Unknown Guild";

        try
        {
            var guild = await _guildService.GetGuildByIdAsync(_guildId, cancellationToken);
            if (guild != null)
            {
                guildName = guild.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get guild name for {GuildId}", _guildId);
        }

        return $"{_guildId}\n{guildName}\n---\n{rawMessage}";
    }

    /// <summary>Column width of <c>AssistantInteractionLog.ToolNames</c>.</summary>
    private const int MaxToolNamesChars = 512;

    /// <summary>
    /// Comma-joins the tools a run called, within the column's width.
    /// </summary>
    /// <remarks>
    /// Truncation stops at a whole name rather than mid-word: the per-tool metrics table splits this
    /// column on the comma, and half a name would be counted as a tool that does not exist. A run
    /// that calls more tools than fit is far past the round budget anyway.
    /// </remarks>
    private static string? JoinToolNames(IReadOnlyList<string> toolNames)
    {
        if (toolNames.Count == 0)
        {
            return null;
        }

        var joined = new System.Text.StringBuilder();

        foreach (var name in toolNames)
        {
            var addition = joined.Length == 0 ? name : ", " + name;
            if (joined.Length + addition.Length > MaxToolNamesChars)
            {
                break;
            }

            joined.Append(addition);
        }

        return joined.Length > 0 ? joined.ToString() : null;
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken)
    {
        if (_options.Cost.EnableCostTracking)
        {
            try
            {
                await _metricsRepository.IncrementMetricsAsync(
                    _guildId,
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
                _logger.LogError(ex, "Failed to log assistant metrics for guild {GuildId}", _guildId);
            }
        }

        if (_options.Privacy.LogInteractions)
        {
            try
            {
                var log = new AssistantInteractionLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserId = _userId,
                    GuildId = _guildId,
                    ChannelId = _channelId,
                    MessageId = _messageId,
                    Question = _question.Length > _options.Messages.MaxQuestionLength
                        ? _question[.._options.Messages.MaxQuestionLength]
                        : _question,
                    Response = result.Response?.Length > _options.Messages.MaxResponseLength
                        ? result.Response[.._options.Messages.MaxResponseLength]
                        : result.Response,
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    CachedTokens = result.CachedTokens,
                    CacheCreationTokens = result.CacheCreationTokens,
                    CacheHit = result.CacheHit,
                    ToolCalls = result.ToolCalls,
                    ToolNames = JoinToolNames(result.ToolNames),
                    LatencyMs = result.LatencyMs,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    EstimatedCostUsd = result.EstimatedCostUsd,
                    Model = result.Model
                };

                await _interactionLogRepository.AddAsync(log, cancellationToken);

                if (result.UsageRecord != null)
                {
                    result.UsageRecord.InteractionLogId = log.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log assistant interaction for guild {GuildId}", _guildId);
            }
        }

        if (result.UsageRecord != null)
        {
            result.UsageRecord.LatencyMs = result.LatencyMs;
            _usageRecorder.Record(result.UsageRecord);
        }
    }
}
