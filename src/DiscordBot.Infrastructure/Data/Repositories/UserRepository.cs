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

    public async Task<int> BatchUpsertAsync(
        IEnumerable<User> users,
        CancellationToken cancellationToken = default)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            _logger.LogDebug("BatchUpsertAsync called with empty collection");
            return 0;
        }

        _logger.LogInformation("Starting batch upsert for {Count} users", usersList.Count);

        var totalAffected = 0;
        var batchSize = 500;

        for (int i = 0; i < usersList.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = usersList.Skip(i).Take(batchSize).ToList();
            var batchUserIds = batch.Select(u => u.Id).ToList();

            _logger.LogDebug(
                "Processing batch {BatchStart}-{BatchEnd} of {Total}",
                i + 1, Math.Min(i + batchSize, usersList.Count), usersList.Count);

            using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Load existing users for this batch
                var existingUsers = await DbSet
                    .Where(u => batchUserIds.Contains(u.Id))
                    .ToListAsync(cancellationToken);

                var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();

                // Separate new and existing users
                var newUsers = batch.Where(u => !existingUserIds.Contains(u.Id)).ToList();
                var updateUsers = batch.Where(u => existingUserIds.Contains(u.Id)).ToList();

                // Add new users
                if (newUsers.Any())
                {
                    await DbSet.AddRangeAsync(newUsers, cancellationToken);
                    _logger.LogDebug("Adding {Count} new users in batch", newUsers.Count);
                }

                // Update existing users
                foreach (var user in updateUsers)
                {
                    var existing = existingUsers.First(u => u.Id == user.Id);
                    existing.Username = user.Username;
                    existing.Discriminator = user.Discriminator;
                    existing.LastSeenAt = user.LastSeenAt;
                    existing.AccountCreatedAt = user.AccountCreatedAt ?? existing.AccountCreatedAt;
                    existing.AvatarHash = user.AvatarHash ?? existing.AvatarHash;
                    existing.GlobalDisplayName = user.GlobalDisplayName ?? existing.GlobalDisplayName;
                }

                if (updateUsers.Any())
                {
                    _logger.LogDebug("Updating {Count} existing users in batch", updateUsers.Count);
                }

                var affected = await Context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                totalAffected += affected;

                _logger.LogDebug(
                    "Batch upsert completed: {BatchStart}-{BatchEnd} of {Total}, {Affected} records affected",
                    i + 1, Math.Min(i + batchSize, usersList.Count), usersList.Count, affected);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Batch upsert failed for batch {BatchStart}-{BatchEnd}",
                    i + 1, Math.Min(i + batchSize, usersList.Count));
                throw;
            }
        }

        _logger.LogInformation(
            "Batch upsert completed. {Total} users processed, {Affected} records affected",
            usersList.Count, totalAffected);

        return totalAffected;
    }
}
