using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Shared implementation of <see cref="IAssistantMessagePipeline"/>. Builds the
/// <see cref="AgentContext"/> from an <see cref="IAssistantContext"/>, runs the agentic loop,
/// prices the resulting token usage, and truncates the response — identically for the guild
/// and DM assistants.
/// </summary>
public class AssistantMessagePipeline : IAssistantMessagePipeline
{
    private readonly IAgentRunner _agentRunner;

    public AssistantMessagePipeline(IAgentRunner agentRunner)
    {
        _agentRunner = agentRunner ?? throw new ArgumentNullException(nameof(agentRunner));
    }

    /// <inheritdoc />
    public async Task<AssistantPipelineResult> RunAsync(
        string userMessage,
        IAssistantContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var systemPrompt = await context.BuildSystemPromptAsync(cancellationToken);

        var agentContext = new AgentContext
        {
            SystemPrompt = systemPrompt,
            ToolRegistry = context.ToolRegistry,
            ExecutionContext = context.ExecutionContext,
            Model = context.Model,
            MaxTokens = context.MaxTokens,
            Temperature = context.Temperature,
            MaxToolCallIterations = context.MaxToolCallIterations,
            MaxToolResultChars = context.MaxToolResultChars,
            ToolExecutionTimeoutMs = context.ToolExecutionTimeoutMs,
            DuplicateToolCallLimit = context.DuplicateToolCallLimit,
            ConversationHistory = context.ConversationHistory.Count > 0 ? context.ConversationHistory : null,
            Skills = context.Skills,
            RunKind = context.Mode.ToString()
        };

        var agentResult = await _agentRunner.RunAsync(userMessage, agentContext, cancellationToken);

        var cost = CalculateCost(agentResult.TotalUsage, context.CostRates);
        var resolvedModel = agentResult.Model ?? context.Model ?? "unknown";

        var usageRecord = new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = context.Mode,
            UserId = context.ExecutionContext.UserId,
            GuildId = context.ExecutionContext.GuildId,
            Model = resolvedModel,
            InputTokens = agentResult.TotalUsage.InputTokens,
            OutputTokens = agentResult.TotalUsage.OutputTokens,
            CachedTokens = agentResult.TotalUsage.CachedTokens,
            CacheWriteTokens = agentResult.TotalUsage.CacheWriteTokens,
            LlmCalls = agentResult.LoopCount,
            ToolCalls = agentResult.TotalToolCalls,
            CostUsd = cost,
            CostSource = agentResult.TotalUsage.EstimatedCost.HasValue ? LlmCostSource.Billed : LlmCostSource.Estimated,
            Success = agentResult.Success
            // LatencyMs is filled in by the caller once the stopwatch stops; InteractionLogId is
            // filled in by the context after its own AddAsync.
        };

        return new AssistantPipelineResult
        {
            Success = agentResult.Success,
            Response = agentResult.Success ? Truncate(agentResult.Response, context.MaxResponseLength, context.TruncationSuffix) : null,
            ErrorMessage = agentResult.ErrorMessage,
            InputTokens = agentResult.TotalUsage.InputTokens,
            OutputTokens = agentResult.TotalUsage.OutputTokens,
            CachedTokens = agentResult.TotalUsage.CachedTokens,
            CacheCreationTokens = agentResult.TotalUsage.CacheWriteTokens,
            CacheHit = agentResult.TotalUsage.CachedTokens > 0,
            ToolCalls = agentResult.TotalToolCalls,
            LoopCount = agentResult.LoopCount,
            ToolNames = agentResult.ToolNames,
            ConversationCleared = agentResult.ConversationCleared,
            StoppedOnMaxIterations = agentResult.StoppedOnMaxIterations,
            EstimatedCostUsd = cost,
            Model = resolvedModel,
            UsageRecord = usageRecord
        };
    }

    /// <summary>
    /// Cost of a run in USD. OpenRouter reports what it actually billed, so that figure wins when
    /// present; the configured per-million rates are the fallback for a response that carried no
    /// cost (a BYOK call, or a provider that doesn't report one). A null cost means "not reported",
    /// never zero.
    /// </summary>
    private static decimal CalculateCost(LlmUsage usage, AssistantCostRates rates)
    {
        if (usage.EstimatedCost.HasValue)
        {
            return usage.EstimatedCost.Value;
        }

        var inputCost = usage.InputTokens * rates.InputPerMillion / 1_000_000m;
        var outputCost = usage.OutputTokens * rates.OutputPerMillion / 1_000_000m;
        var cachedCost = usage.CachedTokens * rates.CachedPerMillion / 1_000_000m;
        var cacheWriteCost = usage.CacheWriteTokens * rates.CacheWritePerMillion / 1_000_000m;

        return inputCost + outputCost + cachedCost + cacheWriteCost;
    }

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
