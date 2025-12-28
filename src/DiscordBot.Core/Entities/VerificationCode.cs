namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a verification code used to link a Discord account via the bot.
/// </summary>
public class VerificationCode
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the ApplicationUser initiating verification.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// The 6-character verification code (e.g., "ABC-123" stored as "ABC123").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Discord user ID who ran /verify-account. Null until command is executed.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Current status of the verification.
    /// </summary>
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

    /// <summary>
    /// When the verification was initiated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the code expires (CreatedAt + 15 minutes).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// ExpiresAt in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string ExpiresAtUtcIso => ExpiresAt.ToString("o");

    /// <summary>
    /// When the verification was completed (if successful).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IP address that initiated the verification request.
    /// </summary>
    public string? IpAddress { get; set; }
}

/// <summary>
/// Status of a verification code.
/// </summary>
public enum VerificationStatus
{
    /// <summary>Verification initiated, awaiting code entry.</summary>
    Pending = 0,

    /// <summary>Code validated, accounts linked successfully.</summary>
    Completed = 1,

    /// <summary>Code expired without being used.</summary>
    Expired = 2,

    /// <summary>Verification cancelled by user.</summary>
    Cancelled = 3
}
