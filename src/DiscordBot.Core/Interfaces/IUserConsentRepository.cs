using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserConsent entities with consent-specific operations.
/// </summary>
public interface IUserConsentRepository : IRepository<UserConsent>
{
    /// <summary>
    /// Gets the active (non-revoked) consent record for a specific user and consent type.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="type">Type of consent to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active consent record if found, null otherwise.</returns>
    Task<UserConsent?> GetActiveConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all consent records (both active and revoked) for a specific user.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of all consent records for the user.</returns>
    Task<IEnumerable<UserConsent>> GetUserConsentsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user has active consent for a specific type.
    /// </summary>
    /// <param name="discordUserId">Discord user ID.</param>
    /// <param name="type">Type of consent to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user has active consent, false otherwise.</returns>
    Task<bool> HasActiveConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default);
}
