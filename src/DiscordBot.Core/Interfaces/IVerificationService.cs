using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing Discord bot account verification codes.
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// Initiates a verification request for a user.
    /// Creates a pending verification record.
    /// </summary>
    /// <param name="applicationUserId">The user initiating verification.</param>
    /// <param name="ipAddress">Optional IP address for audit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with verification ID if successful.</returns>
    Task<VerificationInitiationResult> InitiateVerificationAsync(
        string applicationUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a verification code for a Discord user.
    /// Called when user runs /verify-account command.
    /// </summary>
    /// <param name="discordUserId">Discord user ID from the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with generated code if successful.</returns>
    Task<CodeGenerationResult> GenerateCodeForDiscordUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a verification code and links accounts.
    /// </summary>
    /// <param name="applicationUserId">User submitting the code.</param>
    /// <param name="code">The verification code entered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with reason.</returns>
    Task<CodeValidationResult> ValidateCodeAsync(
        string applicationUserId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any pending verification for a user.
    /// </summary>
    /// <param name="applicationUserId">User cancelling verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a Discord user has exceeded the rate limit for code generation.
    /// </summary>
    /// <param name="discordUserId">Discord user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rate limit exceeded, false otherwise.</returns>
    Task<bool> IsRateLimitedAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current pending verification for a user, if any.
    /// </summary>
    /// <param name="applicationUserId">User to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending verification or null.</returns>
    Task<VerificationCode?> GetPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired verification codes.
    /// Called by background service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of codes cleaned up.</returns>
    Task<int> CleanupExpiredCodesAsync(CancellationToken cancellationToken = default);
}
