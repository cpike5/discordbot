namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a user data purge operation.
/// </summary>
public class UserPurgeResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, int> DeletedCounts { get; set; } = new();
    public DateTime PurgedAt { get; set; }
    public string AuditLogCorrelationId { get; set; } = string.Empty;

    public static UserPurgeResultDto Succeeded(Dictionary<string, int> deletedCounts, string correlationId)
    {
        return new UserPurgeResultDto
        {
            Success = true,
            DeletedCounts = deletedCounts,
            PurgedAt = DateTime.UtcNow,
            AuditLogCorrelationId = correlationId
        };
    }

    public static UserPurgeResultDto Failed(string errorCode, string errorMessage)
    {
        return new UserPurgeResultDto
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            PurgedAt = DateTime.UtcNow
        };
    }

    // Error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserHasAdminRole = "USER_HAS_ADMIN_ROLE";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string TransactionFailed = "TRANSACTION_FAILED";
}
