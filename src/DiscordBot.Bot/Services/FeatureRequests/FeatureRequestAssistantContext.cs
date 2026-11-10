using cpike.Agent;
using DiscordBot.Core.Configuration.Assistant;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.FeatureRequests;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Bot.Services.FeatureRequests;

/// <summary>
/// The <see cref="IAssistantContext"/> for one turn of a feature-request conversation: the
/// <c>feature-requests</c> mode (whose only tool is <c>submit_feature_request</c>), the
/// conversation so far, and the ledger row.
/// </summary>
public sealed class FeatureRequestAssistantContext : IAssistantContext
{
    private readonly LlmResolvedModel _model;
    private readonly AssistantCostOptions _costFallback;
    private readonly ILlmUsageRecorder _usageRecorder;

    /// <summary>Creates the context.</summary>
    /// <param name="userId">The member filing the request.</param>
    /// <param name="guildId">The guild the request is for.</param>
    /// <param name="history">The conversation so far, oldest first.</param>
    /// <param name="model">The resolved feature-request model and its catalogue pricing.</param>
    /// <param name="costFallback">
    /// Per-million rates for any price the catalogue does not report. Feature requests have no cost
    /// options of their own, and the guild assistant's are the closest existing fallback.
    /// </param>
    /// <param name="usageRecorder">The usage ledger.</param>
    public FeatureRequestAssistantContext(
        ulong userId,
        ulong guildId,
        IEnumerable<FeatureRequestTurn> history,
        LlmResolvedModel model,
        AssistantCostOptions costFallback,
        ILlmUsageRecorder usageRecorder)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _costFallback = costFallback ?? throw new ArgumentNullException(nameof(costFallback));
        _usageRecorder = usageRecorder ?? throw new ArgumentNullException(nameof(usageRecorder));

        // Filing a feature request is the whole point of this conversation, and any member who can
        // start one may finish it.
        Identity = new DiscordRunIdentity(userId, guildId, CanMutate: true);
        History = history
            .Select(turn => new ConversationTurn(turn.Role == FeatureRequestTurnRole.User, turn.Content))
            .ToList();
    }

    public string RateLimitCacheKeyPrefix => "feature_request_ratelimit:";
    public string RateLimitScopeKey => Identity.UserId.ToString();
    public int? RateLimit => null;
    public int RateLimitWindowMinutes => 0;
    public string? Model => _model.Slug;
    public LlmMode Mode => LlmMode.FeatureRequests;
    public string ModeKey => CatalogPromptStore.FeatureRequestModeName;
    public DiscordRunIdentity Identity { get; }
    public int MaxTokens => 1024;
    public int MaxToolCallIterations => 2;
    public int MaxToolResultChars => 8000;
    public IReadOnlyCollection<string> DisabledTools => [];
    public IReadOnlyCollection<string> PreloadedSkills => [];
    public IReadOnlyList<ConversationTurn> History { get; }

    public AssistantCostRates CostRates => new(
        _model.Pricing?.PromptPricePerMillion ?? _costFallback.CostPerMillionInputTokens,
        _model.Pricing?.CompletionPricePerMillion ?? _costFallback.CostPerMillionOutputTokens,
        _model.Pricing?.CacheReadPricePerMillion ?? _costFallback.CostPerMillionCachedTokens,
        _model.Pricing?.CacheWritePricePerMillion ?? _costFallback.CostPerMillionCacheWriteTokens);

    /// <summary>Discord's own message limit, so a long reply is cut rather than rejected by Discord.</summary>
    public int MaxResponseLength => 2000;
    public string TruncationSuffix => "…";

    public Task<IReadOnlyList<PromptPart>> BuildPartsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PromptPart>>([]);

    public Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken)
        => Task.FromResult(rawMessage);

    /// <summary>The ledger row only; feature requests keep no interaction log or daily metrics.</summary>
    public Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken)
    {
        if (result.UsageRecord is { } record)
        {
            record.LatencyMs = result.LatencyMs;
            record.InteractionLogId = null;
            _usageRecorder.Record(record);
        }

        return Task.CompletedTask;
    }
}
