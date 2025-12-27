using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing audit log operations including querying, retrieval, and logging.
/// Provides a fluent builder API for convenient audit log creation.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly IAuditLogQueue _queue;
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="repository">The audit log repository.</param>
    /// <param name="queue">The audit log queue for background processing.</param>
    /// <param name="logger">The logger.</param>
    public AuditLogService(
        IAuditLogRepository repository,
        IAuditLogQueue queue,
        ILogger<AuditLogService> logger)
    {
        _repository = repository;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Querying audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, GuildId={GuildId}, Page={Page}, PageSize={PageSize}",
            query.Category, query.Action, query.ActorId, query.GuildId, query.Page, query.PageSize);

        // Validate pagination parameters
        if (query.Page < 1)
        {
            query.Page = 1;
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            query.PageSize = 20;
        }

        // Execute repository query
        var (items, totalCount) = await _repository.GetLogsAsync(query, cancellationToken);

        // Map entities to DTOs
        var dtos = items.Select(MapToDto).ToList();

        _logger.LogInformation(
            "Retrieved {Count} of {TotalCount} audit logs (Page {Page}/{TotalPages})",
            dtos.Count, totalCount, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize));

        return (dtos.AsReadOnly(), totalCount);
    }

    /// <inheritdoc/>
    public async Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audit log with ID {Id}", id);

        var log = await _repository.GetByIdAsync(id, cancellationToken);

        if (log is null)
        {
            _logger.LogWarning("Audit log with ID {Id} not found", id);
            return null;
        }

        _logger.LogDebug(
            "Retrieved audit log {Id}: {Category}.{Action} by {ActorType} {ActorId}",
            id, log.Category, log.Action, log.ActorType, log.ActorId);

        return MapToDto(log);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLogDto>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audit logs with correlation ID {CorrelationId}", correlationId);

        var logs = await _repository.GetByCorrelationIdAsync(correlationId, cancellationToken);

        var dtos = logs.Select(MapToDto).ToList();

        _logger.LogInformation(
            "Retrieved {Count} audit logs for correlation ID {CorrelationId}",
            dtos.Count, correlationId);

        return dtos.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<AuditLogStatsDto> GetStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audit log statistics for guildId: {GuildId}", guildId);

        var stats = await _repository.GetStatsAsync(guildId, cancellationToken);

        _logger.LogInformation(
            "Retrieved audit log statistics - Total: {Total}, Last24h: {Last24h}, Last7d: {Last7d}, Last30d: {Last30d}",
            stats.TotalEntries, stats.Last24Hours, stats.Last7Days, stats.Last30Days);

        return stats;
    }

    /// <inheritdoc/>
    public Task LogAsync(AuditLogCreateDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace(
            "Enqueuing audit log entry: {Category}.{Action} by {ActorType} {ActorId}",
            dto.Category, dto.Action, dto.ActorType, dto.ActorId);

        // Enqueue for background processing (fire-and-forget)
        _queue.Enqueue(dto);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder CreateBuilder()
    {
        return new AuditLogBuilder(this, _queue, _logger);
    }

    /// <summary>
    /// Maps an AuditLog entity to an AuditLogDto.
    /// </summary>
    /// <param name="entity">The audit log entity.</param>
    /// <returns>The mapped AuditLogDto.</returns>
    private static AuditLogDto MapToDto(AuditLog entity)
    {
        return new AuditLogDto
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            Category = entity.Category,
            CategoryName = entity.Category.ToString(),
            Action = entity.Action,
            ActionName = entity.Action.ToString(),
            ActorId = entity.ActorId,
            ActorType = entity.ActorType,
            ActorTypeName = entity.ActorType.ToString(),
            ActorDisplayName = entity.ActorId, // Default to ActorId; could be enriched with actual display name
            TargetType = entity.TargetType,
            TargetId = entity.TargetId,
            GuildId = entity.GuildId,
            GuildName = null, // Not available from entity; could be enriched from Guild navigation property
            Details = entity.Details,
            IpAddress = entity.IpAddress,
            CorrelationId = entity.CorrelationId
        };
    }
}
