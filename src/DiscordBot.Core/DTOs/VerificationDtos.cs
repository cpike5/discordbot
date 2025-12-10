namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of initiating a verification request.
/// </summary>
public class VerificationInitiationResult
{
    public bool Succeeded { get; private set; }
    public Guid? VerificationId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static VerificationInitiationResult Success(Guid verificationId) => new()
    {
        Succeeded = true,
        VerificationId = verificationId
    };

    public static VerificationInitiationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Error codes
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string PendingVerificationExists = "PENDING_EXISTS";
    public const string UserNotFound = "USER_NOT_FOUND";
}

/// <summary>
/// Result of generating a verification code.
/// </summary>
public class CodeGenerationResult
{
    public bool Succeeded { get; private set; }
    public string? Code { get; private set; }
    public string? FormattedCode { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CodeGenerationResult Success(string code, DateTime expiresAt) => new()
    {
        Succeeded = true,
        Code = code,
        FormattedCode = FormatCode(code),
        ExpiresAt = expiresAt
    };

    public static CodeGenerationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static string FormatCode(string code)
    {
        // Format as "ABC-123" for display
        if (code.Length == 6)
            return $"{code.Substring(0, 3)}-{code.Substring(3, 3)}";
        return code;
    }

    // Error codes
    public const string RateLimited = "RATE_LIMITED";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string NoPendingVerification = "NO_PENDING_VERIFICATION";
}

/// <summary>
/// Result of validating a verification code.
/// </summary>
public class CodeValidationResult
{
    public bool Succeeded { get; private set; }
    public ulong? LinkedDiscordUserId { get; private set; }
    public string? LinkedDiscordUsername { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CodeValidationResult Success(ulong discordUserId, string? discordUsername = null) => new()
    {
        Succeeded = true,
        LinkedDiscordUserId = discordUserId,
        LinkedDiscordUsername = discordUsername
    };

    public static CodeValidationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Error codes
    public const string InvalidCode = "INVALID_CODE";
    public const string CodeExpired = "CODE_EXPIRED";
    public const string CodeAlreadyUsed = "CODE_ALREADY_USED";
    public const string UserMismatch = "USER_MISMATCH";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string DiscordAlreadyLinked = "DISCORD_ALREADY_LINKED";
}
