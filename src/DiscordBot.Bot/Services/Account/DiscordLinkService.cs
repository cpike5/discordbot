using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Services.Account;

/// <inheritdoc cref="IDiscordLinkService" />
public class DiscordLinkService : IDiscordLinkService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDiscordTokenService _tokenService;
    private readonly IDiscordUserInfoService _userInfoService;
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly IVerificationService _verificationService;
    private readonly ILogger<DiscordLinkService> _logger;

    public DiscordLinkService(
        UserManager<ApplicationUser> userManager,
        IDiscordTokenService tokenService,
        IDiscordUserInfoService userInfoService,
        IUserDiscordGuildService userDiscordGuildService,
        IVerificationService verificationService,
        ILogger<DiscordLinkService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _userInfoService = userInfoService;
        _userDiscordGuildService = userDiscordGuildService;
        _verificationService = verificationService;
        _logger = logger;
    }

    public async Task<DiscordLinkOperationOutcome> UnlinkAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(UnlinkAsync));

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to unlink Discord but no Discord account is linked", user.Id);
            return new DiscordLinkOperationOutcome(false, "not-linked");
        }

        var discordUserId = user.DiscordUserId.Value;
        _logger.LogInformation("User {UserId} unlinking Discord account (Discord ID: {DiscordUserId})", user.Id, discordUserId);

        try
        {
            user.DiscordUserId = null;
            user.DiscordUsername = null;
            user.DiscordAvatarUrl = null;

            await _tokenService.DeleteTokensAsync(user.Id, cancellationToken);
            _logger.LogDebug("Deleted OAuth tokens for user {UserId}", user.Id);

            await _userDiscordGuildService.DeleteUserGuildsAsync(user.Id, cancellationToken);
            _logger.LogDebug("Deleted stored guild memberships for user {UserId}", user.Id);

            _userInfoService.InvalidateCache(user.Id);
            _logger.LogDebug("Invalidated Discord user info cache for user {UserId}", user.Id);

            var logins = await _userManager.GetLoginsAsync(user);
            var discordLogin = logins.FirstOrDefault(l => l.LoginProvider == "Discord");
            if (discordLogin != null)
            {
                var removeLoginResult = await _userManager.RemoveLoginAsync(user, discordLogin.LoginProvider, discordLogin.ProviderKey);
                if (removeLoginResult.Succeeded)
                {
                    _logger.LogDebug("Removed Discord external login for user {UserId}", user.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to remove Discord external login for user {UserId}: {Errors}",
                        user.Id, string.Join(", ", removeLoginResult.Errors.Select(e => e.Description)));
                }
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                _logger.LogInformation("Successfully unlinked Discord account for user {UserId}", user.Id);
                return new DiscordLinkOperationOutcome(true, "unlink-success");
            }

            _logger.LogError("Failed to update user {UserId} after unlinking Discord: {Errors}",
                user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return new DiscordLinkOperationOutcome(false, "unlink-failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking Discord account for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(false, "unlink-error");
        }
    }

    public async Task<DiscordLinkOperationOutcome> RefreshDiscordDataAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(RefreshDiscordDataAsync));

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to refresh Discord data but no Discord account is linked", user.Id);
            return new DiscordLinkOperationOutcome(false, "not-linked");
        }

        _logger.LogInformation("User {UserId} refreshing Discord guild data", user.Id);

        try
        {
            await _userDiscordGuildService.RefreshUserGuildsAsync(user.Id, cancellationToken);
            _userInfoService.InvalidateCache(user.Id);

            _logger.LogInformation("User {UserId} refreshed Discord guild data successfully", user.Id);
            return new DiscordLinkOperationOutcome(true, "refresh-success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Discord data for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(false, "refresh-error");
        }
    }

    public async Task<DiscordLinkOperationOutcome> InitiateBotVerificationAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(InitiateBotVerificationAsync));
        _logger.LogInformation("User {UserId} initiating bot verification", user.Id);

        try
        {
            var result = await _verificationService.InitiateVerificationAsync(user.Id, ipAddress, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation("Bot verification initiated successfully for user {UserId}, verification ID: {VerificationId}",
                    user.Id, result.VerificationId);
                return new DiscordLinkOperationOutcome(true, "verify-init-success");
            }

            _logger.LogWarning("Failed to initiate bot verification for user {UserId}: {ErrorCode} - {ErrorMessage}",
                user.Id, result.ErrorCode, result.ErrorMessage);
            return new DiscordLinkOperationOutcome(false, "verify-init-failed", result.ErrorMessage ?? "Failed to initiate verification.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating bot verification for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(false, "verify-init-error");
        }
    }

    public async Task<DiscordLinkOperationOutcome> VerifyCodeAsync(ApplicationUser user, string? code, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(VerifyCodeAsync));

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("User {UserId} submitted empty verification code", user.Id);
            return new DiscordLinkOperationOutcome(false, "verify-code-empty");
        }

        var cleanCode = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        _logger.LogInformation("User {UserId} attempting to verify code", user.Id);

        try
        {
            var result = await _verificationService.ValidateCodeAsync(user.Id, cleanCode, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation("Code verified successfully for user {UserId}, linked Discord user {DiscordUserId}",
                    user.Id, result.LinkedDiscordUserId);
                return new DiscordLinkOperationOutcome(true, "verify-code-success", result.LinkedDiscordUsername ?? "Discord User");
            }

            _logger.LogWarning("Code verification failed for user {UserId}: {ErrorCode} - {ErrorMessage}",
                user.Id, result.ErrorCode, result.ErrorMessage);
            return new DiscordLinkOperationOutcome(false, "verify-code-failed", result.ErrorMessage ?? "Invalid verification code.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying code for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(false, "verify-code-error");
        }
    }

    public async Task<DiscordLinkOperationOutcome> CancelVerificationAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(CancelVerificationAsync));
        _logger.LogInformation("User {UserId} cancelling pending verification", user.Id);

        try
        {
            await _verificationService.CancelPendingVerificationAsync(user.Id, cancellationToken);
            _logger.LogInformation("Verification cancelled successfully for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(true, "cancel-success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling verification for user {UserId}", user.Id);
            return new DiscordLinkOperationOutcome(false, "cancel-error");
        }
    }
}
