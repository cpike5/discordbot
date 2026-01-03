using System.Security.Cryptography;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing Discord bot account verification codes.
/// </summary>
public class VerificationService : IVerificationService
{
    private readonly BotDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VerificationService> _logger;
    private readonly VerificationOptions _verificationOptions;

    public VerificationService(
        BotDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<VerificationService> logger,
        IOptions<VerificationOptions> verificationOptions)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _verificationOptions = verificationOptions.Value;
    }

    public async Task<VerificationInitiationResult> InitiateVerificationAsync(
        string applicationUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "initiate_verification");

        try
        {
            _logger.LogDebug("Initiating verification for user {UserId}", applicationUserId);

            // Check if user exists and doesn't already have Discord linked
            var user = await _userManager.FindByIdAsync(applicationUserId);
            if (user == null)
            {
                return VerificationInitiationResult.Failure(
                    VerificationInitiationResult.UserNotFound,
                    "User not found.");
            }

            if (user.DiscordUserId.HasValue)
            {
                return VerificationInitiationResult.Failure(
                    VerificationInitiationResult.AlreadyLinked,
                    "Discord account is already linked.");
            }

            // Cancel any existing pending verifications
            await CancelPendingVerificationAsync(applicationUserId, cancellationToken);

            // Create new pending verification
            var verification = new VerificationCode
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = applicationUserId,
                Code = string.Empty, // Code generated when Discord user runs command
                Status = VerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromMinutes(_verificationOptions.CodeExpiryMinutes)),
                IpAddress = ipAddress
            };

            _context.VerificationCodes.Add(verification);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Verification initiated for user {UserId}, verification ID: {VerificationId}",
                applicationUserId, verification.Id);

            BotActivitySource.SetSuccess(activity);
            return VerificationInitiationResult.Success(verification.Id);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<CodeGenerationResult> GenerateCodeForDiscordUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "generate_code",
            userId: discordUserId);

        try
        {
            _logger.LogDebug("Generating verification code for Discord user {DiscordUserId}", discordUserId);

            // Check if Discord user is already linked to an account
            var existingUser = await _userManager.Users
                .FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, cancellationToken);

            if (existingUser != null)
            {
                return CodeGenerationResult.Failure(
                    CodeGenerationResult.AlreadyLinked,
                    "This Discord account is already linked to a user account.");
            }

            // Check rate limit
            if (await IsRateLimitedAsync(discordUserId, cancellationToken))
            {
                return CodeGenerationResult.Failure(
                    CodeGenerationResult.RateLimited,
                    "Rate limit exceeded. You can request a maximum of 3 codes per hour.");
            }

            // Find any pending verification that hasn't been claimed yet
            // (no DiscordUserId assigned means waiting for Discord command)
            var pendingVerification = await _context.VerificationCodes
                .Where(v => v.Status == VerificationStatus.Pending)
                .Where(v => v.DiscordUserId == null)
                .Where(v => v.ExpiresAt > DateTime.UtcNow)
                .OrderBy(v => v.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingVerification == null)
            {
                return CodeGenerationResult.Failure(
                    CodeGenerationResult.NoPendingVerification,
                    "No pending verification found. Please initiate verification from the web interface first.");
            }

            // Generate unique code
            var code = GenerateUniqueCode();

            // Update the verification with Discord user and code
            pendingVerification.DiscordUserId = discordUserId;
            pendingVerification.Code = code;
            pendingVerification.ExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromMinutes(_verificationOptions.CodeExpiryMinutes));

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Verification code generated for Discord user {DiscordUserId}, verification {VerificationId}",
                discordUserId, pendingVerification.Id);

            BotActivitySource.SetSuccess(activity);
            return CodeGenerationResult.Success(code, pendingVerification.ExpiresAt);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<CodeValidationResult> ValidateCodeAsync(
        string applicationUserId,
        string code,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "validate_code");

        try
        {
            // Normalize code (remove dashes, uppercase)
            code = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

            _logger.LogDebug("Validating code for user {UserId}", applicationUserId);

            // Check if user already has Discord linked
            var user = await _userManager.FindByIdAsync(applicationUserId);
            if (user == null)
            {
                return CodeValidationResult.Failure(
                    CodeValidationResult.InvalidCode,
                    "User not found.");
            }

            if (user.DiscordUserId.HasValue)
            {
                return CodeValidationResult.Failure(
                    CodeValidationResult.AlreadyLinked,
                    "Discord account is already linked.");
            }

            // Find the verification by code
            var verification = await _context.VerificationCodes
                .Where(v => v.Code == code)
                .Where(v => v.ApplicationUserId == applicationUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (verification == null)
            {
                _logger.LogWarning("Invalid verification code attempt for user {UserId}", applicationUserId);
                return CodeValidationResult.Failure(
                    CodeValidationResult.InvalidCode,
                    "Invalid verification code.");
            }

            if (verification.Status == VerificationStatus.Completed)
            {
                return CodeValidationResult.Failure(
                    CodeValidationResult.CodeAlreadyUsed,
                    "This code has already been used.");
            }

            if (verification.Status == VerificationStatus.Expired || verification.ExpiresAt <= DateTime.UtcNow)
            {
                verification.Status = VerificationStatus.Expired;
                await _context.SaveChangesAsync(cancellationToken);

                return CodeValidationResult.Failure(
                    CodeValidationResult.CodeExpired,
                    "Verification code has expired. Please request a new code.");
            }

            if (!verification.DiscordUserId.HasValue)
            {
                return CodeValidationResult.Failure(
                    CodeValidationResult.InvalidCode,
                    "Verification code not yet activated. Please run /verify-account in Discord first.");
            }

            // Check if the Discord user is already linked to another account
            var existingDiscordUser = await _userManager.Users
                .FirstOrDefaultAsync(u => u.DiscordUserId == verification.DiscordUserId, cancellationToken);

            if (existingDiscordUser != null)
            {
                return CodeValidationResult.Failure(
                    CodeValidationResult.DiscordAlreadyLinked,
                    "This Discord account is already linked to another user.");
            }

            // Success! Link the accounts
            user.DiscordUserId = verification.DiscordUserId;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                _logger.LogError(
                    "Failed to update user {UserId} with Discord ID: {Errors}",
                    applicationUserId,
                    string.Join(", ", updateResult.Errors.Select(e => e.Description)));

                return CodeValidationResult.Failure(
                    CodeValidationResult.InvalidCode,
                    "Failed to link accounts. Please try again.");
            }

            // Mark verification as completed
            verification.Status = VerificationStatus.Completed;
            verification.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully linked Discord user {DiscordUserId} to user {UserId}",
                verification.DiscordUserId, applicationUserId);

            BotActivitySource.SetSuccess(activity);
            return CodeValidationResult.Success(verification.DiscordUserId.Value);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task CancelPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "cancel_pending_verification");

        try
        {
            var pendingVerifications = await _context.VerificationCodes
                .Where(v => v.ApplicationUserId == applicationUserId)
                .Where(v => v.Status == VerificationStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var verification in pendingVerifications)
            {
                verification.Status = VerificationStatus.Cancelled;
            }

            if (pendingVerifications.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Cancelled {Count} pending verifications for user {UserId}",
                    pendingVerifications.Count, applicationUserId);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<bool> IsRateLimitedAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "check_rate_limit",
            userId: discordUserId);

        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);

            var recentCodeCount = await _context.VerificationCodes
                .Where(v => v.DiscordUserId == discordUserId)
                .Where(v => v.CreatedAt >= oneHourAgo)
                .CountAsync(cancellationToken);

            var isRateLimited = recentCodeCount >= _verificationOptions.MaxCodesPerHour;

            BotActivitySource.SetSuccess(activity);
            return isRateLimited;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<VerificationCode?> GetPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "get_pending_verification");

        try
        {
            var result = await _context.VerificationCodes
                .Where(v => v.ApplicationUserId == applicationUserId)
                .Where(v => v.Status == VerificationStatus.Pending)
                .Where(v => v.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync(cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<int> CleanupExpiredCodesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "verification",
            "cleanup_expired_codes");

        try
        {
            var expiredCodes = await _context.VerificationCodes
                .Where(v => v.Status == VerificationStatus.Pending)
                .Where(v => v.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var code in expiredCodes)
            {
                code.Status = VerificationStatus.Expired;
            }

            if (expiredCodes.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Marked {Count} verification codes as expired", expiredCodes.Count);
            }

            // Delete old completed/expired/cancelled codes (older than configured threshold)
            var oldCutoff = DateTime.UtcNow.AddHours(-_verificationOptions.OldCodeCleanupHours);
            var oldCodes = await _context.VerificationCodes
                .Where(v => v.Status != VerificationStatus.Pending)
                .Where(v => v.CreatedAt <= oldCutoff)
                .ToListAsync(cancellationToken);

            if (oldCodes.Any())
            {
                _context.VerificationCodes.RemoveRange(oldCodes);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Deleted {Count} old verification codes", oldCodes.Count);
            }

            var totalCleaned = expiredCodes.Count + oldCodes.Count;

            BotActivitySource.SetSuccess(activity);
            return totalCleaned;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    private string GenerateUniqueCode()
    {
        var bytes = new byte[_verificationOptions.CodeLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var chars = new char[_verificationOptions.CodeLength];
        for (int i = 0; i < _verificationOptions.CodeLength; i++)
        {
            chars[i] = _verificationOptions.CodeCharset[bytes[i] % _verificationOptions.CodeCharset.Length];
        }

        return new string(chars);
    }
}
