using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

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

    public GuildAssistantContext(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        IToolRegistry? toolRegistry,
        IGuildService guildService,
        IPromptTemplate promptTemplate,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        AssistantOptions options,
        ILogger logger)
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
        RateLimit = rateLimit;

        ExecutionContext = new ToolContext
        {
            UserId = userId,
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId
        };
    }

    public string RateLimitCacheKeyPrefix => RateLimitPrefix;
    public string RateLimitScopeKey => $"{_guildId}:{_userId}";
    public int? RateLimit { get; }
    public int RateLimitWindowMinutes => _options.RateLimits.RateLimitWindowMinutes;

    public string? Model => _options.Sampling.Model;
    public int MaxTokens => _options.Sampling.MaxTokens;
    public double Temperature => _options.Sampling.Temperature;
    public int MaxToolCallIterations => _options.Tools.MaxToolCallsPerQuestion;

    public IToolRegistry? ToolRegistry { get; }
    public ToolContext ExecutionContext { get; }
    public List<LlmMessage> ConversationHistory { get; } = new();

    public AssistantCostRates CostRates => new(
        _options.Cost.CostPerMillionInputTokens,
        _options.Cost.CostPerMillionOutputTokens,
        _options.Cost.CostPerMillionCachedTokens,
        _options.Cost.CostPerMillionCacheWriteTokens);

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
                    LatencyMs = result.LatencyMs,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    EstimatedCostUsd = result.EstimatedCostUsd
                };

                await _interactionLogRepository.AddAsync(log, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log assistant interaction for guild {GuildId}", _guildId);
            }
        }
    }
}
