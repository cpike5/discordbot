using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for RatVote entities with vote-specific operations.
/// </summary>
public class RatVoteRepository : Repository<RatVote>, IRatVoteRepository
{
    private readonly ILogger<RatVoteRepository> _logger;

    public RatVoteRepository(
        BotDbContext context,
        ILogger<RatVoteRepository> logger,
        ILogger<Repository<RatVote>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<RatVote?> GetUserVoteAsync(
        Guid ratWatchId,
        ulong voterUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving vote for Rat Watch {RatWatchId} from user {VoterUserId}",
            ratWatchId, voterUserId);

        var vote = await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.RatWatchId == ratWatchId && v.VoterUserId == voterUserId,
                cancellationToken);

        _logger.LogDebug("Vote found: {Found}", vote != null);
        return vote;
    }

    public async Task<(int GuiltyCount, int NotGuiltyCount)> GetVoteTallyAsync(
        Guid ratWatchId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating vote tally for Rat Watch {RatWatchId}", ratWatchId);

        var votes = await DbSet
            .AsNoTracking()
            .Where(v => v.RatWatchId == ratWatchId)
            .ToListAsync(cancellationToken);

        var guiltyCount = votes.Count(v => v.IsGuiltyVote);
        var notGuiltyCount = votes.Count(v => !v.IsGuiltyVote);

        _logger.LogDebug(
            "Vote tally for Rat Watch {RatWatchId}: Guilty={GuiltyCount}, Not Guilty={NotGuiltyCount}",
            ratWatchId, guiltyCount, notGuiltyCount);

        return (guiltyCount, notGuiltyCount);
    }
}
