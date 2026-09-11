using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Infrastructure.Services.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for DM-based AI assistant operations.
/// Routes messages through the shared <see cref="IAssistantMessagePipeline"/> (agentic loop,
/// pricing, truncation) with DM-scoped tools and conversation history.
/// </summary>
/// <remarks>
/// Error Handling Strategy (matches guild assistant):
/// - Top-level errors: Catch, log, return friendly error result
/// - Side-effect operations (metrics, logging): Catch, log, swallow
/// - Cancellation: Return early without error logging
///
/// The pipeline invocation and cost calculation are shared with <see cref="AssistantService"/>
/// via <see cref="IAssistantMessagePipeline"/>. Building the DM-scoped agent context (tool
/// registry, conversation history, prompt) and persisting usage/interaction logs live in
/// <see cref="IDmAssistantContextFactory"/> and the <c>DmAssistantContext</c> it returns.
/// </remarks>
public class DmAssistantService : IDmAssistantService
{
    private readonly ILogger<DmAssistantService> _logger;
    private readonly IAssistantMessagePipeline _pipeline;
    private readonly IBotOwnerResolver _ownerResolver;
    private readonly IDmAssistantContextFactory _contextFactory;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly DmAssistantOptions _options;

    public DmAssistantService(
        ILogger<DmAssistantService> logger,
        IAssistantMessagePipeline pipeline,
        IBotOwnerResolver ownerResolver,
        IDmAssistantContextFactory contextFactory,
        IOptions<DmAssistantOptions> options,
        ILlmUsageRecorder? usageRecorder = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        // Optional trailing parameter (default NoOpUsageRecorder) - see AssistantService's
        // constructor comment. Production DI always supplies the real recorder.
        _usageRecorder = usageRecorder ?? NoOpUsageRecorder.Instance;
    }

    /// <inheritdoc />
    public async Task<DmAssistantResponse> ProcessMessageAsync(
        ulong userId, string message, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        IAssistantContext? context = null;

        _logger.LogDebug("Processing DM assistant message from user {UserId}", userId);

        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return DmAssistantResponse.ErrorResult("Message cannot be empty.");
            }

            // Discord messages are capped at 4000 chars; truncate if somehow longer
            if (message.Length > 4000)
            {
                message = message[..4000];
            }

            var isOwner = await IsOwnerAsync(userId);

            if (!isOwner)
            {
                _logger.LogDebug("User {UserId} is not owner, returning placeholder", userId);

                var placeholderResponse = DmAssistantResponse.PlaceholderResult(_options.PlaceholderMessage);
                stopwatch.Stop();
                placeholderResponse.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

                await _contextFactory.LogPlaceholderInteractionAsync(
                    userId, message, placeholderResponse.Response ?? string.Empty, placeholderResponse.LatencyMs, ct);

                return placeholderResponse;
            }

            context = await _contextFactory.CreateAsync(userId, activeGuildId: null, ct);
            var formattedMessage = await context.FormatUserMessageAsync(message, ct);

            var pipelineResult = await _pipeline.RunAsync(formattedMessage, context, ct);

            stopwatch.Stop();
            pipelineResult.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

            if (!pipelineResult.Success || string.IsNullOrWhiteSpace(pipelineResult.Response))
            {
                _logger.LogWarning("Agent run failed for user {UserId}: {Error}",
                    userId, pipelineResult.ErrorMessage ?? "Empty response");

                // Mark the pipeline result itself as failed (not just the outward-facing response)
                // before recording usage, so a blank-but-"successful" run is not saved to
                // conversation history, not counted in daily metrics, and the interaction log
                // records Success=false with an error message.
                pipelineResult.Success = false;
                pipelineResult.ErrorMessage ??= _options.ErrorMessage;

                var errorResponse = DmAssistantResponse.ErrorResult(pipelineResult.ErrorMessage);
                errorResponse.LatencyMs = pipelineResult.LatencyMs;

                await context.RecordUsageAsync(message, pipelineResult, ct);

                return errorResponse;
            }

            var result = new DmAssistantResponse
            {
                Success = true,
                Response = pipelineResult.Response,
                IsOwner = true,
                InputTokens = pipelineResult.InputTokens,
                OutputTokens = pipelineResult.OutputTokens,
                CachedTokens = pipelineResult.CachedTokens,
                EstimatedCostUsd = pipelineResult.EstimatedCostUsd,
                LatencyMs = pipelineResult.LatencyMs
            };

            // Saves conversation turns (skipped if cleared), interaction log, and daily metrics
            await context.RecordUsageAsync(message, pipelineResult, ct);

            _logger.LogInformation(
                "DM assistant response sent to user {UserId}. " +
                "Tokens: {InputTokens} in / {OutputTokens} out / {CachedTokens} cached. " +
                "Cost: ${Cost:F4}. Latency: {LatencyMs}ms. ToolCalls: {ToolCalls}. Loops: {Loops}",
                userId, result.InputTokens, result.OutputTokens, result.CachedTokens,
                result.EstimatedCostUsd, result.LatencyMs, pipelineResult.ToolCalls, pipelineResult.LoopCount);

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("DM assistant processing cancelled for user {UserId}", userId);
            return DmAssistantResponse.ErrorResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing DM assistant message from user {UserId}", userId);

            // Same rationale as AssistantService's top-level catch: without this, an exception
            // escaping the pipeline (rather than coming back as a Success=false AgentRunResult)
            // leaves the exchange with no ledger row at all.
            RecordFailedUsage(context, userId, (int)stopwatch.ElapsedMilliseconds);

            return DmAssistantResponse.ErrorResult(_options.ErrorMessage);
        }
    }

    /// <summary>
    /// Best-effort ledger row for an exchange that failed outside the normal pipeline path. Never
    /// throws - a telemetry failure must not mask the original error.
    /// </summary>
    private void RecordFailedUsage(IAssistantContext? context, ulong userId, int latencyMs)
    {
        try
        {
            _usageRecorder.Record(new LlmUsageRecord
            {
                Timestamp = DateTime.UtcNow,
                Mode = context?.Mode ?? LlmMode.DmAssistant,
                UserId = userId,
                GuildId = null,
                Model = context?.Model ?? "unknown",
                CostUsd = 0m,
                CostSource = LlmCostSource.Estimated,
                LatencyMs = latencyMs,
                Success = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record failed-usage ledger row for user {UserId}", userId);
        }
    }

    private async Task<bool> IsOwnerAsync(ulong userId)
    {
        try
        {
            var ownerId = await _ownerResolver.GetOwnerIdAsync();
            return userId == ownerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get application owner info");
            return false;
        }
    }
}
