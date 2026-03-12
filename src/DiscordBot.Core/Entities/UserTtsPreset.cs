namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user's saved custom TTS voice preset.
/// Allows users to save and quickly recall voice configurations in the portal.
/// Presets are per-user globally (not guild-scoped).
/// </summary>
public class UserTtsPreset
{
    /// <summary>
    /// Unique identifier for this preset record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord user ID who owns this preset.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// User-defined display name for the preset (max 50 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Azure TTS voice name (e.g., "en-US-JennyNeural"). Max 100 characters.
    /// </summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>
    /// Optional speaking style (e.g., "cheerful", "angry"). Max 50 characters.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Speech rate multiplier (0.5 to 2.0).
    /// </summary>
    public decimal Speed { get; set; } = 1.0m;

    /// <summary>
    /// Pitch adjustment multiplier (0.5 to 2.0).
    /// </summary>
    public decimal Pitch { get; set; } = 1.0m;

    /// <summary>
    /// Optional icon identifier for visual display (max 50 characters).
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Timestamp when the preset was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the preset was last updated (UTC). Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
