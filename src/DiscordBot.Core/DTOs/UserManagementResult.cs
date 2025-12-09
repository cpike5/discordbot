namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a user management operation.
/// </summary>
public class UserManagementResult
{
    public bool Succeeded { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public UserDto? User { get; private set; }
    public string? GeneratedPassword { get; private set; }

    public static UserManagementResult Success(UserDto? user = null) => new()
    {
        Succeeded = true,
        User = user
    };

    public static UserManagementResult SuccessWithPassword(string password, UserDto user) => new()
    {
        Succeeded = true,
        User = user,
        GeneratedPassword = password
    };

    public static UserManagementResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Common error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string SelfModificationDenied = "SELF_MODIFICATION_DENIED";
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
    public const string InvalidRole = "INVALID_ROLE";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string PasswordValidationFailed = "PASSWORD_VALIDATION_FAILED";
    public const string DiscordNotLinked = "DISCORD_NOT_LINKED";
}
