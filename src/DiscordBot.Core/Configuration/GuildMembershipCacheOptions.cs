namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for caching stored guild membership data from the database.
/// This is separate from in-memory API caching (CachingOptions.GuildMembershipDurationMinutes).
/// </summary>
public class GuildMembershipCacheOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "GuildMembershipCache";

    /// <summary>
    /// Gets or sets the cache duration (in minutes) for stored guild membership data.
    /// Used to reduce database queries when checking if users are members of guilds
    /// for authorization purposes (e.g., GuildAccessAuthorizationHandler).
    /// Default is 30 minutes.
    /// </summary>
    public int StoredGuildMembershipDurationMinutes { get; set; } = 30;
}
