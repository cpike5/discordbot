using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing user consent operations.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Gets the current consent status for all consent types for a user.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of consent status for all consent types.</returns>
    Task<IEnumerable<ConsentStatusDto>> GetConsentStatusAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the consent history for a user, ordered by most recent first.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of consent history entries.</returns>
    Task<IEnumerable<ConsentHistoryEntryDto>> GetConsentHistoryAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants consent for a specific consent type via Web UI.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="type">Type of consent to grant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<ConsentUpdateResult> GrantConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes consent for a specific consent type via Web UI.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="type">Type of consent to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<ConsentUpdateResult> RevokeConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user has active consent for a specific type.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="consentType">Type of consent to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user has active consent, false otherwise.</returns>
    Task<bool> HasConsentAsync(
        ulong discordUserId,
        ConsentType consentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active consent types for a user.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of active consent types.</returns>
    Task<IEnumerable<ConsentType>> GetActiveConsentsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch check consent for multiple users (for efficiency).
    /// </summary>
    /// <param name="discordUserIds">Collection of Discord user IDs to check.</param>
    /// <param name="consentType">Type of consent to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping user IDs to their consent status.</returns>
    Task<IDictionary<ulong, bool>> HasConsentBatchAsync(
        IEnumerable<ulong> discordUserIds,
        ConsentType consentType,
        CancellationToken cancellationToken = default);
}
