using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for FeatureRequest and FeatureRequestRejection entities.
/// </summary>
public class FeatureRequestRepository : IFeatureRequestRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<FeatureRequestRepository> _logger;

    public FeatureRequestRepository(BotDbContext context, ILogger<FeatureRequestRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FeatureRequest> AddAsync(FeatureRequest entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.FeatureRequests.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Added FeatureRequest {Id} for guild {GuildId}", entity.Id, entity.GuildId);
        return entity;
    }

    /// <inheritdoc/>
    public async Task<FeatureRequest?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Retrieving FeatureRequest {Id}", id);
        return await _context.FeatureRequests.FindAsync(id);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<FeatureRequest> Items, int Total)> GetByGuildIdAsync(
        ulong guildId, FeatureRequestStatus? statusFilter, int page, int pageSize)
    {
        _logger.LogDebug(
            "Retrieving feature requests for guild {GuildId}, status {Status}, page {Page}, pageSize {PageSize}",
            guildId, statusFilter, page, pageSize);

        var query = _context.FeatureRequests
            .AsNoTracking()
            .Where(f => f.GuildId == guildId);

        if (statusFilter.HasValue)
        {
            query = query.Where(f => f.Status == statusFilter.Value);
        }

        var total = await query.CountAsync();

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogDebug(
            "Retrieved {Count} feature requests for guild {GuildId} out of {Total} total",
            items.Count, guildId, total);

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(FeatureRequest entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        _context.FeatureRequests.Update(entity);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Updated FeatureRequest {Id}, status {Status}", entity.Id, entity.Status);
    }

    /// <inheritdoc/>
    public async Task AddRejectionAsync(FeatureRequestRejection rejection)
    {
        rejection.CreatedAt = DateTime.UtcNow;

        _context.FeatureRequestRejections.Add(rejection);
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Recorded FeatureRequestRejection for user {UserId} in guild {GuildId}: {Reason}",
            rejection.UserId, rejection.GuildId, rejection.RejectionReason);
    }
}
