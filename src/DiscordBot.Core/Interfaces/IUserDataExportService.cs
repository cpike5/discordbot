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
    /// Cleans up expired export files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files cleaned up.</returns>
    Task<int> CleanupExpiredExportsAsync(CancellationToken cancellationToken = default);
}
