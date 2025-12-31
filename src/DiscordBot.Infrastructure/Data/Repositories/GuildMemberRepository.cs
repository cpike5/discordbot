using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildMember entities with Discord-specific operations.
/// </summary>
public class GuildMemberRepository : Repository<GuildMember>, IGuildMemberRepository
{
    private readonly ILogger<GuildMemberRepository> _logger;

    public GuildMemberRepository(
        BotDbContext context,
        ILogger<GuildMemberRepository> logger,
        ILogger<Repository<GuildMember>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<GuildMember?> GetByGuildAndUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild member for guild {GuildId} and user {UserId}", guildId, userId);

        return await DbSet
            .FirstOrDefaultAsync(
                gm => gm.GuildId == guildId && gm.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<GuildMember>> GetActiveByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active members for guild {GuildId}", guildId);

        return await DbSet
            .Where(gm => gm.GuildId == guildId && gm.IsActive)
            .Include(gm => gm.User)
            .OrderBy(gm => gm.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<ulong>> GetMemberUserIdsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving member user IDs for guild {GuildId}", guildId);

        var userIds = await DbSet
            .Where(gm => gm.GuildId == guildId && gm.IsActive)
            .Select(gm => gm.UserId)
            .ToListAsync(cancellationToken);

        return new HashSet<ulong>(userIds);
    }

    public async Task<GuildMember> UpsertAsync(
        GuildMember member,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByGuildAndUserAsync(member.GuildId, member.UserId, cancellationToken);

        if (existing == null)
        {
            _logger.LogInformation(
                "Creating new guild member record for user {UserId} in guild {GuildId}",
                member.UserId, member.GuildId);

            await DbSet.AddAsync(member, cancellationToken);
        }
        else
        {
            _logger.LogDebug(
                "Updating existing guild member record for user {UserId} in guild {GuildId}",
                member.UserId, member.GuildId);

            existing.Nickname = member.Nickname;
            existing.CachedRolesJson = member.CachedRolesJson;
            existing.JoinedAt = member.JoinedAt;
            existing.LastActiveAt = member.LastActiveAt;
            existing.LastCachedAt = member.LastCachedAt;
            existing.IsActive = member.IsActive;

            DbSet.Update(existing);
            member = existing;
        }

        await Context.SaveChangesAsync(cancellationToken);
        return member;
    }

    public async Task<int> BatchUpsertAsync(
        IEnumerable<GuildMember> members,
        CancellationToken cancellationToken = default)
    {
        var membersList = members.ToList();
        if (!membersList.Any())
        {
            _logger.LogDebug("BatchUpsertAsync called with empty collection");
            return 0;
        }

        var guildId = membersList.First().GuildId;
        _logger.LogInformation(
            "Starting batch upsert for {Count} members in guild {GuildId}",
            membersList.Count, guildId);

        var totalAffected = 0;
        var batchSize = 500;

        for (int i = 0; i < membersList.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = membersList.Skip(i).Take(batchSize).ToList();
            var batchUserIds = batch.Select(m => m.UserId).ToList();

            _logger.LogDebug(
                "Processing batch {BatchStart}-{BatchEnd} of {Total}",
                i + 1, Math.Min(i + batchSize, membersList.Count), membersList.Count);

            using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Load existing members for this batch
                var existingMembers = await DbSet
                    .Where(gm => gm.GuildId == guildId && batchUserIds.Contains(gm.UserId))
                    .ToListAsync(cancellationToken);

                var existingUserIds = existingMembers.Select(gm => gm.UserId).ToHashSet();

                // Separate new and existing members
                var newMembers = batch.Where(m => !existingUserIds.Contains(m.UserId)).ToList();
                var updateMembers = batch.Where(m => existingUserIds.Contains(m.UserId)).ToList();

                // Add new members
                if (newMembers.Any())
                {
                    await DbSet.AddRangeAsync(newMembers, cancellationToken);
                    _logger.LogDebug("Adding {Count} new members in batch", newMembers.Count);
                }

                // Update existing members
                foreach (var member in updateMembers)
                {
                    var existing = existingMembers.First(gm => gm.UserId == member.UserId);
                    existing.Nickname = member.Nickname;
                    existing.CachedRolesJson = member.CachedRolesJson;
                    existing.JoinedAt = member.JoinedAt;
                    existing.LastActiveAt = member.LastActiveAt;
                    existing.LastCachedAt = member.LastCachedAt;
                    existing.IsActive = member.IsActive;
                }

                if (updateMembers.Any())
                {
                    _logger.LogDebug("Updating {Count} existing members in batch", updateMembers.Count);
                }

                var affected = await Context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                totalAffected += affected;

                _logger.LogDebug(
                    "Batch upsert completed: {BatchStart}-{BatchEnd} of {Total}, {Affected} records affected",
                    i + 1, Math.Min(i + batchSize, membersList.Count), membersList.Count, affected);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Batch upsert failed for batch {BatchStart}-{BatchEnd} in guild {GuildId}",
                    i + 1, Math.Min(i + batchSize, membersList.Count), guildId);
                throw;
            }
        }

        _logger.LogInformation(
            "Batch upsert completed for guild {GuildId}. {Total} members processed, {Affected} records affected",
            guildId, membersList.Count, totalAffected);

        return totalAffected;
    }

    public async Task<int> MarkInactiveExceptAsync(
        ulong guildId,
        IEnumerable<ulong> activeUserIds,
        CancellationToken cancellationToken = default)
    {
        var activeUserIdsList = activeUserIds.ToList();

        _logger.LogInformation(
            "Marking members inactive for guild {GuildId} except {ActiveCount} active users",
            guildId, activeUserIdsList.Count);

        var affected = await DbSet
            .Where(gm => gm.GuildId == guildId
                && gm.IsActive
                && !activeUserIdsList.Contains(gm.UserId))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(gm => gm.IsActive, false),
                cancellationToken);

        _logger.LogInformation(
            "Marked {Count} members as inactive in guild {GuildId}",
            affected, guildId);

        return affected;
    }

    public async Task<bool> MarkInactiveAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking member {UserId} as inactive in guild {GuildId}", userId, guildId);

        var affected = await DbSet
            .Where(gm => gm.GuildId == guildId && gm.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(gm => gm.IsActive, false),
                cancellationToken);

        return affected > 0;
    }

    public async Task<bool> UpdateMemberInfoAsync(
        ulong guildId,
        ulong userId,
        string? nickname,
        string? cachedRolesJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Updating member info for user {UserId} in guild {GuildId}",
            userId, guildId);

        var affected = await DbSet
            .Where(gm => gm.GuildId == guildId && gm.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(gm => gm.Nickname, nickname)
                    .SetProperty(gm => gm.CachedRolesJson, cachedRolesJson)
                    .SetProperty(gm => gm.LastCachedAt, DateTime.UtcNow),
                cancellationToken);

        return affected > 0;
    }

    public async Task<int> GetMemberCountAsync(
        ulong guildId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting member count for guild {GuildId}, activeOnly: {ActiveOnly}",
            guildId, activeOnly);

        var query = DbSet.Where(gm => gm.GuildId == guildId);

        if (activeOnly)
        {
            query = query.Where(gm => gm.IsActive);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting last sync time for guild {GuildId}", guildId);

        return await DbSet
            .Where(gm => gm.GuildId == guildId)
            .MaxAsync(gm => (DateTime?)gm.LastCachedAt, cancellationToken);
    }
}
