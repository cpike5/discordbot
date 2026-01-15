namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for the AI assistant feature.
/// Controls whether the assistant is enabled, channel restrictions, and rate limit overrides.
/// </summary>
public class AssistantGuildSettings
{
    /// <summary>
    /// Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether the assistant feature is enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// List of channel IDs where assistant is allowed to respond.
    /// Empty list means all channels are allowed.
    /// Stored as JSON array.
    /// </summary>
    public string AllowedChannelIds { get; set; } = "[]";

    /// <summary>
    /// Guild-specific rate limit override (questions per RateLimitWindowMinutes).
    /// Null means use global default from AssistantOptions.DefaultRateLimit.
    /// </summary>
    public int? RateLimitOverride { get; set; }

    /// <summary>
    /// Timestamp when these settings were created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when these settings were last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild these settings belong to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Helper to deserialize AllowedChannelIds from JSON.
    /// </summary>
    public List<ulong> GetAllowedChannelIdsList()
    {
        if (string.IsNullOrWhiteSpace(AllowedChannelIds) || AllowedChannelIds == "[]")
            return new List<ulong>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ulong>>(AllowedChannelIds)
                ?? new List<ulong>();
        }
        catch
        {
            return new List<ulong>();
        }
    }

    /// <summary>
    /// Helper to serialize AllowedChannelIds to JSON.
    /// </summary>
    public void SetAllowedChannelIdsList(List<ulong> channelIds)
    {
        AllowedChannelIds = System.Text.Json.JsonSerializer.Serialize(channelIds ?? new List<ulong>());
    }
}
