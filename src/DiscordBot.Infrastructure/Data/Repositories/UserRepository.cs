using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for User entities with Discord-specific operations.
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(BotDbContext context, ILogger<UserRepository> logger, ILogger<Repository<User>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<User?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving user by Discord ID {UserId}", discordId);
        return await DbSet.FindAsync(new object[] { discordId }, cancellationToken);
    }

    public async Task<User?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving user {UserId} with command logs", discordId);
        return await DbSet
            .Include(u => u.CommandLogs)
            .FirstOrDefaultAsync(u => u.Id == discordId, cancellationToken);
    }

    public async Task UpdateLastSeenAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Updating last seen timestamp for user {UserId}", discordId);

        await DbSet
            .Where(u => u.Id == discordId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.LastSeenAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default)
    {
        var existing = await GetByDiscordIdAsync(user.Id, cancellationToken);

        if (existing == null)
        {
            _logger.LogInformation("Creating new user record for {UserId} ({Username})", user.Id, user.Username);
            await DbSet.AddAsync(user, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Updating existing user record for {UserId} ({Username})", user.Id, user.Username);
            existing.Username = user.Username;
            existing.Discriminator = user.Discriminator;
            existing.LastSeenAt = user.LastSeenAt;
            DbSet.Update(existing);
            user = existing;
        }

        await Context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<IReadOnlyList<User>> GetRecentlyActiveAsync(
        TimeSpan timeframe,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - timeframe;
        _logger.LogDebug("Retrieving users active since {CutoffTime}", cutoffTime);

        return await DbSet
            .Where(u => u.LastSeenAt >= cutoffTime)
            .OrderByDescending(u => u.LastSeenAt)
            .ToListAsync(cancellationToken);
    }
}
