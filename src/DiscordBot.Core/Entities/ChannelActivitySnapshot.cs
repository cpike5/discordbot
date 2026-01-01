using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Aggregated activity snapshot for a channel within a guild.
/// Tracks message volume, unique participants, and peak activity times.
/// </summary>
public class ChannelActivitySnapshot
{
    /// <summary>
    /// Gets or sets the unique identifier for this snapshot.
    /// Uses long (Int64) to support high-volume logging scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the channel name at the time of snapshot (for historical reference).
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start of the snapshot period (UTC).
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the granularity of this snapshot (Hourly or Daily).
    /// </summary>
    public SnapshotGranularity Granularity { get; set; }

    /// <summary>
    /// Gets or sets the total number of messages in this channel during the period.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of unique users who sent messages in this channel.
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Gets or sets the hour of day (0-23) when activity was highest.
    /// Null for hourly snapshots, populated for daily snapshots.
    /// </summary>
    public int? PeakHour { get; set; }

    /// <summary>
    /// Gets or sets the message count during the peak hour.
    /// Null for hourly snapshots.
    /// </summary>
    public int? PeakHourMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the average message length in characters for messages in this channel.
    /// </summary>
    public double AverageMessageLength { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this snapshot was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the navigation property to the guild entity.
    /// </summary>
    public Guild? Guild { get; set; }
}
