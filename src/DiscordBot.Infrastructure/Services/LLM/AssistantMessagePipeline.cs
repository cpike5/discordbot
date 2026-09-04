using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;

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
            ConversationHistory = context.ConversationHistory.Count > 0 ? context.ConversationHistory : null
        };

        var agentResult = await _agentRunner.RunAsync(userMessage, agentContext, cancellationToken);

        var cost = CalculateCost(agentResult.TotalUsage, context.CostRates);

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
            EstimatedCostUsd = cost
        };
    }

    private static decimal CalculateCost(LlmUsage usage, AssistantCostRates rates)
    {
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
