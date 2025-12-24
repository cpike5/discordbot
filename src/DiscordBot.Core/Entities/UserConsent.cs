using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user's consent for specific data processing activities.
/// Supports GDPR and privacy compliance by tracking opt-in/opt-out status.
/// </summary>
public class UserConsent
{
    /// <summary>
    /// Unique identifier for this consent record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord user ID who granted/revoked consent.
    /// </summary>
    public ulong DiscordUserId { get; set; }

    /// <summary>
    /// Type of consent being tracked (e.g., MessageLogging, Analytics).
    /// </summary>
    public ConsentType ConsentType { get; set; }

    /// <summary>
    /// Timestamp when consent was originally granted.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// Timestamp when consent was revoked. Null if consent is still active.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Indicates whether this consent is currently active (not revoked).
    /// </summary>
    public bool IsActive => RevokedAt == null;

    /// <summary>
    /// Source/method through which consent was granted (e.g., "SlashCommand", "WebUI").
    /// </summary>
    public string? GrantedVia { get; set; }

    /// <summary>
    /// Source/method through which consent was revoked (e.g., "SlashCommand", "WebUI").
    /// </summary>
    public string? RevokedVia { get; set; }

    /// <summary>
    /// Navigation property for the Discord user.
    /// </summary>
    public User? User { get; set; }
}
