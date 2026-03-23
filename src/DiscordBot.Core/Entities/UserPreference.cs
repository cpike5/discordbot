namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user preference stored per-guild.
/// Enables server-synced user preferences with localStorage cache on the client.
/// </summary>
public class UserPreference
{
    /// <summary>
    /// Unique identifier for this preference record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord guild ID where this preference applies.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user ID who owns the preference.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Preference key (e.g., "tts_selected_voice", "tts_mode_preference").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Preference value as a string (JSON-encoded for complex values).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this preference was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
