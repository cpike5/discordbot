namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for in-memory caching durations across the application.
/// </summary>
public class CachingOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Caching";

    /// <summary>
    /// Gets or sets the cache duration (in minutes) for guild membership data.
    /// Used to reduce API calls when checking user guild memberships.
    /// Default is 5 minutes.
    /// </summary>
    public int GuildMembershipDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cache duration (in minutes) for Discord user information.
    /// Used to reduce API calls when fetching user profiles and details.
    /// Default is 15 minutes.
    /// </summary>
    public int DiscordUserInfoDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the expiry time (in minutes) for interaction state data.
    /// Used for storing temporary state during multi-step Discord interactions.
    /// Default is 15 minutes.
    /// </summary>
    public int InteractionStateExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the cache duration (in minutes) for user consent data.
    /// Used to reduce database lookups when checking message logging consent.
    /// Default is 5 minutes.
    /// </summary>
    public int ConsentCacheDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cache duration (in seconds) for dashboard statistics.
    /// Used to reduce load when fetching aggregated dashboard data.
    /// Default is 5 seconds.
    /// </summary>
    public int DashboardStatsCacheDurationSeconds { get; set; } = 5;
}
