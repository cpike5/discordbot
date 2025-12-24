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
}
