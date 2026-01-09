using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing audit log operations including querying, retrieval, and logging.
/// Provides a fluent builder API for convenient audit log creation.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly IAuditLogQueue _queue;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="repository">The audit log repository.</param>
    /// <param name="queue">The audit log queue for background processing.</param>
    /// <param name="userManager">The ASP.NET Identity user manager.</param>
    /// <param name="discordClient">The Discord socket client for looking up guild names.</param>
    /// <param name="logger">The logger.</param>
    public AuditLogService(
        IAuditLogRepository repository,
        IAuditLogQueue queue,
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient,
        ILogger<AuditLogService> logger)
    {
        _repository = repository;
        _queue = queue;
        _userManager = userManager;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "get_logs",
            guildId: query.GuildId,
            userId: query.ActorId != null ? ulong.TryParse(query.ActorId, out var actorId) ? actorId : null : null);

        try
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

            // Enrich DTOs with user display names and guild names
            await EnrichDtosAsync(dtos, cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} of {TotalCount} audit logs (Page {Page}/{TotalPages})",
                dtos.Count, totalCount, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize));

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return (dtos.AsReadOnly(), totalCount);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "get_by_id",
            entityId: id.ToString());

        try
        {
            _logger.LogDebug("Retrieving audit log with ID {Id}", id);

            var log = await _repository.GetByIdAsync(id, cancellationToken);

            if (log is null)
            {
                _logger.LogWarning("Audit log with ID {Id} not found", id);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            _logger.LogDebug(
                "Retrieved audit log {Id}: {Category}.{Action} by {ActorType} {ActorId}",
                id, log.Category, log.Action, log.ActorType, log.ActorId);

            var dto = MapToDto(log);
            await EnrichDtosAsync(new List<AuditLogDto> { dto }, cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return dto;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLogDto>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "get_by_correlation_id");

        try
        {
            _logger.LogDebug("Retrieving audit logs with correlation ID {CorrelationId}", correlationId);

            var logs = await _repository.GetByCorrelationIdAsync(correlationId, cancellationToken);

            var dtos = logs.Select(MapToDto).ToList();

            // Enrich DTOs with user display names and guild names
            await EnrichDtosAsync(dtos, cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} audit logs for correlation ID {CorrelationId}",
                dtos.Count, correlationId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos.AsReadOnly();
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AuditLogStatsDto> GetStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "get_stats",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving audit log statistics for guildId: {GuildId}", guildId);

            var stats = await _repository.GetStatsAsync(guildId, cancellationToken);

            _logger.LogInformation(
                "Retrieved audit log statistics - Total: {Total}, Last24h: {Last24h}, Last7d: {Last7d}, Last30d: {Last30d}",
                stats.TotalEntries, stats.Last24Hours, stats.Last7Days, stats.Last30Days);

            BotActivitySource.SetSuccess(activity);
            return stats;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task LogAsync(AuditLogCreateDto dto, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "log",
            guildId: dto.GuildId);

        try
        {
            _logger.LogTrace(
                "Enqueuing audit log entry: {Category}.{Action} by {ActorType} {ActorId}",
                dto.Category, dto.Action, dto.ActorType, dto.ActorId);

            // Enqueue for background processing (fire-and-forget)
            _queue.Enqueue(dto);

            BotActivitySource.SetSuccess(activity);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public IAuditLogBuilder CreateBuilder()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audit_log",
            "create_builder");

        try
        {
            var builder = new AuditLogBuilder(this, _queue, _logger);
            BotActivitySource.SetSuccess(activity);
            return builder;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
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
            ActorDisplayName = null, // Will be enriched by EnrichDtosAsync for User actors
            TargetType = entity.TargetType,
            TargetId = entity.TargetId,
            GuildId = entity.GuildId,
            GuildName = null, // Will be enriched by EnrichDtosAsync
            Details = entity.Details,
            IpAddress = entity.IpAddress,
            CorrelationId = entity.CorrelationId
        };
    }

    /// <summary>
    /// Enriches DTOs with user display names and guild names by batch-looking up
    /// users and guilds referenced in the audit logs.
    /// </summary>
    /// <param name="dtos">The DTOs to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnrichDtosAsync(List<AuditLogDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        // Collect unique user IDs from User-type actors
        var userActorIds = dtos
            .Where(d => d.ActorType == AuditLogActorType.User && !string.IsNullOrEmpty(d.ActorId))
            .Select(d => d.ActorId!)
            .Distinct()
            .ToList();

        // Collect unique guild IDs
        var guildIds = dtos
            .Where(d => d.GuildId.HasValue)
            .Select(d => d.GuildId!.Value)
            .Distinct()
            .ToList();

        // Batch lookup users
        var userDisplayNames = new Dictionary<string, string>();
        if (userActorIds.Count > 0)
        {
            try
            {
                var users = await _userManager.Users
                    .Where(u => userActorIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.DisplayName, u.Email, u.DiscordUsername })
                    .ToListAsync(cancellationToken);

                foreach (var user in users)
                {
                    // Prefer DisplayName, then DiscordUsername, then Email (before @)
                    var displayName = user.DisplayName
                        ?? user.DiscordUsername
                        ?? user.Email?.Split('@')[0]
                        ?? user.Id;

                    userDisplayNames[user.Id] = displayName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to lookup user display names for audit logs");
            }
        }

        // Lookup guild names from Discord client (synchronous - no DB call needed)
        var guildNames = new Dictionary<ulong, string>();
        foreach (var guildId in guildIds)
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild != null)
            {
                guildNames[guildId] = guild.Name;
            }
        }

        // Apply enriched data to DTOs
        foreach (var dto in dtos)
        {
            // Set actor display name
            if (dto.ActorType == AuditLogActorType.User && !string.IsNullOrEmpty(dto.ActorId))
            {
                dto.ActorDisplayName = userDisplayNames.TryGetValue(dto.ActorId, out var displayName)
                    ? displayName
                    : null; // Leave null if user not found - view will handle GUID display
            }
            else if (dto.ActorType == AuditLogActorType.System)
            {
                dto.ActorDisplayName = "System";
            }
            else if (dto.ActorType == AuditLogActorType.Bot)
            {
                dto.ActorDisplayName = "Bot";
            }

            // Set guild name
            if (dto.GuildId.HasValue && guildNames.TryGetValue(dto.GuildId.Value, out var guildName))
            {
                dto.GuildName = guildName;
            }
        }
    }
}
