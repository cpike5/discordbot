namespace DiscordBot.Core.Configuration;

/// <summary>
/// Per-user rate limits for the member portal's soundboard upload and play endpoints.
/// Each signed-in user gets their own fixed window; a request past the limit is answered 429.
/// </summary>
public class PortalRateLimitOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "PortalRateLimit";

    /// <summary>
    /// Sound uploads per user. Default: 10 per 60 seconds.
    /// </summary>
    public FixedWindowLimit Upload { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>
    /// Sound plays per user. Default: 30 per 60 seconds.
    /// </summary>
    public FixedWindowLimit Play { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

/// <summary>
/// A fixed-window limit: at most <see cref="PermitLimit"/> requests every <see cref="WindowSeconds"/>.
/// </summary>
public class FixedWindowLimit
{
    /// <summary>
    /// Requests allowed in one window.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Window length in seconds.
    /// </summary>
    public int WindowSeconds { get; set; }
}
