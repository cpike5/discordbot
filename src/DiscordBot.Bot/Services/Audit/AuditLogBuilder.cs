using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Fluent builder implementation for constructing audit log entries.
/// Provides a chainable API for creating audit logs with improved readability.
/// </summary>
public class AuditLogBuilder : IAuditLogBuilder
{
    private readonly AuditLogService _service;
    private readonly IAuditLogQueue _queue;
    private readonly ILogger<AuditLogService> _logger;

    private AuditLogCategory _category;
    private AuditLogAction _action;
    private string? _actorId;
    private AuditLogActorType _actorType;
    private string? _targetType;
    private string? _targetId;
    private ulong? _guildId;
    private string? _details;
    private string? _ipAddress;
    private string? _correlationId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogBuilder"/> class.
    /// </summary>
    /// <param name="service">The audit log service.</param>
    /// <param name="queue">The audit log queue.</param>
    /// <param name="logger">The logger.</param>
    internal AuditLogBuilder(
        AuditLogService service,
        IAuditLogQueue queue,
        ILogger<AuditLogService> logger)
    {
        _service = service;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder ForCategory(AuditLogCategory category)
    {
        _category = category;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder WithAction(AuditLogAction action)
    {
        _action = action;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder ByUser(string userId)
    {
        _actorId = userId;
        _actorType = AuditLogActorType.User;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder BySystem()
    {
        _actorId = "System";
        _actorType = AuditLogActorType.System;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder ByBot()
    {
        _actorId = "Bot";
        _actorType = AuditLogActorType.Bot;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder OnTarget(string targetType, string targetId)
    {
        _targetType = targetType;
        _targetId = targetId;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder InGuild(ulong guildId)
    {
        _guildId = guildId;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder WithDetails(Dictionary<string, object?> details)
    {
        _details = JsonSerializer.Serialize(details, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder WithDetails(object details)
    {
        _details = JsonSerializer.Serialize(details, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder FromIpAddress(string ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    /// <inheritdoc/>
    public IAuditLogBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <inheritdoc/>
    public async Task LogAsync(CancellationToken cancellationToken = default)
    {
        var dto = BuildDto();

        _logger.LogDebug(
            "Logging audit entry: {Category}.{Action} by {ActorType} {ActorId}",
            dto.Category, dto.Action, dto.ActorType, dto.ActorId);

        await _service.LogAsync(dto, cancellationToken);
    }

    /// <inheritdoc/>
    public void Enqueue()
    {
        var dto = BuildDto();

        _logger.LogTrace(
            "Enqueuing audit entry: {Category}.{Action} by {ActorType} {ActorId}",
            dto.Category, dto.Action, dto.ActorType, dto.ActorId);

        _queue.Enqueue(dto);
    }

    /// <summary>
    /// Builds the AuditLogCreateDto from the current builder state.
    /// </summary>
    /// <returns>The constructed AuditLogCreateDto.</returns>
    private AuditLogCreateDto BuildDto()
    {
        return new AuditLogCreateDto
        {
            Category = _category,
            Action = _action,
            ActorId = _actorId,
            ActorType = _actorType,
            TargetType = _targetType,
            TargetId = _targetId,
            GuildId = _guildId,
            Details = _details,
            IpAddress = _ipAddress,
            CorrelationId = _correlationId
        };
    }
}
