using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Orchestrates the agentic loop (tool use cycles).
/// Manages conversation history and iterates until a final response is generated.
/// </summary>
public class AgentRunner : IAgentRunner
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<AgentRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the AgentRunner.
    /// </summary>
    /// <param name="llmClient">LLM client for making completion requests.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public AgentRunner(ILlmClient llmClient, ILogger<AgentRunner> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentRunResult> RunAsync(
        string userMessage,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug(
            "Starting agent run for user {UserId} in guild {GuildId}. Max iterations: {MaxIterations}",
            context.ExecutionContext.UserId,
            context.ExecutionContext.GuildId,
            context.MaxToolCallIterations);

        // Initialize conversation history with the user's message
        var conversationHistory = new List<LlmMessage>
        {
            new()
            {
                Role = LlmRole.User,
                Content = userMessage
            }
        };

        // Initialize token usage tracking
        var totalUsage = new LlmUsage();
        var totalToolCalls = 0;
        var loopCount = 0;

        // Build the initial LLM request
        var request = new LlmRequest
        {
            SystemPrompt = context.SystemPrompt,
            Messages = conversationHistory,
            Tools = context.ToolRegistry?.GetEnabledTools().ToList(),
            Model = context.Model,
            MaxTokens = context.MaxTokens,
            Temperature = context.Temperature,
            EnablePromptCaching = true
        };

        _logger.LogDebug(
            "Initial request configured with {ToolCount} tools, MaxTokens: {MaxTokens}, Temperature: {Temperature}",
            request.Tools?.Count ?? 0,
            context.MaxTokens,
            context.Temperature);

        // Agentic loop
        while (loopCount < context.MaxToolCallIterations)
        {
            loopCount++;

            _logger.LogDebug(
                "Agent loop iteration {Iteration}/{MaxIterations}",
                loopCount,
                context.MaxToolCallIterations);

            // Call the LLM
            var response = await _llmClient.CompleteAsync(request, cancellationToken);

            // Check for LLM failure
            if (!response.Success)
            {
                _logger.LogError(
                    "LLM completion failed on iteration {Iteration}: {Error}",
                    loopCount,
                    response.ErrorMessage);

                return new AgentRunResult
                {
                    Success = false,
                    ErrorMessage = response.ErrorMessage ?? "LLM completion failed",
                    LoopCount = loopCount,
                    TotalToolCalls = totalToolCalls,
                    TotalUsage = totalUsage
                };
            }

            // Accumulate token usage
            totalUsage.InputTokens += response.Usage.InputTokens;
            totalUsage.OutputTokens += response.Usage.OutputTokens;
            totalUsage.CachedTokens += response.Usage.CachedTokens;
            totalUsage.CacheWriteTokens += response.Usage.CacheWriteTokens;

            if (response.Usage.EstimatedCost.HasValue)
            {
                totalUsage.EstimatedCost = (totalUsage.EstimatedCost ?? 0) + response.Usage.EstimatedCost.Value;
            }

            _logger.LogDebug(
                "LLM response received. StopReason: {StopReason}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, CachedTokens: {CachedTokens}",
                response.StopReason,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.Usage.CachedTokens);

            // Handle stop reason
            switch (response.StopReason)
            {
                case LlmStopReason.EndTurn:
                    // Final response reached
                    _logger.LogInformation(
                        "Agent run completed successfully. Iterations: {Iterations}, ToolCalls: {ToolCalls}, TotalTokens: {TotalTokens}",
                        loopCount,
                        totalToolCalls,
                        totalUsage.TotalTokens);

                    return new AgentRunResult
                    {
                        Success = true,
                        Response = response.Content ?? string.Empty,
                        LoopCount = loopCount,
                        TotalToolCalls = totalToolCalls,
                        TotalUsage = totalUsage
                    };

                case LlmStopReason.ToolUse:
                    // LLM wants to use tools
                    if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                    {
                        _logger.LogWarning(
                            "StopReason is ToolUse but no tool calls provided. Treating as error");

                        return new AgentRunResult
                        {
                            Success = false,
                            ErrorMessage = "LLM indicated tool use but provided no tool calls",
                            LoopCount = loopCount,
                            TotalToolCalls = totalToolCalls,
                            TotalUsage = totalUsage
                        };
                    }

                    if (context.ToolRegistry == null)
                    {
                        _logger.LogWarning(
                            "LLM requested tool use but no ToolRegistry is configured");

                        return new AgentRunResult
                        {
                            Success = false,
                            ErrorMessage = "Tool use requested but no ToolRegistry configured",
                            LoopCount = loopCount,
                            TotalToolCalls = totalToolCalls,
                            TotalUsage = totalUsage
                        };
                    }

                    _logger.LogDebug(
                        "Processing {ToolCallCount} tool calls",
                        response.ToolCalls.Count);

                    // Add assistant message with tool calls to conversation history
                    conversationHistory.Add(new LlmMessage
                    {
                        Role = LlmRole.Assistant,
                        Content = response.Content ?? string.Empty,
                        ToolCalls = response.ToolCalls
                    });

                    // Execute each tool call
                    var toolResults = new List<LlmToolResult>();

                    foreach (var toolCall in response.ToolCalls)
                    {
                        totalToolCalls++;

                        _logger.LogDebug(
                            "Executing tool {ToolName} (ID: {ToolCallId})",
                            toolCall.Name,
                            toolCall.Id);

                        try
                        {
                            var executionResult = await context.ToolRegistry.ExecuteToolAsync(
                                toolCall.Name,
                                toolCall.Input,
                                context.ExecutionContext,
                                cancellationToken);

                            // Convert ToolExecutionResult to LlmToolResult
                            JsonElement contentElement;
                            if (executionResult.Success && executionResult.Data.HasValue)
                            {
                                contentElement = executionResult.Data.Value;
                            }
                            else if (!executionResult.Success)
                            {
                                // Convert error message to JSON
                                contentElement = JsonSerializer.SerializeToElement(new
                                {
                                    error = executionResult.ErrorMessage ?? "Unknown error"
                                });
                            }
                            else
                            {
                                // Success but no data
                                contentElement = JsonSerializer.SerializeToElement(new
                                {
                                    success = true
                                });
                            }

                            toolResults.Add(new LlmToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = contentElement,
                                IsError = !executionResult.Success
                            });

                            if (executionResult.Success)
                            {
                                _logger.LogDebug(
                                    "Tool {ToolName} executed successfully",
                                    toolCall.Name);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Tool {ToolName} execution failed: {Error}",
                                    toolCall.Name,
                                    executionResult.ErrorMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Exception executing tool {ToolName}",
                                toolCall.Name);

                            // Return error result for this tool
                            var errorElement = JsonSerializer.SerializeToElement(new
                            {
                                error = $"Tool execution exception: {ex.Message}"
                            });

                            toolResults.Add(new LlmToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = errorElement,
                                IsError = true
                            });
                        }
                    }

                    // Add tool results as a user message
                    conversationHistory.Add(new LlmMessage
                    {
                        Role = LlmRole.User,
                        ToolResults = toolResults
                    });

                    // Update the request for the next iteration
                    request.Messages = conversationHistory;

                    _logger.LogDebug(
                        "Tool execution cycle complete. Continuing to next iteration");

                    break;

                case LlmStopReason.MaxTokens:
                    _logger.LogWarning(
                        "Agent run stopped due to max tokens limit. Returning partial response");

                    return new AgentRunResult
                    {
                        Success = true,
                        Response = response.Content ?? string.Empty,
                        LoopCount = loopCount,
                        TotalToolCalls = totalToolCalls,
                        TotalUsage = totalUsage,
                        ErrorMessage = "Response truncated due to max tokens limit"
                    };

                case LlmStopReason.Error:
                    _logger.LogError(
                        "LLM returned error stop reason: {Error}",
                        response.ErrorMessage);

                    return new AgentRunResult
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage ?? "LLM returned error stop reason",
                        LoopCount = loopCount,
                        TotalToolCalls = totalToolCalls,
                        TotalUsage = totalUsage
                    };

                default:
                    _logger.LogWarning(
                        "Unexpected stop reason: {StopReason}",
                        response.StopReason);

                    return new AgentRunResult
                    {
                        Success = false,
                        ErrorMessage = $"Unexpected stop reason: {response.StopReason}",
                        LoopCount = loopCount,
                        TotalToolCalls = totalToolCalls,
                        TotalUsage = totalUsage
                    };
            }
        }

        // Exceeded max iterations
        _logger.LogWarning(
            "Agent run exceeded maximum iterations ({MaxIterations}). Returning incomplete result",
            context.MaxToolCallIterations);

        return new AgentRunResult
        {
            Success = false,
            ErrorMessage = $"Exceeded maximum tool call iterations ({context.MaxToolCallIterations})",
            LoopCount = loopCount,
            TotalToolCalls = totalToolCalls,
            TotalUsage = totalUsage
        };
    }
}
