using cpike.Agent;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Shared implementation of <see cref="IAssistantMessagePipeline"/>. Runs one exchange on the
/// cpike.Agent loop (<see cref="AgentService"/>) in its own DI scope, seeded with the Discord caller,
/// then prices the token usage and truncates the response, identically for every assistant scope.
/// </summary>
/// <remarks>
/// The run gets its own scope because <see cref="AgentRunScope"/>, the tools and their scoped
/// dependencies are per run: a tool reads the caller from <see cref="DiscordRunContext"/>, a live
/// view over that scope, and the pipeline reads back what the tools set on it (a cleared
/// conversation) after the run.
/// </remarks>
public class AssistantMessagePipeline : IAssistantMessagePipeline
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssistantMessagePipeline> _logger;

    public AssistantMessagePipeline(IServiceScopeFactory scopeFactory, ILogger<AssistantMessagePipeline> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AssistantPipelineResult> RunAsync(
        string userMessage,
        IAssistantContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var parts = await context.BuildPartsAsync(cancellationToken);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TurnScopeSeeder>().Seed(DiscordRunContext.Seed(context.Identity, context.ModeKey));
        var run = services.GetRequiredService<DiscordRunContext>();
        var agent = services.GetRequiredService<AgentService>();

        var request = new AgentRunRequest
        {
            Prompt = userMessage,
            Mode = context.ModeKey,
            History = context.History,
            Parts = parts,
            Model = context.Model,
            MaxToolRounds = context.MaxToolCallIterations,
            MaxToolResultChars = context.MaxToolResultChars,
            MaxOutputTokens = context.MaxTokens,
            DisabledTools = context.DisabledTools,
            PreloadedSkills = context.PreloadedSkills,
            // The assistants have no web search; the DM assistant fetches pages with fetch_url.
            WebSearchEnabled = false,
        };

        AgentResult agentResult;
        try
        {
            agentResult = await agent.RunAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed run is a result, not an exception, for the callers: they post the configured
            // error message and record a failed usage row.
            _logger.LogError(ex, "Assistant run failed for {Mode}", context.ModeKey);
            return Failed(context, ex.Message);
        }

        var billed = agentResult.CostUsd;
        var cost = billed ?? EstimateCost(agentResult, context.CostRates);
        var resolvedModel = string.IsNullOrEmpty(agentResult.Model) ? context.Model ?? "unknown" : agentResult.Model;
        var inputTokens = ToInt(agentResult.InputTokens);
        var outputTokens = ToInt(agentResult.OutputTokens);
        var cachedTokens = ToInt(agentResult.CacheReadTokens);
        var cacheWriteTokens = ToInt(agentResult.CacheWriteTokens);

        var usageRecord = new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = context.Mode,
            UserId = context.Identity.UserId,
            GuildId = context.Identity.GuildId,
            Model = resolvedModel,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens,
            CacheWriteTokens = cacheWriteTokens,
            LlmCalls = agentResult.Iterations,
            ToolCalls = agentResult.ToolCalls,
            CostUsd = cost,
            CostSource = billed.HasValue ? LlmCostSource.Billed : LlmCostSource.Estimated,
            Success = true
            // LatencyMs is filled in by the caller once the stopwatch stops; InteractionLogId is
            // filled in by the context after its own AddAsync.
        };

        return new AssistantPipelineResult
        {
            Success = true,
            Response = Truncate(agentResult.Response, context.MaxResponseLength, context.TruncationSuffix),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens,
            CacheCreationTokens = cacheWriteTokens,
            CacheHit = cachedTokens > 0,
            ToolCalls = agentResult.ToolCalls,
            LoopCount = agentResult.Iterations,
            ToolNames = agentResult.ToolCallDetails.Select(call => call.Name).ToList(),
            ConversationCleared = run.ConversationCleared,
            StoppedOnMaxIterations = agentResult.StoppedOnMaxIterations,
            LoadedSkills = agentResult.LoadedSkills?.ToList() ?? new List<string>(),
            EstimatedCostUsd = cost,
            Model = resolvedModel,
            UsageRecord = usageRecord
        };
    }

    private static AssistantPipelineResult Failed(IAssistantContext context, string error) => new()
    {
        Success = false,
        ErrorMessage = error,
        Model = context.Model,
        UsageRecord = new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = context.Mode,
            UserId = context.Identity.UserId,
            GuildId = context.Identity.GuildId,
            Model = context.Model ?? "unknown",
            CostUsd = 0m,
            CostSource = LlmCostSource.Estimated,
            Success = false
        }
    };

    /// <summary>
    /// Cost from the configured per-million rates, for a run whose provider reported no billed cost
    /// (a BYOK call, or a provider that doesn't report one).
    /// </summary>
    private static decimal EstimateCost(AgentResult result, AssistantCostRates rates)
    {
        var inputCost = result.InputTokens * rates.InputPerMillion / 1_000_000m;
        var outputCost = result.OutputTokens * rates.OutputPerMillion / 1_000_000m;
        var cachedCost = result.CacheReadTokens * rates.CachedPerMillion / 1_000_000m;
        var cacheWriteCost = result.CacheWriteTokens * rates.CacheWritePerMillion / 1_000_000m;

        return inputCost + outputCost + cachedCost + cacheWriteCost;
    }

    private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;

    private static string Truncate(string response, int maxLength, string suffix)
    {
        if (string.IsNullOrEmpty(response) || response.Length <= maxLength)
        {
            return response;
        }

        var truncateAt = Math.Max(0, maxLength - suffix.Length);
        return response[..truncateAt] + suffix;
    }
}
