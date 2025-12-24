using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing encrypted Discord OAuth tokens in the database.
/// Handles token storage, retrieval, validation, and expiration tracking.
/// </summary>
public class DiscordTokenService : IDiscordTokenService
{
    private readonly BotDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiscordOAuthOptions _oauthOptions;
    private readonly BackgroundServicesOptions _bgOptions;
    private readonly ILogger<DiscordTokenService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordTokenService"/> class.
    /// </summary>
    /// <param name="context">The database context for token storage.</param>
    /// <param name="dataProtectionProvider">Provider for creating data protectors for encryption.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients for Discord API calls.</param>
    /// <param name="oauthOptions">Discord OAuth configuration options.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DiscordTokenService(
        BotDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordOAuthOptions> oauthOptions,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<DiscordTokenService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("DiscordOAuth.Tokens.v1");
        _httpClientFactory = httpClientFactory;
        _oauthOptions = oauthOptions.Value;
        _bgOptions = bgOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StoreTokensAsync(
        string applicationUserId,
        ulong discordUserId,
        string accessToken,
        string refreshToken,
        DateTime expiresAt,
        string scopes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing Discord OAuth tokens for user {UserId}, expires at {ExpiresAt}",
            applicationUserId, expiresAt);

        try
        {
            // Encrypt tokens before storage
            var encryptedAccessToken = _protector.Protect(accessToken);
            var encryptedRefreshToken = _protector.Protect(refreshToken);

            // Check if token record already exists
            var existingToken = await _context.DiscordOAuthTokens
                .FirstOrDefaultAsync(t => t.ApplicationUserId == applicationUserId, cancellationToken);

            if (existingToken != null)
            {
                // Update existing token
                existingToken.EncryptedAccessToken = encryptedAccessToken;
                existingToken.EncryptedRefreshToken = encryptedRefreshToken;
                existingToken.AccessTokenExpiresAt = expiresAt;
                existingToken.Scopes = scopes;
                existingToken.DiscordUserId = discordUserId;
                existingToken.LastRefreshedAt = DateTime.UtcNow;

                _logger.LogInformation("Updated Discord OAuth tokens for user {UserId}, Discord user {DiscordUserId}",
                    applicationUserId, discordUserId);
            }
            else
            {
                // Create new token record
                var newToken = new DiscordOAuthToken
                {
                    Id = Guid.NewGuid(),
                    ApplicationUserId = applicationUserId,
                    DiscordUserId = discordUserId,
                    EncryptedAccessToken = encryptedAccessToken,
                    EncryptedRefreshToken = encryptedRefreshToken,
                    AccessTokenExpiresAt = expiresAt,
                    Scopes = scopes,
                    CreatedAt = DateTime.UtcNow,
                    LastRefreshedAt = DateTime.UtcNow
                };

                _context.DiscordOAuthTokens.Add(newToken);

                _logger.LogInformation("Created new Discord OAuth tokens for user {UserId}, Discord user {DiscordUserId}",
                    applicationUserId, discordUserId);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store Discord OAuth tokens for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Retrieving access token for user {UserId}", applicationUserId);

        try
        {
            var token = await _context.DiscordOAuthTokens
                .FirstOrDefaultAsync(t => t.ApplicationUserId == applicationUserId, cancellationToken);

            if (token == null)
            {
                _logger.LogDebug("No OAuth token found for user {UserId}", applicationUserId);
                return null;
            }

            // Check if token is expired
            if (token.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Access token for user {UserId} has expired (expired at {ExpiresAt}). Attempting refresh.",
                    applicationUserId, token.AccessTokenExpiresAt);

                // Try to refresh the token
                var refreshed = await RefreshTokenIfNeededAsync(token, cancellationToken);
                if (!refreshed)
                {
                    _logger.LogWarning("Failed to refresh expired token for user {UserId}", applicationUserId);
                    return null;
                }

                // Reload the token after refresh
                token = await _context.DiscordOAuthTokens
                    .FirstOrDefaultAsync(t => t.ApplicationUserId == applicationUserId, cancellationToken);

                if (token == null)
                {
                    _logger.LogError("Token disappeared after refresh for user {UserId}", applicationUserId);
                    return null;
                }
            }
            // Check if token is about to expire and refresh proactively
            else if (token.AccessTokenExpiresAt <= DateTime.UtcNow.Add(TimeSpan.FromMinutes(_bgOptions.OnDemandRefreshThresholdMinutes)))
            {
                _logger.LogDebug("Access token for user {UserId} expires soon ({ExpiresAt}), attempting proactive refresh",
                    applicationUserId, token.AccessTokenExpiresAt);

                // Try to refresh, but don't fail if refresh doesn't work - return current token
                var refreshed = await RefreshTokenIfNeededAsync(token, cancellationToken);
                if (refreshed)
                {
                    // Reload the token after refresh
                    token = await _context.DiscordOAuthTokens
                        .FirstOrDefaultAsync(t => t.ApplicationUserId == applicationUserId, cancellationToken);

                    if (token == null)
                    {
                        _logger.LogError("Token disappeared after refresh for user {UserId}", applicationUserId);
                        return null;
                    }
                }
                else
                {
                    _logger.LogDebug("Proactive refresh failed for user {UserId}, using existing token", applicationUserId);
                }
            }

            // Decrypt and return access token
            var decryptedToken = _protector.Unprotect(token.EncryptedAccessToken);
            _logger.LogTrace("Successfully retrieved access token for user {UserId}", applicationUserId);
            return decryptedToken;
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt access token for user {UserId}. Data protection keys may have changed.",
                applicationUserId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve access token for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasValidTokenAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for valid token for user {UserId}", applicationUserId);

        try
        {
            var hasValidToken = await _context.DiscordOAuthTokens
                .Where(t => t.ApplicationUserId == applicationUserId)
                .Where(t => t.AccessTokenExpiresAt > DateTime.UtcNow)
                .AnyAsync(cancellationToken);

            _logger.LogDebug("User {UserId} has valid token: {HasValidToken}", applicationUserId, hasValidToken);
            return hasValidToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check token validity for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteTokensAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting Discord OAuth tokens for user {UserId}", applicationUserId);

        try
        {
            var token = await _context.DiscordOAuthTokens
                .FirstOrDefaultAsync(t => t.ApplicationUserId == applicationUserId, cancellationToken);

            if (token != null)
            {
                _context.DiscordOAuthTokens.Remove(token);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully deleted OAuth tokens for user {UserId}", applicationUserId);
            }
            else
            {
                _logger.LogDebug("No OAuth tokens found to delete for user {UserId}", applicationUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete OAuth tokens for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscordOAuthToken>> GetExpiringTokensAsync(
        TimeSpan expiresWithin,
        CancellationToken cancellationToken = default)
    {
        var expirationThreshold = DateTime.UtcNow.Add(expiresWithin);

        _logger.LogDebug("Retrieving tokens expiring within {ExpiresWithin} (before {ExpirationThreshold})",
            expiresWithin, expirationThreshold);

        try
        {
            var expiringTokens = await _context.DiscordOAuthTokens
                .Where(t => t.AccessTokenExpiresAt <= expirationThreshold)
                .Where(t => t.AccessTokenExpiresAt > DateTime.UtcNow) // Still valid, just expiring soon
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} tokens expiring within {ExpiresWithin}",
                expiringTokens.Count, expiresWithin);

            return expiringTokens.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve expiring tokens");
            throw;
        }
    }

    /// <summary>
    /// Refreshes an OAuth token using Discord's token endpoint.
    /// </summary>
    /// <param name="token">The token entity to refresh (contains encrypted refresh token).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if refresh succeeded, false otherwise.</returns>
    private async Task<bool> RefreshTokenIfNeededAsync(
        DiscordOAuthToken token,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Refreshing token for user {UserId}, Discord user {DiscordUserId}",
            token.ApplicationUserId, token.DiscordUserId);

        try
        {
            // Get OAuth client credentials from options
            var clientId = _oauthOptions.ClientId;
            var clientSecret = _oauthOptions.ClientSecret;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("Discord OAuth credentials not configured, cannot refresh tokens");
                return false;
            }

            // Decrypt the refresh token
            string decryptedRefreshToken;
            try
            {
                decryptedRefreshToken = _protector.Unprotect(token.EncryptedRefreshToken);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt refresh token for user {UserId}. Data protection keys may have changed.",
                    token.ApplicationUserId);
                return false;
            }

            // Prepare token refresh request
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = decryptedRefreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            });

            // Make HTTP request to Discord token endpoint
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(
                "https://discord.com/api/oauth2/token",
                requestContent,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to refresh token for user {UserId}, status {StatusCode}: {ErrorContent}",
                    token.ApplicationUserId,
                    response.StatusCode,
                    errorContent);
                return false;
            }

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<DiscordTokenResponse>(
                responseContent,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Failed to deserialize Discord token response for user {UserId}", token.ApplicationUserId);
                return false;
            }

            // Calculate new expiration time
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Store the updated tokens (this method will update the existing record)
            await StoreTokensAsync(
                token.ApplicationUserId,
                token.DiscordUserId,
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                expiresAt,
                tokenResponse.Scope,
                cancellationToken);

            _logger.LogInformation(
                "Successfully refreshed token for user {UserId}, Discord user {DiscordUserId}, new expiration: {ExpiresAt}",
                token.ApplicationUserId,
                token.DiscordUserId,
                expiresAt);

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while refreshing token for user {UserId}", token.ApplicationUserId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token for user {UserId}", token.ApplicationUserId);
            return false;
        }
    }

    // Private class for Discord token response deserialization
    private class DiscordTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}
