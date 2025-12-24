using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Guild entities with Discord-specific operations.
/// </summary>
public class GuildRepository : Repository<Guild>, IGuildRepository
{
    private readonly ILogger<GuildRepository> _logger;

    public GuildRepository(BotDbContext context, ILogger<GuildRepository> logger, ILogger<Repository<Guild>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<Guild?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild by Discord ID {GuildId}", discordId);
        return await DbSet.FindAsync(new object[] { discordId }, cancellationToken);
    }

    public async Task<IReadOnlyList<Guild>> GetActiveGuildsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all active guilds");
        return await DbSet
            .Where(g => g.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guild?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild {GuildId} with command logs", discordId);
        return await DbSet
            .Include(g => g.CommandLogs)
            .FirstOrDefaultAsync(g => g.Id == discordId, cancellationToken);
    }

    public async Task SetActiveStatusAsync(ulong discordId, bool isActive, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting guild {GuildId} active status to {IsActive}", discordId, isActive);

        await DbSet
            .Where(g => g.Id == discordId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(g => g.IsActive, isActive),
                cancellationToken);
    }

    public async Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default)
    {
        var existing = await GetByDiscordIdAsync(guild.Id, cancellationToken);

        if (existing == null)
        {
            _logger.LogInformation("Creating new guild record for {GuildId} ({GuildName})", guild.Id, guild.Name);
            await DbSet.AddAsync(guild, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Updating existing guild record for {GuildId} ({GuildName})", guild.Id, guild.Name);
            existing.Name = guild.Name;
            existing.IsActive = guild.IsActive;
            existing.Prefix = guild.Prefix;
            existing.Settings = guild.Settings;
            DbSet.Update(existing);
            guild = existing;
        }

        await Context.SaveChangesAsync(cancellationToken);
        return guild;
    }

    public async Task<int> GetJoinedCountAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild join count since {Since}", since);

        var count = await DbSet
            .AsNoTracking()
            .Where(g => g.JoinedAt >= since)
            .CountAsync(cancellationToken);

        _logger.LogDebug("Found {Count} guilds joined since {Since}", count, since);
        return count;
    }

    public async Task<int> GetLeftCountAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild leave count since {Since}", since);

        // For guilds that left, we track them by checking IsActive = false and assuming
        // the last update time (which we don't track currently). This is a limitation
        // and should ideally track a LeftAt field in the future.
        // For now, we return 0 as we don't have a reliable way to track this without schema changes.
        // TODO: Add LeftAt or LastModifiedAt column to Guild table for accurate tracking.

        _logger.LogWarning("Guild leave tracking requires schema changes (LeftAt field) - returning 0 for now");
        return 0;
    }
}
