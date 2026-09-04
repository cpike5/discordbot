using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// User-facing lifecycle actions on a Rat Watch: creation, cancellation, clearing, and voting.
/// </summary>
public interface IRatWatchLifecycle
{
    /// <summary>
    /// Creates a new Rat Watch.
    /// </summary>
    /// <param name="dto">The watch creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created watch as a DTO.</returns>
    Task<RatWatchDto> CreateWatchAsync(RatWatchCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending Rat Watch.
    /// </summary>
    /// <param name="id">Unique identifier of the watch.</param>
    /// <param name="reason">Reason for cancellation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if cancelled successfully, false if not found or already completed.</returns>
    Task<bool> CancelWatchAsync(Guid id, string reason, CancellationToken ct = default);

    /// <summary>
    /// Clears a watch when the accused checks in early.
    /// Only allowed for the accused user.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="userId">Discord user ID of the user checking in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if cleared successfully, false if not found, not the accused, or already completed.</returns>
    Task<bool> ClearWatchAsync(Guid watchId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Casts or changes a vote on a Rat Watch.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="voterId">Discord user ID of the voter.</param>
    /// <param name="isGuilty">True for guilty vote, false for not guilty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if vote was cast successfully, false if watch not found or not in voting status.</returns>
    Task<bool> CastVoteAsync(Guid watchId, ulong voterId, bool isGuilty, CancellationToken ct = default);
}
