namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a user data export operation.
/// </summary>
public class UserDataExportResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? ExportId { get; set; }
    public Dictionary<string, int> ExportedCounts { get; set; } = new();
    public DateTime ExportedAt { get; set; }

    public static UserDataExportResultDto Succeeded(
        string downloadUrl,
        DateTime expiresAt,
        Guid exportId,
        Dictionary<string, int> exportedCounts)
    {
        return new UserDataExportResultDto
        {
            Success = true,
            DownloadUrl = downloadUrl,
            ExpiresAt = expiresAt,
            ExportId = exportId,
            ExportedCounts = exportedCounts,
            ExportedAt = DateTime.UtcNow
        };
    }

    public static UserDataExportResultDto Failed(string errorCode, string errorMessage)
    {
        return new UserDataExportResultDto
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ExportedAt = DateTime.UtcNow
        };
    }

    // Error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string FileSystemError = "FILE_SYSTEM_ERROR";
    public const string ExportFailed = "EXPORT_FAILED";
}
