using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing encrypted Discord OAuth tokens in the database.
/// Handles token storage, retrieval, validation, and expiration tracking.
/// </summary>
public interface IDiscordTokenService
{
    /// <summary>
    /// Stores or updates Discord OAuth tokens for a user.
    /// Tokens are encrypted before storage.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="discordUserId">The Discord user ID (snowflake).</param>
    /// <param name="accessToken">Plain-text access token (will be encrypted).</param>
    /// <param name="refreshToken">Plain-text refresh token (will be encrypted).</param>
    /// <param name="expiresAt">When the access token expires.</param>
    /// <param name="scopes">Space-separated OAuth scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreTokensAsync(
        string applicationUserId,
        ulong discordUserId,
        string accessToken,
        string refreshToken,
        DateTime expiresAt,
        string scopes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the decrypted access token for a user.
    /// Automatically refreshes the token if it's expired.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decrypted access token, or null if no token exists.</returns>
    Task<string?> GetAccessTokenAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a valid (non-expired) OAuth token.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a valid token exists, false otherwise.</returns>
    Task<bool> HasValidTokenAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all OAuth tokens for a user.
    /// Used when a user revokes access or signs out.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteTokensAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tokens that will expire within the specified timespan.
    /// Used by background services to proactively refresh tokens.
    /// </summary>
    /// <param name="expiresWithin">Time window to check for expiring tokens.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of expiring tokens.</returns>
    Task<IReadOnlyList<DiscordOAuthToken>> GetExpiringTokensAsync(
        TimeSpan expiresWithin,
        CancellationToken cancellationToken = default);
}
