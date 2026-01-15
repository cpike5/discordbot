namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of an assistant question processing operation.
/// </summary>
public class AssistantResponseResult
{
    /// <summary>
    /// Gets or sets whether the question was processed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the assistant's response text.
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Gets or sets the error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the number of input tokens consumed (non-cached).
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of output tokens consumed.
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens served from cache.
    /// </summary>
    public int CachedTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens written to cache (on cache miss).
    /// </summary>
    public int CacheCreationTokens { get; set; }

    /// <summary>
    /// Gets or sets whether the prompt cache was hit.
    /// </summary>
    public bool CacheHit { get; set; }

    /// <summary>
    /// Gets or sets the number of tool calls executed.
    /// </summary>
    public int ToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the total response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost in USD for this interaction.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// Creates a successful response result.
    /// </summary>
    /// <param name="response">The assistant's response text.</param>
    /// <returns>A successful AssistantResponseResult.</returns>
    public static AssistantResponseResult SuccessResult(string response)
    {
        return new AssistantResponseResult
        {
            Success = true,
            Response = response
        };
    }

    /// <summary>
    /// Creates a failed response result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed AssistantResponseResult.</returns>
    public static AssistantResponseResult ErrorResult(string errorMessage)
    {
        return new AssistantResponseResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
