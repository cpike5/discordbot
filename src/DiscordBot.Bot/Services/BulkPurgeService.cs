using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for bulk data purge operations.
/// </summary>
public class BulkPurgeService : IBulkPurgeService
{
    private readonly BotDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<BulkPurgeService> _logger;

    public BulkPurgeService(
        BotDbContext dbContext,
        IAuditLogService auditLogService,
        IHubContext<DashboardHub> hubContext,
        ILogger<BulkPurgeService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<BulkPurgePreviewDto> PreviewPurgeAsync(
        BulkPurgeCriteriaDto criteria,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "bulkpurge",
            "preview_purge");

        try
        {
            activity?.SetTag("purge.entity_type", criteria.EntityType.ToString());
            activity?.SetTag("purge.guild_id", criteria.GuildId?.ToString());

            _logger.LogDebug(
                "Previewing bulk purge for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
                criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

            var count = criteria.EntityType switch
            {
                BulkPurgeEntityType.Messages => await CountMessagesAsync(criteria, cancellationToken),
                BulkPurgeEntityType.AuditLogs => await CountAuditLogsAsync(criteria, cancellationToken),
                BulkPurgeEntityType.CommandLogs => await CountCommandLogsAsync(criteria, cancellationToken),
                BulkPurgeEntityType.ModerationCases => await CountModerationCasesAsync(criteria, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(criteria.EntityType))
            };

            activity?.SetTag("preview.estimated_count", count);
            BotActivitySource.SetSuccess(activity);

            return BulkPurgePreviewDto.Succeeded(
                criteria.EntityType,
                count,
                criteria.GetDateRangeDescription(),
                criteria.GuildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing bulk purge for {EntityType}", criteria.EntityType);
            BotActivitySource.RecordException(activity, ex);

            return BulkPurgePreviewDto.Failed($"Failed to preview: {ex.Message}");
        }
    }

    public async Task<BulkPurgeResultDto> ExecutePurgeAsync(
        BulkPurgeCriteriaDto criteria,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "bulkpurge",
            "execute_purge");

        try
        {
            activity?.SetTag("purge.entity_type", criteria.EntityType.ToString());
            activity?.SetTag("purge.guild_id", criteria.GuildId?.ToString());
            activity?.SetTag("purge.admin_user_id", adminUserId);

            _logger.LogInformation(
                "Starting bulk purge for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}, Admin: {AdminUserId}",
                criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId, adminUserId);

            // Get count first for progress tracking
            var totalCount = criteria.EntityType switch
            {
                BulkPurgeEntityType.Messages => await CountMessagesAsync(criteria, cancellationToken),
                BulkPurgeEntityType.AuditLogs => await CountAuditLogsAsync(criteria, cancellationToken),
                BulkPurgeEntityType.CommandLogs => await CountCommandLogsAsync(criteria, cancellationToken),
                BulkPurgeEntityType.ModerationCases => await CountModerationCasesAsync(criteria, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(criteria.EntityType))
            };

            if (totalCount == 0)
            {
                _logger.LogWarning("No records found matching criteria for {EntityType}", criteria.EntityType);
                activity?.SetTag("purge.no_records", true);

                return BulkPurgeResultDto.Failed(
                    BulkPurgeResultDto.NoRecordsFound,
                    "No records found matching the specified criteria.");
            }

            // Broadcast initial progress
            await BroadcastProgressAsync(criteria.EntityType, 0, totalCount, false, "Starting purge...");

            // Begin transaction
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                var deletedCount = criteria.EntityType switch
                {
                    BulkPurgeEntityType.Messages => await DeleteMessagesAsync(criteria, totalCount, cancellationToken),
                    BulkPurgeEntityType.AuditLogs => await DeleteAuditLogsAsync(criteria, totalCount, cancellationToken),
                    BulkPurgeEntityType.CommandLogs => await DeleteCommandLogsAsync(criteria, totalCount, cancellationToken),
                    BulkPurgeEntityType.ModerationCases => await DeleteModerationCasesAsync(criteria, totalCount, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(criteria.EntityType))
                };

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully purged {DeletedCount} {EntityType} records",
                    deletedCount, criteria.EntityType);

                // Broadcast completion
                await BroadcastProgressAsync(criteria.EntityType, deletedCount, totalCount, true, "Purge complete.");

                // Create audit log entry
                try
                {
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.System)
                        .WithAction(AuditLogAction.BulkDataPurged)
                        .ByUser(adminUserId)
                        .OnTarget(criteria.EntityType.ToString(), $"{deletedCount} records")
                        .InGuild(criteria.GuildId ?? 0)
                        .WithDetails(new
                        {
                            entityType = criteria.EntityType.ToString(),
                            dateRange = criteria.GetDateRangeDescription(),
                            guildId = criteria.GuildId,
                            deletedCount,
                            timestamp = DateTime.UtcNow
                        })
                        .WithCorrelationId(correlationId)
                        .Enqueue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create audit log entry for bulk purge");
                }

                activity?.SetTag("purge.success", true);
                activity?.SetTag("purge.deleted_count", deletedCount);
                BotActivitySource.SetSuccess(activity);

                return BulkPurgeResultDto.Succeeded(criteria.EntityType, deletedCount, correlationId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogError(ex,
                    "Transaction failed while bulk purging {EntityType}",
                    criteria.EntityType);

                // Broadcast failure
                await BroadcastProgressAsync(criteria.EntityType, 0, totalCount, true, $"Purge failed: {ex.Message}");

                activity?.SetTag("purge.success", false);
                BotActivitySource.RecordException(activity, ex);

                return BulkPurgeResultDto.Failed(
                    BulkPurgeResultDto.TransactionFailed,
                    $"Failed to purge: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while bulk purging {EntityType}",
                criteria.EntityType);

            BotActivitySource.RecordException(activity, ex);

            return BulkPurgeResultDto.Failed(
                BulkPurgeResultDto.DatabaseError,
                "An unexpected error occurred while purging data.");
        }
    }

    private async Task<int> CountMessagesAsync(BulkPurgeCriteriaDto criteria, CancellationToken cancellationToken)
    {
        var query = _dbContext.MessageLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(m => m.Timestamp >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(m => m.Timestamp < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(m => m.GuildId == criteria.GuildId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<int> CountAuditLogsAsync(BulkPurgeCriteriaDto criteria, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(a => a.GuildId == criteria.GuildId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<int> CountCommandLogsAsync(BulkPurgeCriteriaDto criteria, CancellationToken cancellationToken)
    {
        var query = _dbContext.CommandLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(c => c.ExecutedAt >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(c => c.ExecutedAt < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(c => c.GuildId == criteria.GuildId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<int> CountModerationCasesAsync(BulkPurgeCriteriaDto criteria, CancellationToken cancellationToken)
    {
        var query = _dbContext.ModerationCases.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(m => m.GuildId == criteria.GuildId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<int> DeleteMessagesAsync(BulkPurgeCriteriaDto criteria, int totalCount, CancellationToken cancellationToken)
    {
        var query = _dbContext.MessageLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(m => m.Timestamp >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(m => m.Timestamp < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(m => m.GuildId == criteria.GuildId.Value);
        }

        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        await BroadcastProgressAsync(criteria.EntityType, deleted, totalCount, false, "Deleting message logs...");

        return deleted;
    }

    private async Task<int> DeleteAuditLogsAsync(BulkPurgeCriteriaDto criteria, int totalCount, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(a => a.GuildId == criteria.GuildId.Value);
        }

        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        await BroadcastProgressAsync(criteria.EntityType, deleted, totalCount, false, "Deleting audit logs...");

        return deleted;
    }

    private async Task<int> DeleteCommandLogsAsync(BulkPurgeCriteriaDto criteria, int totalCount, CancellationToken cancellationToken)
    {
        var query = _dbContext.CommandLogs.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(c => c.ExecutedAt >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(c => c.ExecutedAt < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(c => c.GuildId == criteria.GuildId.Value);
        }

        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        await BroadcastProgressAsync(criteria.EntityType, deleted, totalCount, false, "Deleting command logs...");

        return deleted;
    }

    private async Task<int> DeleteModerationCasesAsync(BulkPurgeCriteriaDto criteria, int totalCount, CancellationToken cancellationToken)
    {
        var query = _dbContext.ModerationCases.AsQueryable();

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt < criteria.EndDate.Value);
        }

        if (criteria.GuildId.HasValue)
        {
            query = query.Where(m => m.GuildId == criteria.GuildId.Value);
        }

        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        await BroadcastProgressAsync(criteria.EntityType, deleted, totalCount, false, "Deleting moderation cases...");

        return deleted;
    }

    private async Task BroadcastProgressAsync(
        BulkPurgeEntityType entityType,
        int processedCount,
        int totalCount,
        bool isComplete,
        string? message = null)
    {
        try
        {
            var progress = BulkPurgeProgressDto.Create(entityType, processedCount, totalCount, isComplete, message);

            await _hubContext.Clients
                .Group(DashboardHub.BulkPurgeGroupName)
                .SendAsync("BulkPurgeProgress", progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast bulk purge progress");
        }
    }
}
