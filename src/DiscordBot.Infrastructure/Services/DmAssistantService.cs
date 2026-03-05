using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for DM-based AI assistant operations.
/// Handles owner detection, conversation history management, and LLM interactions.
/// </summary>
/// <remarks>
/// Error Handling Strategy (matches guild assistant):
/// - Top-level errors: Catch, log, return friendly error result
/// - Side-effect operations (metrics, logging): Catch, log, swallow
/// - Cancellation: Return early without error logging
/// </remarks>
public class DmAssistantService : IDmAssistantService
{
    private readonly ILogger<DmAssistantService> _logger;
    private readonly ILlmClient _llmClient;
    private readonly IDmConversationMessageRepository _conversationRepo;
    private readonly IDmAssistantInteractionLogRepository _interactionLogRepo;
    private readonly IDmAssistantUsageMetricsRepository _metricsRepo;
    private readonly IBotOwnerResolver _ownerResolver;
    private readonly DmAssistantOptions _options;

    private string? _cachedOwnerSystemPrompt;

    public DmAssistantService(
        ILogger<DmAssistantService> logger,
        ILlmClient llmClient,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        IBotOwnerResolver ownerResolver,
        IOptions<DmAssistantOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<DmAssistantResponse> ProcessMessageAsync(
        ulong userId, string message, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Processing DM assistant message from user {UserId}", userId);

        try
        {
            var isOwner = await IsOwnerAsync(userId);

            // Non-owner: return placeholder and log interaction
            if (!isOwner)
            {
                _logger.LogDebug("User {UserId} is not owner, returning placeholder", userId);

                var placeholderResponse = DmAssistantResponse.PlaceholderResult(_options.PlaceholderMessage);
                stopwatch.Stop();
                placeholderResponse.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

                if (_options.LogInteractions)
                {
                    await LogInteractionAsync(userId, false, message, placeholderResponse, ct);
                }

                return placeholderResponse;
            }

            // Load system prompt (cached in memory)
            var systemPrompt = await LoadSystemPromptAsync(ct);

            // Load conversation history
            var history = await _conversationRepo.GetRecentByUserAsync(
                userId, _options.MaxConversationMessages, ct);

            // Build message list from history + current message
            var messages = new List<LlmMessage>();
            foreach (var historyMsg in history)
            {
                messages.Add(new LlmMessage
                {
                    Role = historyMsg.Role == "assistant" ? LlmRole.Assistant : LlmRole.User,
                    Content = historyMsg.Content
                });
            }
            messages.Add(new LlmMessage { Role = LlmRole.User, Content = message });

            // Build LLM request
            var request = new LlmRequest
            {
                SystemPrompt = systemPrompt,
                Messages = messages,
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                EnablePromptCaching = _options.EnablePromptCaching
            };

            // Call LLM
            var llmResponse = await _llmClient.CompleteAsync(request, ct);

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            if (!llmResponse.Success || string.IsNullOrWhiteSpace(llmResponse.Content))
            {
                _logger.LogWarning("LLM call failed for user {UserId}: {Error}",
                    userId, llmResponse.ErrorMessage ?? "Empty response");

                var errorResponse = DmAssistantResponse.ErrorResult(
                    llmResponse.ErrorMessage ?? _options.ErrorMessage);
                errorResponse.LatencyMs = latencyMs;

                if (_options.LogInteractions)
                {
                    await LogInteractionAsync(userId, true, message, errorResponse, ct);
                }

                return errorResponse;
            }

            var responseText = TruncateResponse(llmResponse.Content);

            // Save conversation messages
            var utcNow = DateTime.UtcNow;
            await _conversationRepo.AddAsync(new DmConversationMessage
            {
                UserId = userId,
                Role = "user",
                Content = message,
                Timestamp = utcNow
            }, ct);

            await _conversationRepo.AddAsync(new DmConversationMessage
            {
                UserId = userId,
                Role = "assistant",
                Content = responseText,
                Timestamp = utcNow
            }, ct);

            // Trim conversation history to sliding window
            await _conversationRepo.DeleteOldestByUserAsync(
                userId, _options.MaxConversationMessages, ct);

            // Calculate cost
            var cost = CalculateCost(llmResponse.Usage);

            var result = new DmAssistantResponse
            {
                Success = true,
                Response = responseText,
                IsOwner = true,
                InputTokens = llmResponse.Usage.InputTokens,
                OutputTokens = llmResponse.Usage.OutputTokens,
                CachedTokens = llmResponse.Usage.CachedTokens,
                EstimatedCostUsd = cost,
                LatencyMs = latencyMs
            };

            // Log interaction (swallow errors)
            if (_options.LogInteractions)
            {
                await LogInteractionAsync(userId, true, message, result, ct);
            }

            // Update daily metrics (swallow errors)
            if (_options.EnableCostTracking)
            {
                await UpdateDailyMetricsAsync(userId, result, ct);
            }

            _logger.LogInformation(
                "DM assistant response sent to user {UserId}. " +
                "Tokens: {InputTokens} in / {OutputTokens} out / {CachedTokens} cached. " +
                "Cost: ${Cost:F4}. Latency: {LatencyMs}ms",
                userId, result.InputTokens, result.OutputTokens, result.CachedTokens,
                result.EstimatedCostUsd, result.LatencyMs);

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
            return DmAssistantResponse.ErrorResult(_options.ErrorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsOwnerAsync(ulong userId)
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

    private async Task<string> LoadSystemPromptAsync(CancellationToken ct)
    {
        if (_cachedOwnerSystemPrompt != null)
        {
            return _cachedOwnerSystemPrompt;
        }

        try
        {
            _cachedOwnerSystemPrompt = await File.ReadAllTextAsync(
                _options.OwnerSystemPromptPath, ct);
            return _cachedOwnerSystemPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load owner system prompt from {Path}, using fallback",
                _options.OwnerSystemPromptPath);
            return "You are a helpful AI assistant. Be concise and accurate.";
        }
    }

    private string TruncateResponse(string response)
    {
        if (string.IsNullOrEmpty(response) || response.Length <= _options.MaxResponseLength)
        {
            return response;
        }

        var truncateAt = _options.MaxResponseLength - _options.TruncationSuffix.Length;
        return response[..truncateAt] + _options.TruncationSuffix;
    }

    private decimal CalculateCost(LlmUsage usage)
    {
        var inputCost = usage.InputTokens * _options.CostPerMillionInputTokens / 1_000_000m;
        var outputCost = usage.OutputTokens * _options.CostPerMillionOutputTokens / 1_000_000m;
        var cachedCost = usage.CachedTokens * _options.CostPerMillionCachedTokens / 1_000_000m;

        return inputCost + outputCost + cachedCost;
    }

    private async Task LogInteractionAsync(
        ulong userId, bool isOwner, string message,
        DmAssistantResponse result, CancellationToken ct)
    {
        try
        {
            var log = new DmAssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                IsOwner = isOwner,
                Message = message.Length > 2000 ? message[..2000] : message,
                Response = result.Response?.Length > 2000 ? result.Response[..2000] : result.Response,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                CachedTokens = result.CachedTokens,
                LatencyMs = result.LatencyMs,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                EstimatedCostUsd = result.EstimatedCostUsd
            };

            await _interactionLogRepo.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log DM assistant interaction for user {UserId}", userId);
        }
    }

    private async Task UpdateDailyMetricsAsync(
        ulong userId, DmAssistantResponse result, CancellationToken ct)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var metrics = await _metricsRepo.GetByUserAndDateAsync(userId, today, ct);

            if (metrics == null)
            {
                metrics = new DmAssistantUsageMetrics
                {
                    UserId = userId,
                    Date = today
                };
            }

            metrics.TotalMessages++;
            metrics.TotalInputTokens += result.InputTokens;
            metrics.TotalOutputTokens += result.OutputTokens;
            metrics.TotalCachedTokens += result.CachedTokens;
            metrics.EstimatedCostUsd += result.EstimatedCostUsd;
            if (!result.Success) metrics.FailedRequests++;

            // Rolling average latency
            if (metrics.TotalMessages == 1)
            {
                metrics.AverageLatencyMs = result.LatencyMs;
            }
            else
            {
                metrics.AverageLatencyMs = (int)(
                    (metrics.AverageLatencyMs * (metrics.TotalMessages - 1) + result.LatencyMs)
                    / metrics.TotalMessages);
            }

            metrics.UpdatedAt = DateTime.UtcNow;

            if (metrics.Id == 0)
            {
                await _metricsRepo.AddAsync(metrics, ct);
            }
            else
            {
                await _metricsRepo.UpdateAsync(metrics, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update DM assistant daily metrics for user {UserId}", userId);
        }
    }
}
