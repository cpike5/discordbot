namespace DiscordBot.Core.Interfaces;

using DiscordBot.Core.DTOs;

/// <summary>
/// Service for exporting user data (GDPR Article 15 - Right of Access).
/// </summary>
public interface IUserDataExportService
{
    /// <summary>
    /// Exports all user data to a ZIP file.
    /// </summary>
    /// <param name="discordUserId">The Discord user ID to export data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result with download URL and metadata.</returns>
    Task<UserDataExportResultDto> ExportUserDataAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an export archive that belongs to the given user. The path is built from the user id and
    /// the export id only, so it can never point outside that user's export directory.
    /// </summary>
    /// <param name="discordUserId">The Discord user ID that owns the export.</param>
    /// <param name="exportId">The export id from <see cref="UserDataExportResultDto.ExportId"/>.</param>
    /// <returns>The file path, or null when the user has no such export or it has expired.</returns>
    string? GetExportFilePath(ulong discordUserId, Guid exportId);

    /// <summary>
    /// Deletes every export archive that belongs to the given user (used when the user's data is purged).
    /// </summary>
    /// <param name="discordUserId">The Discord user ID whose exports are deleted.</param>
    /// <returns>Number of archives deleted.</returns>
    int DeleteUserExports(ulong discordUserId);

    /// <summary>
    /// Cleans up expired export files, and any files left in the old public location (<c>wwwroot/exports</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files cleaned up.</returns>
    Task<int> CleanupExpiredExportsAsync(CancellationToken cancellationToken = default);
}
