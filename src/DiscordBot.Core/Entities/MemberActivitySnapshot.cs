using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Aggregated activity snapshot for a member within a guild.
/// Can represent hourly or daily granularity.
/// </summary>
public class MemberActivitySnapshot
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
    /// Gets or sets the Discord user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the start of the snapshot period (UTC).
    /// For hourly: represents the start of the hour.
    /// For daily: represents the start of the day (00:00:00 UTC).
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the granularity of this snapshot (Hourly or Daily).
    /// </summary>
    public SnapshotGranularity Granularity { get; set; }

    /// <summary>
    /// Gets or sets the number of messages sent during this period.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of reactions added during this period.
    /// Currently set to 0 as MessageLog does not track reactions yet.
    /// </summary>
    public int ReactionCount { get; set; }

    /// <summary>
    /// Gets or sets the total minutes spent in voice channels during this period.
    /// Currently set to 0 as voice activity tracking is not implemented yet.
    /// </summary>
    public int VoiceMinutes { get; set; }

    /// <summary>
    /// Gets or sets the number of unique channels the member was active in during this period.
    /// </summary>
    public int UniqueChannelsActive { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this snapshot was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the navigation property to the guild entity.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the user entity.
    /// </summary>
    public User? User { get; set; }
}
