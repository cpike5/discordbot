using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that proactively refreshes Discord OAuth tokens before they expire.
/// Intervals and thresholds are configurable via BackgroundServicesOptions.
/// </summary>
public class DiscordTokenRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiscordOAuthOptions _oauthOptions;
    private readonly BackgroundServicesOptions _bgOptions;
    private readonly ILogger<DiscordTokenRefreshService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordTokenRefreshService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating service scopes (needed for scoped services).</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients for Discord API calls.</param>
    /// <param name="oauthOptions">Discord OAuth configuration options.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DiscordTokenRefreshService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordOAuthOptions> oauthOptions,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<DiscordTokenRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _oauthOptions = oauthOptions.Value;
        _bgOptions = bgOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background token refresh loop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshInterval = TimeSpan.FromMinutes(_bgOptions.TokenRefreshIntervalMinutes);
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.TokenRefreshInitialDelayMinutes);

        _logger.LogInformation("Discord OAuth token refresh service started, checking every {Interval} minutes",
            refreshInterval.TotalMinutes);

        // Wait a bit before first run to allow application to fully start
        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Token refresh service is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh cycle, will retry on next interval");
            }

            try
            {
                await Task.Delay(refreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Token refresh service is shutting down");
                break;
            }
        }

        _logger.LogInformation("Discord OAuth token refresh service stopped");
    }

    /// <summary>
    /// Refreshes all tokens that are expiring within the threshold window.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task RefreshExpiringTokensAsync(CancellationToken cancellationToken)
    {
        var expirationThreshold = TimeSpan.FromHours(_bgOptions.TokenExpirationThresholdHours);
        var refreshDelay = TimeSpan.FromSeconds(_bgOptions.TokenRefreshDelaySeconds);

        _logger.LogDebug("Starting token refresh cycle, checking for tokens expiring within {Threshold} hour(s)",
            expirationThreshold.TotalHours);

        // Create a scope to get scoped services
        using var scope = _scopeFactory.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IDiscordTokenService>();

        IReadOnlyList<DiscordOAuthToken> expiringTokens;
        try
        {
            expiringTokens = await tokenService.GetExpiringTokensAsync(expirationThreshold, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve expiring tokens from database");
            return;
        }

        if (expiringTokens.Count == 0)
        {
            _logger.LogDebug("No tokens require refresh at this time");
            return;
        }

        _logger.LogInformation("Found {Count} token(s) requiring refresh", expiringTokens.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var token in expiringTokens)
        {
            try
            {
                var refreshed = await RefreshTokenAsync(tokenService, token, cancellationToken);
                if (refreshed)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }

                // Rate limit: small delay between refreshes to avoid hitting Discord API limits
                await Task.Delay(refreshDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Token refresh interrupted by shutdown");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error refreshing token for user {UserId}", token.ApplicationUserId);
                failureCount++;
            }
        }

        _logger.LogInformation("Token refresh cycle completed: {SuccessCount} successful, {FailureCount} failed",
            successCount, failureCount);
    }

    /// <summary>
    /// Refreshes a single OAuth token using Discord's token endpoint.
    /// </summary>
    /// <param name="tokenService">Token service for storing updated tokens.</param>
    /// <param name="token">The token entity to refresh (contains encrypted refresh token).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if refresh succeeded, false otherwise.</returns>
    private async Task<bool> RefreshTokenAsync(
        IDiscordTokenService tokenService,
        DiscordOAuthToken token,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Refreshing token for user {UserId}, Discord user {DiscordUserId}, expires at {ExpiresAt}",
            token.ApplicationUserId, token.DiscordUserId, token.AccessTokenExpiresAt);

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
            // We need to create a scope to get the data protection provider
            using var scope = _scopeFactory.CreateScope();
            var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            var protector = dataProtectionProvider.CreateProtector("DiscordOAuth.Tokens.v1");

            string decryptedRefreshToken;
            try
            {
                decryptedRefreshToken = protector.Unprotect(token.EncryptedRefreshToken);
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
            var tokenResponse = JsonSerializer.Deserialize<DiscordTokenResponse>(responseContent, new JsonSerializerOptions
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

            // Store the updated tokens
            await tokenService.StoreTokensAsync(
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
