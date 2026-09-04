namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Rate limiting configuration for the guild AI assistant.
/// Binds under "Assistant:RateLimits" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantRateLimitOptions
{
    /// <summary>
    /// Gets or sets the default maximum number of questions a user can ask within the rate limit window.
    /// Guilds can override this value.
    /// Default is 5 questions per 5 minutes.
    /// </summary>
    public int DefaultRateLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window for rate limiting in minutes.
    /// Default is 5 minutes.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum role required to bypass rate limits.
    /// Roles at or above this level are not rate limited.
    /// Default is "Admin" (Admin and SuperAdmin bypass).
    /// </summary>
    /// <remarks>
    /// Valid values: "SuperAdmin", "Admin", "Moderator", "Viewer", or null (no bypass).
    /// </remarks>
    public string? RateLimitBypassRole { get; set; } = "Admin";
}
