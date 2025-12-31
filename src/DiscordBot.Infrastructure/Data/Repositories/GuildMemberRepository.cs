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

    public async Task<(IReadOnlyList<GuildMember> Members, int TotalCount)> GetMembersAsync(
        ulong guildId,
        string? searchTerm = null,
        List<ulong>? roleIds = null,
        DateTime? joinedAtStart = null,
        DateTime? joinedAtEnd = null,
        DateTime? lastActiveAtStart = null,
        DateTime? lastActiveAtEnd = null,
        bool? isActive = true,
        string sortBy = "JoinedAt",
        bool sortDescending = false,
        int page = 1,
        int pageSize = 25,
        List<ulong>? userIds = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving members for guild {GuildId} with filters - SearchTerm: {SearchTerm}, RoleIds: {RoleIds}, UserIds: {UserIds}, IsActive: {IsActive}, Page: {Page}, PageSize: {PageSize}",
            guildId, searchTerm, roleIds != null ? string.Join(",", roleIds) : "none", userIds != null ? string.Join(",", userIds) : "none", isActive, page, pageSize);

        var query = DbSet
            .Where(gm => gm.GuildId == guildId)
            .Include(gm => gm.User)
            .AsQueryable();

        // Apply active status filter
        if (isActive.HasValue)
        {
            query = query.Where(gm => gm.IsActive == isActive.Value);
        }

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(gm =>
                gm.User.Username.ToLower().Contains(lowerSearchTerm) ||
                (gm.User.GlobalDisplayName != null && gm.User.GlobalDisplayName.ToLower().Contains(lowerSearchTerm)) ||
                (gm.Nickname != null && gm.Nickname.ToLower().Contains(lowerSearchTerm)));
        }

        // Apply role filter (members must have ALL specified roles)
        if (roleIds != null && roleIds.Any())
        {
            foreach (var roleId in roleIds)
            {
                var roleIdString = roleId.ToString();
                query = query.Where(gm =>
                    gm.CachedRolesJson != null && gm.CachedRolesJson.Contains(roleIdString));
            }
        }

        // Apply join date range filters
        if (joinedAtStart.HasValue)
        {
            query = query.Where(gm => gm.JoinedAt >= joinedAtStart.Value);
        }
        if (joinedAtEnd.HasValue)
        {
            query = query.Where(gm => gm.JoinedAt <= joinedAtEnd.Value);
        }

        // Apply last active date range filters
        if (lastActiveAtStart.HasValue)
        {
            query = query.Where(gm => gm.LastActiveAt != null && gm.LastActiveAt >= lastActiveAtStart.Value);
        }
        if (lastActiveAtEnd.HasValue)
        {
            query = query.Where(gm => gm.LastActiveAt != null && gm.LastActiveAt <= lastActiveAtEnd.Value);
        }

        // Apply user IDs filter (for exporting selected members)
        if (userIds != null && userIds.Any())
        {
            query = query.Where(gm => userIds.Contains(gm.UserId));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "username" => sortDescending
                ? query.OrderByDescending(gm => gm.User.Username)
                : query.OrderBy(gm => gm.User.Username),
            "displayname" => sortDescending
                ? query.OrderByDescending(gm => gm.Nickname ?? gm.User.GlobalDisplayName ?? gm.User.Username)
                : query.OrderBy(gm => gm.Nickname ?? gm.User.GlobalDisplayName ?? gm.User.Username),
            "lastactiveat" => sortDescending
                ? query.OrderByDescending(gm => gm.LastActiveAt)
                : query.OrderBy(gm => gm.LastActiveAt),
            "joinedat" or _ => sortDescending
                ? query.OrderByDescending(gm => gm.JoinedAt)
                : query.OrderBy(gm => gm.JoinedAt),
        };

        // Apply pagination
        var members = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} of {TotalCount} members for guild {GuildId}",
            members.Count, totalCount, guildId);

        return (members, totalCount);
    }

    public async Task<GuildMember?> GetMemberAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving member {UserId} for guild {GuildId} with User entity", userId, guildId);

        return await DbSet
            .Include(gm => gm.User)
            .FirstOrDefaultAsync(
                gm => gm.GuildId == guildId && gm.UserId == userId,
                cancellationToken);
    }
}
