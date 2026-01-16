using Anthropic;
using Anthropic.Models.Messages;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.Anthropic;

/// <summary>
/// Anthropic Claude API client implementation of ILlmClient.
/// Uses the official Anthropic package and handles API calls, retry logic, and message mapping.
/// </summary>
public class AnthropicLlmClient : ILlmClient
{
    private readonly AnthropicClient _client;
    private readonly IOptions<AnthropicOptions> _options;
    private readonly ILogger<AnthropicLlmClient> _logger;

    public AnthropicLlmClient(
        AnthropicClient client,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicLlmClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public bool SupportsToolUse => true;

    /// <inheritdoc />
    public bool SupportsPromptCaching => true;

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        // Validate API key
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            _logger.LogError("Anthropic API key is not configured");
            return new LlmResponse
            {
                Success = false,
                StopReason = LlmStopReason.Error,
                ErrorMessage = "Anthropic API key is not configured"
            };
        }

        // Build the message request parameters
        var messageParams = BuildMessageParams(request);

        // Execute with retry logic
        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                // Create timeout cancellation token source
                using var timeoutCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(options.TimeoutSeconds));

                // Combine timeout with caller's cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                _logger.LogDebug(
                    "Sending Anthropic completion request (attempt {Attempt}/{MaxAttempts})",
                    attempt + 1,
                    options.MaxRetries + 1);

                // Call Anthropic API using the official SDK
                var response = await _client.Messages.Create(
                    messageParams,
                    cancellationToken: linkedCts.Token);

                _logger.LogInformation(
                    "Anthropic completion successful. Tokens: {InputTokens} in, {OutputTokens} out, {CachedTokens} cached",
                    response.Usage.InputTokens,
                    response.Usage.OutputTokens,
                    response.Usage.CacheReadInputTokens ?? 0);

                // Map response to LlmResponse
                return AnthropicMessageMapper.ToLlmResponse(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancelled the operation - don't retry
                _logger.LogWarning("Anthropic completion request was cancelled");
                return new LlmResponse
                {
                    Success = false,
                    StopReason = LlmStopReason.Error,
                    ErrorMessage = "Request was cancelled"
                };
            }
            catch (OperationCanceledException)
            {
                // Timeout occurred
                _logger.LogWarning(
                    "Anthropic completion request timed out after {TimeoutSeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                    options.TimeoutSeconds,
                    attempt + 1,
                    options.MaxRetries + 1);

                if (attempt >= options.MaxRetries)
                {
                    return new LlmResponse
                    {
                        Success = false,
                        StopReason = LlmStopReason.Error,
                        ErrorMessage = $"Request timed out after {options.TimeoutSeconds} seconds"
                    };
                }

                await DelayForRetry(attempt, options.RetryBaseDelayMs, cancellationToken);
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                // Transient error - retry with exponential backoff
                _logger.LogWarning(
                    ex,
                    "Transient error calling Anthropic API (attempt {Attempt}/{MaxAttempts}): {ErrorMessage}",
                    attempt + 1,
                    options.MaxRetries + 1,
                    ex.Message);

                if (attempt >= options.MaxRetries)
                {
                    return new LlmResponse
                    {
                        Success = false,
                        StopReason = LlmStopReason.Error,
                        ErrorMessage = $"Request failed after {options.MaxRetries + 1} attempts: {ex.Message}"
                    };
                }

                await DelayForRetry(attempt, options.RetryBaseDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                // Permanent error - don't retry
                _logger.LogError(
                    ex,
                    "Permanent error calling Anthropic API: {ErrorMessage}",
                    ex.Message);

                return new LlmResponse
                {
                    Success = false,
                    StopReason = LlmStopReason.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        // Should never reach here, but satisfy compiler
        return new LlmResponse
        {
            Success = false,
            StopReason = LlmStopReason.Error,
            ErrorMessage = "Unknown error occurred"
        };
    }

    /// <summary>
    /// Builds MessageCreateParams from LlmRequest.
    /// </summary>
    private MessageCreateParams BuildMessageParams(LlmRequest request)
    {
        var options = _options.Value;

        // Determine the model to use - Model class has static properties for known models
        var modelName = !string.IsNullOrEmpty(request.Model)
            ? request.Model
            : options.DefaultModel;

        // Build base params with object initializer (all init-only properties set here)
        var messageParams = new MessageCreateParams
        {
            Model = modelName,
            MaxTokens = request.MaxTokens,
            Messages = AnthropicMessageMapper.ToAnthropicMessages(request),
            // Set system prompt if provided
            System = !string.IsNullOrEmpty(request.SystemPrompt)
                ? (request.EnablePromptCaching && options.EnablePromptCachingByDefault
                    ? AnthropicMessageMapper.CreateCachedSystemMessage(request.SystemPrompt)
                    : AnthropicMessageMapper.CreateSystemMessage(request.SystemPrompt))
                : null,
            // Set tools if provided
            Tools = request.Tools?.Any() == true
                ? AnthropicMessageMapper.ToAnthropicTools(request.Tools)
                : null
        };

        return messageParams;
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        // Check exception type and message for known transient errors
        var message = ex.Message.ToLowerInvariant();

        // Rate limits
        if (message.Contains("rate limit") || message.Contains("429"))
        {
            return true;
        }

        // Timeouts
        if (message.Contains("timeout") || message.Contains("timed out"))
        {
            return true;
        }

        // Server errors (5xx)
        if (message.Contains("500") || message.Contains("502") ||
            message.Contains("503") || message.Contains("504"))
        {
            return true;
        }

        // Network errors
        if (ex is HttpRequestException or TaskCanceledException)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Delays for retry with exponential backoff.
    /// </summary>
    private async Task DelayForRetry(
        int attempt,
        int baseDelayMs,
        CancellationToken cancellationToken)
    {
        // Exponential backoff: baseDelay * (2 ^ attempt)
        var delayMs = baseDelayMs * (int)Math.Pow(2, attempt);

        _logger.LogDebug(
            "Retrying Anthropic API call in {DelayMs}ms (attempt {Attempt})",
            delayMs,
            attempt + 1);

        await Task.Delay(delayMs, cancellationToken);
    }
}
