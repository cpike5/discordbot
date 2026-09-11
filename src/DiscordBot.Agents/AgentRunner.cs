using System.Buffers;
using System.Text;
using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Contracts.Enums;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Agents;

/// <summary>
/// Orchestrates the agentic loop (tool use cycles).
/// Manages conversation history and iterates until a final response is generated.
/// </summary>
public class AgentRunner : IAgentRunner
{
    /// <summary>How deep argument normalization descends before collapsing to a marker.</summary>
    private const int MaxNormalizedDepth = 8;

    /// <summary>Ceiling on the normalized-argument half of a duplicate-guard key.</summary>
    private const int MaxNormalizedKeyChars = 4000;

    /// <summary>Stands in for anything below <see cref="MaxNormalizedDepth"/>.</summary>
    private const string DepthLimitMarker = "<depth-limited>";

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

        // Initialize conversation history: use pre-existing history if provided, else start fresh
        List<LlmMessage> conversationHistory;
        if (context.ConversationHistory is { Count: > 0 })
        {
            conversationHistory = new List<LlmMessage>(context.ConversationHistory)
            {
                new() { Role = LlmRole.User, Content = userMessage }
            };

            _logger.LogDebug(
                "Initialized conversation from {HistoryCount} existing messages + new user message",
                context.ConversationHistory.Count);
        }
        else
        {
            conversationHistory = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = userMessage }
            };
        }

        // Initialize token usage tracking
        var totalUsage = new LlmUsage();
        var totalToolCalls = 0;
        var loopCount = 0;
        var conversationCleared = false;
        string? lastModel = null;
        var toolNames = new List<string>();

        // Per-run duplicate-call counters, keyed by tool name plus its normalised arguments.
        var duplicateCallCounts = new Dictionary<string, int>(StringComparer.Ordinal);

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

            // Call the LLM. An exception here (the HTTP call itself faulting, not a modeled
            // response.Success == false) would otherwise propagate out of RunAsync and discard
            // totalUsage accumulated over any prior iterations of this loop - the caller's pipeline
            // builds the usage ledger row from the returned AgentRunResult, so a thrown exception
            // means that row, and the tokens already spent on this run, are silently lost. Catching
            // it here keeps every exit from this loop going through the normal AgentRunResult path.
            LlmResponse response;
            try
            {
                response = await _llmClient.CompleteAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "LLM client threw calling CompleteAsync on iteration {Iteration}",
                    loopCount);

                return new AgentRunResult
                {
                    Success = false,
                    ErrorMessage = $"LLM request failed: {ex.Message}",
                    LoopCount = loopCount,
                    TotalToolCalls = totalToolCalls,
                    ToolNames = toolNames,
                    TotalUsage = totalUsage,
                    Model = lastModel
                };
            }

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
                    ToolNames = toolNames,
                    TotalUsage = totalUsage,
                    Model = lastModel
                };
            }

            lastModel = response.Model ?? lastModel;

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
                        ToolNames = toolNames,
                        TotalUsage = totalUsage,
                        ConversationCleared = conversationCleared,
                        Model = lastModel
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
                            TotalUsage = totalUsage,
                            Model = lastModel
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
                            TotalUsage = totalUsage,
                            Model = lastModel
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
                        if (IsRefusedAsDuplicate(toolCall, context, duplicateCallCounts, out var refusal))
                        {
                            // The tool is never entered, so this is not counted as a tool call and
                            // does not reach ToolNames: nothing ran.
                            toolResults.Add(refusal);
                            continue;
                        }

                        totalToolCalls++;
                        toolNames.Add(toolCall.Name);

                        _logger.LogDebug(
                            "Executing tool {ToolName} (ID: {ToolCallId})",
                            toolCall.Name,
                            toolCall.Id);

                        try
                        {
                            var executionResult = await ExecuteWithDeadlineAsync(
                                toolCall, context, cancellationToken);

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

                            // Cap before the result enters history: it is re-sent on every later
                            // iteration, so an oversized one is paid for again each time.
                            contentElement = ToolResultLimiter.Cap(contentElement, context.MaxToolResultChars);

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

                                if (string.Equals(toolCall.Name, "clear_conversation", StringComparison.OrdinalIgnoreCase))
                                {
                                    conversationCleared = true;
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Tool {ToolName} execution failed: {Error}",
                                    toolCall.Name,
                                    executionResult.ErrorMessage);
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // The caller cancelled (interaction expired, host shutting down). That
                            // is not a tool failure and must not be laundered into a tool result.
                            throw;
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
                                Content = ToolResultLimiter.Cap(errorElement, context.MaxToolResultChars),
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
                        ToolNames = toolNames,
                        TotalUsage = totalUsage,
                        ErrorMessage = "Response truncated due to max tokens limit",
                        Model = lastModel
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
                        TotalUsage = totalUsage,
                        Model = lastModel
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
                        TotalUsage = totalUsage,
                        Model = lastModel
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
            ToolNames = toolNames,
            TotalUsage = totalUsage,
            Model = lastModel
        };
    }

    /// <summary>
    /// Runs one tool under its own deadline, converting an expired deadline into a directive error
    /// result rather than letting it cancel the run.
    /// </summary>
    /// <remarks>
    /// The <c>when</c> clause is load-bearing: an outer cancellation (the Discord interaction
    /// expiring, the host shutting down) must still propagate as cancellation and not be laundered
    /// into a tool result.
    /// </remarks>
    private async Task<ToolExecutionResult> ExecuteWithDeadlineAsync(
        LlmToolCall toolCall,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (context.ToolExecutionTimeoutMs > 0)
        {
            toolCts.CancelAfter(context.ToolExecutionTimeoutMs);
        }

        try
        {
            return await context.ToolRegistry!.ExecuteToolAsync(
                toolCall.Name,
                toolCall.Input,
                context.ExecutionContext,
                toolCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Tool {ToolName} exceeded its {TimeoutMs}ms deadline and was abandoned",
                toolCall.Name,
                context.ToolExecutionTimeoutMs);

            return ToolExecutionResult.CreateError(
                $"Tool '{toolCall.Name}' timed out after {context.ToolExecutionTimeoutMs}ms. " +
                "Do not retry it; answer with what you have or try a different approach.");
        }
    }

    /// <summary>
    /// Counts this call against the run's duplicate budget and, once the budget is spent, produces
    /// the refusal to return in place of executing the tool.
    /// </summary>
    /// <remarks>
    /// The refusal is a normal result carrying a directive, not an error: flagging it would both
    /// inflate tool-error metrics and prepend <c>Error: </c> on the wire, which reads to the model
    /// as a malfunction rather than an instruction.
    /// </remarks>
    /// <returns>True when the call must not be executed.</returns>
    private bool IsRefusedAsDuplicate(
        LlmToolCall toolCall,
        AgentContext context,
        Dictionary<string, int> counts,
        out LlmToolResult refusal)
    {
        refusal = null!;

        if (context.DuplicateToolCallLimit <= 0)
        {
            return false;
        }

        var key = BuildDuplicateKey(toolCall);
        counts.TryGetValue(key, out var seen);
        counts[key] = seen + 1;

        if (seen < context.DuplicateToolCallLimit)
        {
            return false;
        }

        _logger.LogWarning(
            "Refusing call {CallNumber} to tool {ToolName} with arguments already used {Limit} time(s) in this run",
            seen + 1,
            toolCall.Name,
            context.DuplicateToolCallLimit);

        refusal = new LlmToolResult
        {
            ToolCallId = toolCall.Id,
            Content = JsonSerializer.SerializeToElement(new
            {
                error = "repeated_call",
                message = $"You have already called '{toolCall.Name}' with these exact arguments "
                    + $"{context.DuplicateToolCallLimit} time(s) in this run. Do not repeat it: use the "
                    + "result you already have, call it with different arguments, or answer with what you know.",
            }),
            IsError = false,
        };

        return true;
    }

    /// <summary>
    /// The duplicate-guard key for one call: the tool name, a separator that cannot occur in a tool
    /// name, and the arguments normalised so property order does not matter.
    /// </summary>
    private static string BuildDuplicateKey(LlmToolCall toolCall) =>
        toolCall.Name + '\u001f' + NormalizeArguments(toolCall.Input);

    /// <summary>
    /// Re-serializes an argument object with object properties sorted by name, so
    /// <c>{"a":1,"b":2}</c> and <c>{"b":2,"a":1}</c> produce one key.
    /// </summary>
    /// <remarks>
    /// Depth-limited and length-capped against pathological inputs. Both limits can in principle
    /// collapse two genuinely different argument sets onto one key; the cost of that is one refused
    /// call carrying a directive, which is cheaper than walking an adversarial payload.
    /// </remarks>
    private static string NormalizeArguments(JsonElement arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteNormalized(arguments, writer, MaxNormalizedDepth);
        }

        var text = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return text.Length <= MaxNormalizedKeyChars ? text : text[..MaxNormalizedKeyChars];
    }

    /// <summary>Writes one element with object properties in ordinal name order.</summary>
    private static void WriteNormalized(JsonElement element, Utf8JsonWriter writer, int depthRemaining)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object when depthRemaining > 0:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteNormalized(property.Value, writer, depthRemaining - 1);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array when depthRemaining > 0:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteNormalized(item, writer, depthRemaining - 1);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.Object:
            case JsonValueKind.Array:
                // Past the depth limit everything collapses to one marker.
                writer.WriteStringValue(DepthLimitMarker);
                break;

            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
