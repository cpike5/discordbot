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
/// Service implementation for AI assistant operations.
/// Handles rate limiting, consent checking, and delegates to the <see cref="IAssistantMessagePipeline"/>
/// for the actual LLM interaction.
/// </summary>
/// <remarks>
/// Error Handling Strategy:
/// - Top-level errors: Catch, log, record to APM, return friendly error result (graceful degradation)
/// - Side-effect operations (metrics, logging): Catch, log, record to APM, swallow (user experience unaffected)
/// - Cancellation: Return early without error logging
///
/// The shared cache-key-prefixed rate limiting and agentic-loop invocation live in
/// <see cref="AssistantRateLimiter"/> and <see cref="AssistantMessagePipeline"/> respectively,
/// which are also used by <see cref="DmAssistantService"/>. Scope-specific concerns (guild
/// enable/consent/channel checks, building the agent context, and logging usage) live in
/// <see cref="IAssistantAccessGate"/> and <see cref="IGuildAssistantContextFactory"/>.
/// </remarks>
public class AssistantService : IAssistantService
{
    private readonly ILogger<AssistantService> _logger;
    private readonly IAssistantMessagePipeline _pipeline;
    private readonly IAssistantRateLimiter _rateLimiter;
    private readonly IAssistantAccessGate _accessGate;
    private readonly IGuildAssistantContextFactory _contextFactory;
    private readonly IAssistantTelemetryReader _telemetryReader;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly AssistantOptions _options;

    public AssistantService(
        ILogger<AssistantService> logger,
        IAssistantMessagePipeline pipeline,
        IAssistantRateLimiter rateLimiter,
        IAssistantAccessGate accessGate,
        IGuildAssistantContextFactory contextFactory,
        IAssistantTelemetryReader telemetryReader,
        IOptions<AssistantOptions> options,
        ILlmUsageRecorder? usageRecorder = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _accessGate = accessGate ?? throw new ArgumentNullException(nameof(accessGate));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _telemetryReader = telemetryReader ?? throw new ArgumentNullException(nameof(telemetryReader));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        // Optional trailing parameter (default NoOpUsageRecorder), matching GuildAssistantContext's
        // pattern, so tests constructing this service directly keep compiling unchanged. Production
        // DI always supplies the real recorder - ILlmUsageRecorder is registered ungated.
        _usageRecorder = usageRecorder ?? NoOpUsageRecorder.Instance;
    }

    /// <inheritdoc />
    public async Task<AssistantResponseResult> AskQuestionAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        string question,
        bool callerCanMutate = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        IAssistantContext? context = null;

        _logger.LogDebug(
            "Processing assistant question from user {UserId} in guild {GuildId}, channel {ChannelId}",
            userId, guildId, channelId);

        try
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return AssistantResponseResult.ErrorResult("Question cannot be empty.");
            }

            if (question.Length > _options.Messages.MaxQuestionLength)
            {
                return AssistantResponseResult.ErrorResult(
                    $"Question is too long. Maximum length is {_options.Messages.MaxQuestionLength} characters.");
            }

            if (!await IsEnabledForGuildAsync(guildId, cancellationToken))
            {
                return AssistantResponseResult.ErrorResult(
                    "The AI assistant is not enabled for this server.");
            }

            if (!await IsAllowedInChannelAsync(guildId, channelId, cancellationToken))
            {
                return AssistantResponseResult.ErrorResult(
                    "The AI assistant is not allowed in this channel.");
            }

            if (!await _accessGate.HasConsentAsync(userId, cancellationToken))
            {
                return AssistantResponseResult.ErrorResult(
                    "You need to grant consent before using the AI assistant. Use `/consent grant type:assistant` to enable this feature.");
            }

            var rateLimit = await _accessGate.GetRateLimitAsync(guildId, cancellationToken);
            context = await _contextFactory.CreateAsync(
                guildId, channelId, userId, messageId, rateLimit, question, callerCanMutate, cancellationToken);

            var rateLimitResult = await _rateLimiter.CheckAsync(
                context.RateLimitCacheKeyPrefix,
                context.RateLimitScopeKey,
                context.RateLimit ?? rateLimit,
                context.RateLimitWindowMinutes,
                cancellationToken);
            if (!rateLimitResult.IsAllowed)
            {
                return AssistantResponseResult.ErrorResult(
                    rateLimitResult.Message ?? "You have exceeded your rate limit. Please try again later.");
            }

            var formattedMessage = await context.FormatUserMessageAsync(question, cancellationToken);

            var pipelineResult = await _pipeline.RunAsync(formattedMessage, context, cancellationToken);

            stopwatch.Stop();
            pipelineResult.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

            var result = new AssistantResponseResult
            {
                Success = pipelineResult.Success,
                Response = pipelineResult.Response,
                ErrorMessage = pipelineResult.ErrorMessage,
                InputTokens = pipelineResult.InputTokens,
                OutputTokens = pipelineResult.OutputTokens,
                CachedTokens = pipelineResult.CachedTokens,
                CacheCreationTokens = pipelineResult.CacheCreationTokens,
                CacheHit = pipelineResult.CacheHit,
                ToolCalls = pipelineResult.ToolCalls,
                LatencyMs = pipelineResult.LatencyMs,
                EstimatedCostUsd = pipelineResult.EstimatedCostUsd
            };

            if (pipelineResult.Success)
            {
                _rateLimiter.RecordUsage(context.RateLimitCacheKeyPrefix, context.RateLimitScopeKey, context.RateLimitWindowMinutes);
            }

            await context.RecordUsageAsync(question, pipelineResult, cancellationToken);

            _logger.LogInformation(
                "Assistant question processed. Success: {Success}, Latency: {LatencyMs}ms, Tokens: {TotalTokens}, Cost: ${Cost:F4}",
                result.Success, result.LatencyMs, result.InputTokens + result.OutputTokens, result.EstimatedCostUsd);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogDebug(
                "Assistant question processing cancelled for user {UserId} in guild {GuildId}",
                userId, guildId);
            return AssistantResponseResult.ErrorResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Error processing assistant question from user {UserId} in guild {GuildId}",
                userId, guildId);

            if (_options.Cost.EnableCostTracking)
            {
                await _telemetryReader.IncrementFailedRequestAsync(guildId, cancellationToken);
            }

            // An exception here (a provider error the agent loop didn't already turn into a
            // Success=false AgentRunResult, or a failure before the pipeline even ran) would
            // otherwise leave this exchange with no ledger row at all. Record what we know - zero
            // tokens/cost if the pipeline never ran, the resolved model if context was built.
            RecordFailedUsage(context, userId, guildId, (int)stopwatch.ElapsedMilliseconds);

            return AssistantResponseResult.ErrorResult(_options.Messages.ErrorMessage);
        }
    }

    /// <summary>
    /// Best-effort ledger row for an exchange that failed outside the normal pipeline path (see the
    /// top-level catch in <see cref="AskQuestionAsync"/>). Never throws - a telemetry failure must
    /// not mask the original error.
    /// </summary>
    private void RecordFailedUsage(IAssistantContext? context, ulong userId, ulong guildId, int latencyMs)
    {
        try
        {
            _usageRecorder.Record(new LlmUsageRecord
            {
                Timestamp = DateTime.UtcNow,
                Mode = context?.Mode ?? LlmMode.GuildAssistant,
                UserId = userId,
                GuildId = guildId,
                Model = context?.Model ?? "unknown",
                CostUsd = 0m,
                CostSource = LlmCostSource.Estimated,
                LatencyMs = latencyMs,
                Success = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record failed-usage ledger row for user {UserId} in guild {GuildId}", userId, guildId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledForGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        return await _accessGate.IsEnabledForGuildAsync(guildId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> IsAllowedInChannelAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        return _accessGate.IsChannelAllowedAsync(guildId, channelId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitCheckResult> CheckRateLimitAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var rateLimit = await _accessGate.GetRateLimitAsync(guildId, cancellationToken);

        return await _rateLimiter.CheckAsync(
            GuildAssistantContext.RateLimitPrefix,
            $"{guildId}:{userId}",
            rateLimit,
            _options.RateLimits.RateLimitWindowMinutes,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        return _telemetryReader.GetUsageMetricsAsync(guildId, date, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return _telemetryReader.GetUsageMetricsRangeAsync(guildId, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return _telemetryReader.GetRecentInteractionsAsync(guildId, limit, cancellationToken);
    }
}
