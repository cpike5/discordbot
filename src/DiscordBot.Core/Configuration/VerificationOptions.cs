namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for user verification code generation and validation.
/// </summary>
public class VerificationOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Verification";

    /// <summary>
    /// Gets or sets the character set used for generating verification codes.
    /// Excludes ambiguous characters like 0, O, 1, I to improve readability.
    /// Default is "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".
    /// </summary>
    public string CodeCharset { get; set; } = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Gets or sets the length of generated verification codes.
    /// Default is 6 characters.
    /// </summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>
    /// Gets or sets the expiry time (in minutes) for verification codes.
    /// After this time, codes become invalid and cannot be used.
    /// Default is 15 minutes.
    /// </summary>
    public int CodeExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum number of verification codes a user can request per hour.
    /// Used to prevent abuse and spam. Default is 3.
    /// </summary>
    public int MaxCodesPerHour { get; set; } = 3;

    /// <summary>
    /// Gets or sets the age threshold (in hours) for cleaning up old verification codes.
    /// Codes older than this threshold are deleted by the cleanup service.
    /// Default is 24 hours.
    /// </summary>
    public int OldCodeCleanupHours { get; set; } = 24;
}
