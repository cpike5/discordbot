using DiscordBot.Core.Entities;

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
    /// for authorization purposes (e.g., GuildAccessHandler's cache-first guild access check).
    /// Default is 30 minutes.
    /// </summary>
    public int StoredGuildMembershipDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum age of a stored <see cref="UserDiscordGuild"/> row's
    /// <c>LastUpdatedAt</c> timestamp before the guild-access authorization handler treats it
    /// as a cache miss and falls back to a live Discord gateway lookup, refreshing the row.
    /// This bounds how long a user kicked or banned from a guild, or stripped of Discord
    /// Administrator, can keep access on a stale row. It is independent of
    /// <see cref="StoredGuildMembershipDurationMinutes"/>, which only bounds the short-lived
    /// in-memory cache placed over the same database rows (30 minutes, versus this default of
    /// 1 hour on the row's own timestamp). Default is 1 hour.
    /// </summary>
    public TimeSpan MembershipMaxAge { get; set; } = TimeSpan.FromHours(1);
}
