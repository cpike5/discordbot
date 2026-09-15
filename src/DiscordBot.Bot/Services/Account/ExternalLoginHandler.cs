using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Account;

/// <summary>
/// Default <see cref="IExternalLoginHandler"/> - a near-verbatim port of
/// <c>Pages/Account/ExternalLogin.cshtml.cs</c> (docs/plans/blazor-port-plan.md Phase 4 cluster
/// 4c): same token-extraction-before-sign-in ordering, same three linking branches (existing user
/// by Discord id, existing user by email, brand-new user), same token/guild storage calls. Every
/// <c>RedirectToPage("./Login", ...)</c> becomes <see cref="ExternalLoginOutcome.Error"/>,
/// <c>Redirect("/Account/Lockout")</c> becomes <see cref="ExternalLoginOutcome.Lockout"/>, and
/// <c>LocalRedirect(returnUrl)</c> becomes <see cref="ExternalLoginOutcome.Redirect"/>.
/// </summary>
public sealed class ExternalLoginHandler : IExternalLoginHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDiscordTokenService _tokenService;
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ExternalLoginHandler> _logger;

    public ExternalLoginHandler(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IDiscordTokenService tokenService,
        IUserDiscordGuildService userDiscordGuildService,
        IHttpClientFactory httpClientFactory,
        IAuditLogService auditLogService,
        ILogger<ExternalLoginHandler> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _userDiscordGuildService = userDiscordGuildService;
        _httpClientFactory = httpClientFactory;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ExternalLoginOutcome> HandleCallbackAsync(
        HttpContext httpContext,
        string? remoteError,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            _logger.LogWarning("External login failed with remote error: {RemoteError}", remoteError);
            return new ExternalLoginOutcome.Error("discord_error");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("External login info was null - user may have cancelled or callback failed");
            return new ExternalLoginOutcome.Error("discord_error");
        }

        _logger.LogInformation("External login callback received from {Provider}", info.LoginProvider);

        // Extract OAuth tokens BEFORE signing in (they come from external auth cookie).
        // IMPORTANT: must specify the external authentication scheme - tokens are stored there,
        // not in the default scheme.
        string? accessToken = null;
        string? refreshToken = null;
        string? expiresAt = null;
        if (info.LoginProvider == "Discord")
        {
            accessToken = await httpContext.GetTokenAsync(IdentityConstants.ExternalScheme, "access_token");
            refreshToken = await httpContext.GetTokenAsync(IdentityConstants.ExternalScheme, "refresh_token");
            expiresAt = await httpContext.GetTokenAsync(IdentityConstants.ExternalScheme, "expires_at");

            _logger.LogDebug(
                "Retrieved tokens from external auth - AccessToken: {HasAccess}, RefreshToken: {HasRefresh}, ExpiresAt: {ExpiresAt}",
                !string.IsNullOrEmpty(accessToken),
                !string.IsNullOrEmpty(refreshToken),
                expiresAt ?? "null");
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: true,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            _logger.LogInformation("User logged in with {Provider} provider", info.LoginProvider);

            await UpdateUserDiscordInfoAsync(info);

            if (info.LoginProvider == "Discord")
            {
                await StoreOAuthTokensAsync(httpContext, info, accessToken, refreshToken, expiresAt);

                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user != null && !string.IsNullOrEmpty(accessToken))
                {
                    await StoreGuildMembershipsAsync(httpContext, user.Id, accessToken);
                    _userDiscordGuildService.InvalidateCache(user.Id);
                }
            }

            try
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user != null)
                {
                    var discordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.Security)
                        .WithAction(AuditLogAction.Login)
                        .ByUser(user.Id)
                        .OnTarget("User", user.Id)
                        .FromIpAddress(ipAddress ?? "Unknown")
                        .WithDetails(new { email = user.Email, method = "Discord", discordUsername })
                        .Enqueue();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for Discord OAuth login");
            }

            return new ExternalLoginOutcome.Redirect(returnUrl);
        }

        if (signInResult.IsLockedOut)
        {
            _logger.LogWarning("User account locked out during external login");
            return new ExternalLoginOutcome.Lockout();
        }

        _logger.LogInformation("Creating new user account for {Provider} login", info.LoginProvider);
        return await CreateUserFromExternalLoginAsync(httpContext, info, returnUrl, accessToken, refreshToken, expiresAt);
    }

    private async Task<ExternalLoginOutcome> CreateUserFromExternalLoginAsync(
        HttpContext httpContext,
        ExternalLoginInfo info,
        string returnUrl,
        string? accessToken,
        string? refreshToken,
        string? expiresAt)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var discordId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var discordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
        var avatarHash = info.Principal.FindFirstValue("urn:discord:avatar:hash");

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("External login did not provide email address");
            return new ExternalLoginOutcome.Error("discord_error");
        }

        if (ulong.TryParse(discordId, out var parsedDiscordId))
        {
            var existingDiscordUser = _userManager.Users.FirstOrDefault(u => u.DiscordUserId == parsedDiscordId);
            if (existingDiscordUser != null)
            {
                _logger.LogInformation("Found existing user with Discord ID {DiscordId}, linking {Provider} login", discordId, info.LoginProvider);

                var addLoginResult = await _userManager.AddLoginAsync(existingDiscordUser, info);
                if (addLoginResult.Succeeded || addLoginResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
                {
                    await UpdateExistingUserDiscordInfoAsync(existingDiscordUser, discordId, discordUsername, avatarHash);

                    if (!string.Equals(existingDiscordUser.Email, email, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Updating email for user {UserId} from {OldEmail} to {NewEmail}",
                            existingDiscordUser.Id, existingDiscordUser.Email, email);
                        existingDiscordUser.Email = email;
                        existingDiscordUser.NormalizedEmail = email.ToUpperInvariant();
                        existingDiscordUser.UserName = email;
                        existingDiscordUser.NormalizedUserName = email.ToUpperInvariant();
                        await _userManager.UpdateAsync(existingDiscordUser);
                    }

                    if (info.LoginProvider == "Discord")
                    {
                        await StoreOAuthTokensAsync(httpContext, info, accessToken, refreshToken, expiresAt);

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            await StoreGuildMembershipsAsync(httpContext, existingDiscordUser.Id, accessToken);
                            _userDiscordGuildService.InvalidateCache(existingDiscordUser.Id);
                        }
                    }

                    await _signInManager.SignInAsync(existingDiscordUser, isPersistent: true);
                    _logger.LogInformation("User {Email} signed in after linking {Provider}", email, info.LoginProvider);

                    return new ExternalLoginOutcome.Redirect(returnUrl);
                }

                foreach (var error in addLoginResult.Errors)
                {
                    _logger.LogWarning("Error linking external login to existing Discord user: {Error}", error.Description);
                }

                return new ExternalLoginOutcome.Error("discord_error");
            }
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            _logger.LogInformation("Linking {Provider} login to existing user {Email}", info.LoginProvider, email);

            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                await UpdateExistingUserDiscordInfoAsync(existingUser, discordId, discordUsername, avatarHash);

                if (info.LoginProvider == "Discord")
                {
                    await StoreOAuthTokensAsync(httpContext, info, accessToken, refreshToken, expiresAt);

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        await StoreGuildMembershipsAsync(httpContext, existingUser.Id, accessToken);
                        _userDiscordGuildService.InvalidateCache(existingUser.Id);
                    }
                }

                await _signInManager.SignInAsync(existingUser, isPersistent: true);
                _logger.LogInformation("User {Email} signed in after linking {Provider}", email, info.LoginProvider);

                return new ExternalLoginOutcome.Redirect(returnUrl);
            }

            foreach (var error in addLoginResult.Errors)
            {
                _logger.LogWarning("Error linking external login: {Error}", error.Description);
            }

            return new ExternalLoginOutcome.Error("discord_error");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // Discord emails are verified
            DisplayName = discordUsername ?? email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (ulong.TryParse(discordId, out var discordUserId))
        {
            user.DiscordUserId = discordUserId;
        }
        user.DiscordUsername = discordUsername;

        if (!string.IsNullOrEmpty(avatarHash) && !string.IsNullOrEmpty(discordId))
        {
            user.DiscordAvatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png";
        }

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                _logger.LogWarning("Error creating user: {Error}", error.Description);
            }

            return new ExternalLoginOutcome.Error("discord_error");
        }

        var addExternalLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addExternalLoginResult.Succeeded)
        {
            _logger.LogWarning("Failed to add external login to new user {Email}", email);
            return new ExternalLoginOutcome.Error("discord_error");
        }

        _logger.LogInformation("Created new user {Email} via {Provider} OAuth", email, info.LoginProvider);

        var auditDiscordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
        var auditIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            _auditLogService.CreateBuilder()
                .ForCategory(AuditLogCategory.User)
                .WithAction(AuditLogAction.Created)
                .ByUser(user.Id)
                .OnTarget("User", user.Id)
                .FromIpAddress(auditIpAddress ?? "Unknown")
                .WithDetails(new { email, method = "Discord", discordUsername = auditDiscordUsername })
                .Enqueue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry for user creation via Discord OAuth");
        }

        if (info.LoginProvider == "Discord")
        {
            await StoreOAuthTokensAsync(httpContext, info, accessToken, refreshToken, expiresAt);

            if (!string.IsNullOrEmpty(accessToken))
            {
                await StoreGuildMembershipsAsync(httpContext, user.Id, accessToken);
                _userDiscordGuildService.InvalidateCache(user.Id);
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: true);

        try
        {
            _auditLogService.CreateBuilder()
                .ForCategory(AuditLogCategory.Security)
                .WithAction(AuditLogAction.Login)
                .ByUser(user.Id)
                .OnTarget("User", user.Id)
                .FromIpAddress(auditIpAddress ?? "Unknown")
                .WithDetails(new { email, method = "Discord", discordUsername = auditDiscordUsername })
                .Enqueue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry for Discord OAuth login after user creation");
        }

        return new ExternalLoginOutcome.Redirect(returnUrl);
    }

    private async Task UpdateUserDiscordInfoAsync(ExternalLoginInfo info)
    {
        var userId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user == null)
        {
            return;
        }

        var discordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
        var avatarHash = info.Principal.FindFirstValue("urn:discord:avatar:hash");

        await UpdateExistingUserDiscordInfoAsync(user, userId, discordUsername, avatarHash);
    }

    private async Task UpdateExistingUserDiscordInfoAsync(
        ApplicationUser user,
        string? discordId,
        string? discordUsername,
        string? avatarHash)
    {
        var needsUpdate = false;

        if (!string.IsNullOrEmpty(discordUsername) && user.DiscordUsername != discordUsername)
        {
            user.DiscordUsername = discordUsername;
            needsUpdate = true;
        }

        if (ulong.TryParse(discordId, out var discordUserId) && user.DiscordUserId != discordUserId)
        {
            user.DiscordUserId = discordUserId;
            needsUpdate = true;
        }

        if (!string.IsNullOrEmpty(avatarHash) && !string.IsNullOrEmpty(discordId))
        {
            var avatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png";
            if (user.DiscordAvatarUrl != avatarUrl)
            {
                user.DiscordAvatarUrl = avatarUrl;
                needsUpdate = true;
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        needsUpdate = true;

        if (needsUpdate)
        {
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Stores OAuth tokens for a Discord user. Tokens must be retrieved BEFORE calling
    /// <c>SignInAsync</c>/<c>ExternalLoginSignInAsync</c> as they come from the external auth
    /// cookie.
    /// </summary>
    private async Task StoreOAuthTokensAsync(
        HttpContext httpContext,
        ExternalLoginInfo info,
        string? accessToken,
        string? refreshToken,
        string? expiresAt)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Access token not available from {Provider} OAuth callback - user may have denied permissions", info.LoginProvider);
                return;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Refresh token not available from {Provider} OAuth callback", info.LoginProvider);
                return;
            }

            if (string.IsNullOrEmpty(expiresAt))
            {
                _logger.LogWarning("Token expiration time not available from {Provider} OAuth callback", info.LoginProvider);
                return;
            }

            var discordId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ulong.TryParse(discordId, out var discordUserId))
            {
                _logger.LogWarning("Could not parse Discord user ID from external login claims");
                return;
            }

            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user == null)
            {
                _logger.LogWarning("Could not find user for external login {Provider}:{ProviderKey}", info.LoginProvider, info.ProviderKey);
                return;
            }

            if (!DateTime.TryParse(expiresAt, out var expirationTime))
            {
                _logger.LogWarning("Could not parse token expiration time: {ExpiresAt}", expiresAt);
                return;
            }

            var scopes = "identify email";
            _logger.LogDebug("Storing OAuth tokens for user {UserId}, expires at {ExpiresAt}", user.Id, expirationTime);

            await _tokenService.StoreTokensAsync(
                user.Id,
                discordUserId,
                accessToken,
                refreshToken,
                expirationTime,
                scopes,
                httpContext.RequestAborted);

            _logger.LogInformation("Successfully stored OAuth tokens for user {UserId}, Discord ID {DiscordUserId}", user.Id, discordUserId);
        }
        catch (Exception ex)
        {
            // Don't fail the login if token storage fails - user can still use the app.
            _logger.LogError(ex, "Failed to store OAuth tokens for {Provider} login", info.LoginProvider);
        }
    }

    /// <summary>
    /// Fetches guild memberships from Discord API and stores them locally. Called during OAuth to
    /// capture which guilds the user belongs to.
    /// </summary>
    private async Task StoreGuildMembershipsAsync(HttpContext httpContext, string applicationUserId, string accessToken)
    {
        try
        {
            _logger.LogDebug("Fetching guild memberships from Discord API for user {UserId}", applicationUserId);

            var httpClient = _httpClientFactory.CreateClient("Discord");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("users/@me/guilds", httpContext.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch guilds from Discord API for user {UserId}, status {StatusCode}: {ReasonPhrase}",
                    applicationUserId, response.StatusCode, response.ReasonPhrase);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(httpContext.RequestAborted);
            var discordGuilds = JsonSerializer.Deserialize<List<DiscordApiGuildResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (discordGuilds == null)
            {
                _logger.LogWarning("Failed to deserialize Discord guilds response for user {UserId}", applicationUserId);
                return;
            }

            var guilds = discordGuilds.Select(g => new DiscordGuildDto
            {
                Id = ulong.Parse(g.Id),
                Name = g.Name,
                Icon = g.Icon,
                Owner = g.Owner,
                Permissions = long.Parse(g.Permissions)
            }).ToList();

            var count = await _userDiscordGuildService.StoreGuildMembershipsAsync(
                applicationUserId,
                guilds,
                httpContext.RequestAborted);

            _logger.LogInformation(
                "Successfully stored {Count} guild memberships for user {UserId}",
                count, applicationUserId);
        }
        catch (Exception ex)
        {
            // Don't fail the login if guild storage fails - user can still use the app.
            _logger.LogError(ex, "Failed to store guild memberships for user {UserId}", applicationUserId);
        }
    }

    private sealed class DiscordApiGuildResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public bool Owner { get; set; }
        public string Permissions { get; set; } = "0";
    }
}
