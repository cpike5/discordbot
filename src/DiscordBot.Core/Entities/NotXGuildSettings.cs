using System.Text.Json;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for the not-X feature (X/Twitter link preview).
/// </summary>
public class NotXGuildSettings
{
    /// <summary>
    /// Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Feature kill-switch. Defaults to disabled.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// If set, tweet previews are posted to this channel instead of the originating channel.
    /// Null means reply in the originating channel.
    /// </summary>
    public ulong? OutputChannelId { get; set; }

    /// <summary>
    /// JSON-serialised ulong[] — only monitor these channels.
    /// Null or empty means monitor all channels.
    /// </summary>
    public string? MonitoredChannelIdsJson { get; set; }

    /// <summary>
    /// When true, only post previews when the tweet is flagged sensitive.
    /// When false, post previews for ALL tweet links regardless of sensitivity.
    /// Defaults to true (the primary use-case).
    /// </summary>
    public bool SensitiveOnly { get; set; } = true;

    /// <summary>
    /// When true, suppress the sensitive content label on the embed.
    /// Useful for guilds that are already NSFW-designated.
    /// </summary>
    public bool HideSensitiveLabel { get; set; } = false;

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
    /// Gets the list of monitored channel IDs from the JSON backing field.
    /// Returns an empty list when null or empty (meaning all channels are monitored).
    /// </summary>
    public List<ulong> GetMonitoredChannelIds()
    {
        try
        {
            if (string.IsNullOrEmpty(MonitoredChannelIdsJson))
                return new List<ulong>();

            return JsonSerializer.Deserialize<List<ulong>>(MonitoredChannelIdsJson) ?? new List<ulong>();
        }
        catch
        {
            return new List<ulong>();
        }
    }

    /// <summary>
    /// Sets the list of monitored channel IDs, serialising to JSON for storage.
    /// </summary>
    public void SetMonitoredChannelIds(IEnumerable<ulong> ids)
    {
        MonitoredChannelIdsJson = JsonSerializer.Serialize(ids.ToArray());
    }
}
