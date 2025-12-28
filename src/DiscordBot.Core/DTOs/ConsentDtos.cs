namespace DiscordBot.Core.DTOs;

/// <summary>
/// Represents the current status of a user consent for a specific consent type.
/// </summary>
public class ConsentStatusDto
{
    /// <summary>
    /// Type of consent (e.g., MessageLogging).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// User-friendly display name for the consent type.
    /// </summary>
    public string TypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description explaining what this consent allows.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the user has currently granted this consent.
    /// </summary>
    public bool IsGranted { get; set; }

    /// <summary>
    /// Timestamp when consent was granted (null if never granted or currently revoked).
    /// </summary>
    public DateTime? GrantedAt { get; set; }

    /// <summary>
    /// Timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string? GrantedAtUtcIso => GrantedAt.HasValue
        ? DateTime.SpecifyKind(GrantedAt.Value, DateTimeKind.Utc).ToString("o")
        : null;

    /// <summary>
    /// Source through which consent was granted (e.g., "SlashCommand", "WebUI").
    /// </summary>
    public string? GrantedVia { get; set; }
}

/// <summary>
/// Represents a historical consent change entry.
/// </summary>
public class ConsentHistoryEntryDto
{
    /// <summary>
    /// Type of consent (e.g., MessageLogging).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// User-friendly display name for the consent type.
    /// </summary>
    public string TypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Action performed ("Granted" or "Revoked").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string TimestampUtcIso => DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Source through which the action was performed (e.g., "SlashCommand", "WebUI").
    /// </summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Result of granting or revoking consent.
/// </summary>
public class ConsentUpdateResult
{
    public bool Succeeded { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ConsentUpdateResult Success() => new()
    {
        Succeeded = true
    };

    public static ConsentUpdateResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidConsentType = "INVALID_CONSENT_TYPE";
    public const string AlreadyGranted = "ALREADY_GRANTED";
    public const string NotGranted = "NOT_GRANTED";
    public const string DatabaseError = "DATABASE_ERROR";
}
