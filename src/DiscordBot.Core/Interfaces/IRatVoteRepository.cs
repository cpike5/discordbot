using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for RatVote entities with vote-specific operations.
/// </summary>
public interface IRatVoteRepository : IRepository<RatVote>
{
    /// <summary>
    /// Gets a specific user's vote on a Rat Watch.
    /// </summary>
    /// <param name="ratWatchId">Unique identifier of the Rat Watch.</param>
    /// <param name="voterUserId">Discord user ID of the voter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The vote if found, or null if the user hasn't voted.</returns>
    Task<RatVote?> GetUserVoteAsync(
        Guid ratWatchId,
        ulong voterUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the vote tally (guilty and not guilty counts) for a Rat Watch.
    /// </summary>
    /// <param name="ratWatchId">Unique identifier of the Rat Watch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the guilty vote count and not guilty vote count.</returns>
    Task<(int GuiltyCount, int NotGuiltyCount)> GetVoteTallyAsync(
        Guid ratWatchId,
        CancellationToken cancellationToken = default);
}
