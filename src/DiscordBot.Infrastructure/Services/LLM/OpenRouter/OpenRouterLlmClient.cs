using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// Raised for a failed OpenRouter call, carrying the API's own message and the HTTP status (null
/// when the failure wasn't an HTTP status — e.g. an error object returned on a 200).
/// </summary>
public sealed class OpenRouterException : Exception
{
    public OpenRouterException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status this failure came back with, when it was an HTTP failure.</summary>
    public int? StatusCode { get; }
}

/// <summary>
/// OpenRouter implementation of <see cref="ILlmClient"/>, speaking the OpenAI-compatible
/// chat-completions API through an owned typed <see cref="HttpClient"/> — no vendor SDK.
/// Handles API calls, retry with exponential backoff, and message mapping.
/// </summary>
public class OpenRouterLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly IOptions<OpenRouterOptions> _options;
    private readonly ILogger<OpenRouterLlmClient> _logger;

    public OpenRouterLlmClient(
        HttpClient http,
        IOptions<OpenRouterOptions> options,
        ILogger<OpenRouterLlmClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderName => "OpenRouter";

    /// <inheritdoc />
    public bool SupportsToolUse => true;

    /// <inheritdoc />
    /// <remarks>
    /// Cache breakpoints are passed through to Claude-family models and ignored by other providers,
    /// which report zero cached tokens rather than failing.
    /// </remarks>
    public bool SupportsPromptCaching => true;

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            _logger.LogError("OpenRouter API key is not configured");
            return Failure("OpenRouter API key is not configured");
        }

        var wireRequest = BuildRequest(request);

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(options.TimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                _logger.LogDebug(
                    "Sending OpenRouter completion request for model {Model} (attempt {Attempt}/{MaxAttempts})",
                    wireRequest.Model,
                    attempt + 1,
                    options.MaxRetries + 1);

                var response = await SendAsync(wireRequest, options.ApiKey, linkedCts.Token);

                _logger.LogInformation(
                    "OpenRouter completion successful. Tokens: {InputTokens} in, {OutputTokens} out, {CachedTokens} cached, cost {Cost}",
                    response.Usage?.PromptTokens ?? 0,
                    response.Usage?.CompletionTokens ?? 0,
                    response.Usage?.CacheReadTokens ?? 0,
                    response.Usage?.Cost);

                return OpenRouterMessageMapper.ToLlmResponse(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancelled the operation - don't retry
                _logger.LogWarning("OpenRouter completion request was cancelled");
                return Failure("Request was cancelled");
            }
            catch (OperationCanceledException)
            {
                // Per-attempt timeout elapsed
                _logger.LogWarning(
                    "OpenRouter completion request timed out after {TimeoutSeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                    options.TimeoutSeconds,
                    attempt + 1,
                    options.MaxRetries + 1);

                if (attempt >= options.MaxRetries)
                {
                    return Failure($"Request timed out after {options.TimeoutSeconds} seconds");
                }

                await DelayForRetry(attempt, options.RetryBaseDelayMs, cancellationToken);
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Transient error calling OpenRouter (attempt {Attempt}/{MaxAttempts}): {ErrorMessage}",
                    attempt + 1,
                    options.MaxRetries + 1,
                    ex.Message);

                if (attempt >= options.MaxRetries)
                {
                    return Failure(
                        $"Request failed after {options.MaxRetries + 1} attempts: {ex.Message}");
                }

                await DelayForRetry(attempt, options.RetryBaseDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                // Permanent error - don't retry
                _logger.LogError(ex, "Permanent error calling OpenRouter: {ErrorMessage}", ex.Message);
                return Failure(ex.Message);
            }
        }

        // Unreachable: the loop either returns or throws on its final attempt.
        return Failure("Unknown error occurred");
    }

    /// <summary>
    /// Sends one chat completion. Throws <see cref="OpenRouterException"/> for a non-success status,
    /// an error object in the body, or a reply with no usable choice — so a returned response always
    /// carries a message.
    /// </summary>
    private async Task<ChatCompletionResponse> SendAsync(
        ChatCompletionRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request, options: OpenRouterJson.Options),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenRouter returned {Status} for model {Model}: {Body}",
                (int)response.StatusCode, request.Model, Truncate(body, 1000));
            throw new OpenRouterException(
                DescribeError(response.StatusCode, body), (int)response.StatusCode);
        }

        ChatCompletionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options);
        }
        catch (JsonException ex)
        {
            throw new OpenRouterException(
                $"OpenRouter returned a response this client could not parse: {Truncate(body, 500)}",
                inner: ex);
        }

        // OpenRouter surfaces some provider failures as an error object rather than an HTTP status.
        if (parsed?.Error is { } error)
        {
            _logger.LogWarning(
                "OpenRouter reported an error in the response body for model {Model}: {Message} (code {Code})",
                request.Model, error.Message, error.Code);
            throw new OpenRouterException(
                error.Message ?? "OpenRouter reported an error in the response body.", error.Code);
        }

        if (parsed?.Message is null)
        {
            throw new OpenRouterException(
                $"OpenRouter returned no choices for model {request.Model}: {Truncate(body, 500)}");
        }

        return parsed;
    }

    /// <summary>
    /// Builds the wire request from an <see cref="LlmRequest"/>.
    /// </summary>
    private ChatCompletionRequest BuildRequest(LlmRequest request)
    {
        var options = _options.Value;

        var model = !string.IsNullOrEmpty(request.Model)
            ? request.Model
            : options.DefaultModel;

        var cachingEnabled = request.EnablePromptCaching && options.EnablePromptCachingByDefault;

        var tools = request.Tools?.Any() == true
            ? OpenRouterMessageMapper.ToOpenRouterTools(request.Tools)
            : null;

        return new ChatCompletionRequest
        {
            Model = model,
            Messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, cachingEnabled),
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature,
            Tools = tools,
            // Only route to a provider that supports the parameters we send. Without this a
            // provider with no native function calling can be picked, and the model then fakes a
            // tool call in plain text — which reaches the user as a raw tool-call-shaped string.
            Provider = tools is { Count: > 0 }
                ? new ProviderPreferences { RequireParameters = true }
                : null,
        };
    }

    /// <summary>
    /// Determines whether an exception represents a transient failure worth retrying. Unlike the
    /// Anthropic client this replaced, this reads real HTTP status codes rather than matching on
    /// exception message text.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        if (ex is OpenRouterException { StatusCode: { } status })
        {
            // 408 Request Timeout, 429 Too Many Requests, and any 5xx.
            return status is 408 or 429 || status >= 500;
        }

        // Network-level failures: connection resets, DNS, socket errors.
        return ex is HttpRequestException;
    }

    /// <summary>
    /// Turns an error response into a message worth showing an operator, preferring OpenRouter's own
    /// error text over the raw body and adding a hint for the statuses with a specific cause.
    /// </summary>
    private static string DescribeError(HttpStatusCode status, string body)
    {
        var detail = body;
        try
        {
            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options);
            if (!string.IsNullOrWhiteSpace(parsed?.Error?.Message))
            {
                detail = parsed!.Error!.Message!;
            }
        }
        catch (JsonException)
        {
            // Fall back to the raw body.
        }

        var hint = (int)status switch
        {
            401 => " Check that the OpenRouter API key is valid.",
            402 => " The OpenRouter account is out of credits for this model.",
            404 => " The model slug may be wrong, or account model restrictions exclude it.",
            429 => " Rate limited by OpenRouter - wait a moment and retry.",
            _ => string.Empty,
        };

        return $"OpenRouter error {(int)status}: {Truncate(detail, 500)}{hint}";
    }

    /// <summary>
    /// Delays for retry with exponential backoff.
    /// </summary>
    private async Task DelayForRetry(
        int attempt,
        int baseDelayMs,
        CancellationToken cancellationToken)
    {
        var delayMs = baseDelayMs * (int)Math.Pow(2, attempt);

        _logger.LogDebug(
            "Retrying OpenRouter API call in {DelayMs}ms (attempt {Attempt})",
            delayMs,
            attempt + 1);

        await Task.Delay(delayMs, cancellationToken);
    }

    private static LlmResponse Failure(string message) => new()
    {
        Success = false,
        StopReason = LlmStopReason.Error,
        ErrorMessage = message,
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
