namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a rate limit check for assistant usage.
/// </summary>
public class RateLimitCheckResult
{
    /// <summary>
    /// Gets or sets whether the user is allowed to make a request.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Gets or sets the number of questions remaining in the current window.
    /// </summary>
    public int RemainingQuestions { get; set; }

    /// <summary>
    /// Gets or sets the time until the rate limit resets (if rate limited).
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }

    /// <summary>
    /// Gets or sets the user-friendly message about the rate limit status.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Creates an allowed result.
    /// </summary>
    /// <param name="remainingQuestions">Number of questions remaining in the current window.</param>
    /// <returns>An allowed RateLimitCheckResult.</returns>
    public static RateLimitCheckResult Allowed(int remainingQuestions)
    {
        return new RateLimitCheckResult
        {
            IsAllowed = true,
            RemainingQuestions = remainingQuestions
        };
    }

    /// <summary>
    /// Creates a rate limited result.
    /// </summary>
    /// <param name="retryAfter">Time until the rate limit resets.</param>
    /// <param name="message">User-friendly message about the rate limit status.</param>
    /// <returns>A rate limited RateLimitCheckResult.</returns>
    public static RateLimitCheckResult RateLimited(TimeSpan retryAfter, string message)
    {
        return new RateLimitCheckResult
        {
            IsAllowed = false,
            RemainingQuestions = 0,
            RetryAfter = retryAfter,
            Message = message
        };
    }
}
