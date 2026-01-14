using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a bulk purge operation.
/// </summary>
public class BulkPurgeResultDto
{
    /// <summary>
    /// Whether the purge was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if purge failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if purge failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// The entity type that was purged.
    /// </summary>
    public BulkPurgeEntityType EntityType { get; set; }

    /// <summary>
    /// Number of records deleted.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Timestamp when the purge was executed.
    /// </summary>
    public DateTime PurgedAt { get; set; }

    /// <summary>
    /// Correlation ID for the audit log entry.
    /// </summary>
    public string AuditLogCorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static BulkPurgeResultDto Succeeded(
        BulkPurgeEntityType entityType,
        int deletedCount,
        string correlationId)
    {
        return new BulkPurgeResultDto
        {
            Success = true,
            EntityType = entityType,
            DeletedCount = deletedCount,
            PurgedAt = DateTime.UtcNow,
            AuditLogCorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static BulkPurgeResultDto Failed(string errorCode, string errorMessage)
    {
        return new BulkPurgeResultDto
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            PurgedAt = DateTime.UtcNow
        };
    }

    // Error codes
    public const string NoRecordsFound = "NO_RECORDS_FOUND";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string TransactionFailed = "TRANSACTION_FAILED";
    public const string InvalidCriteria = "INVALID_CRITERIA";
}
