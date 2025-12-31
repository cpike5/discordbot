using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing flagged events from auto-moderation detection.
/// Handles event creation, review, dismissal, acknowledgement, and action tracking.
/// </summary>
public class FlaggedEventService : IFlaggedEventService
{
    private readonly IFlaggedEventRepository _eventRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<FlaggedEventService> _logger;

    public FlaggedEventService(
        IFlaggedEventRepository eventRepository,
        DiscordSocketClient client,
        ILogger<FlaggedEventService> logger)
    {
        _eventRepository = eventRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FlaggedEventDto> CreateEventAsync(ulong guildId, ulong userId, ulong? channelId, RuleType ruleType, Severity severity, string description, string evidence, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating flagged event for user {UserId} in guild {GuildId}, rule: {RuleType}, severity: {Severity}",
            userId, guildId, ruleType, severity);

        var flaggedEvent = new FlaggedEvent
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            ChannelId = channelId ?? 0,
            RuleType = ruleType,
            Severity = severity,
            Description = description,
            Evidence = evidence,
            Status = FlaggedEventStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _eventRepository.AddAsync(flaggedEvent, ct);

        _logger.LogInformation("Flagged event {EventId} created successfully for user {UserId} in guild {GuildId}",
            flaggedEvent.Id, userId, guildId);

        return await MapToDtoAsync(flaggedEvent, ct);
    }

    /// <inheritdoc/>
    public async Task<FlaggedEventDto?> GetEventAsync(Guid eventId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving flagged event {EventId}", eventId);

        var flaggedEvent = await _eventRepository.GetByIdAsync(eventId, ct);
        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found", eventId);
            return null;
        }

        return await MapToDtoAsync(flaggedEvent, ct);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<FlaggedEventDto> Items, int TotalCount)> GetPendingEventsAsync(ulong guildId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving pending flagged events for guild {GuildId}, page {Page}, size {PageSize}",
            guildId, page, pageSize);

        var (events, totalCount) = await _eventRepository.GetPendingEventsAsync(guildId, null, null, null, page, pageSize, ct);
        var eventsList = events.ToList();

        var dtos = new List<FlaggedEventDto>();
        foreach (var flaggedEvent in eventsList)
        {
            dtos.Add(await MapToDtoAsync(flaggedEvent, ct));
        }

        _logger.LogDebug("Retrieved {Count} pending flagged events out of {TotalCount} for guild {GuildId}",
            dtos.Count, totalCount, guildId);

        return (dtos, totalCount);
    }

    /// <inheritdoc/>
    public async Task<FlaggedEventDto?> DismissEventAsync(Guid eventId, ulong reviewerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Dismissing flagged event {EventId} by moderator {ReviewerId}", eventId, reviewerId);

        var flaggedEvent = await _eventRepository.GetByIdAsync(eventId, ct);
        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found", eventId);
            return null;
        }

        flaggedEvent.Status = FlaggedEventStatus.Dismissed;
        flaggedEvent.ReviewedByUserId = reviewerId;
        flaggedEvent.ReviewedAt = DateTime.UtcNow;

        await _eventRepository.UpdateAsync(flaggedEvent, ct);

        _logger.LogInformation("Flagged event {EventId} dismissed successfully by moderator {ReviewerId}",
            eventId, reviewerId);

        return await MapToDtoAsync(flaggedEvent, ct);
    }

    /// <inheritdoc/>
    public async Task<FlaggedEventDto?> AcknowledgeEventAsync(Guid eventId, ulong reviewerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Acknowledging flagged event {EventId} by moderator {ReviewerId}", eventId, reviewerId);

        var flaggedEvent = await _eventRepository.GetByIdAsync(eventId, ct);
        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found", eventId);
            return null;
        }

        flaggedEvent.Status = FlaggedEventStatus.Acknowledged;
        flaggedEvent.ReviewedByUserId = reviewerId;
        flaggedEvent.ReviewedAt = DateTime.UtcNow;

        await _eventRepository.UpdateAsync(flaggedEvent, ct);

        _logger.LogInformation("Flagged event {EventId} acknowledged successfully by moderator {ReviewerId}",
            eventId, reviewerId);

        return await MapToDtoAsync(flaggedEvent, ct);
    }

    /// <inheritdoc/>
    public async Task<FlaggedEventDto?> TakeActionAsync(Guid eventId, string action, ulong reviewerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Taking action on flagged event {EventId} by moderator {ReviewerId}: {Action}",
            eventId, reviewerId, action);

        var flaggedEvent = await _eventRepository.GetByIdAsync(eventId, ct);
        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found", eventId);
            return null;
        }

        flaggedEvent.Status = FlaggedEventStatus.Actioned;
        flaggedEvent.ActionTaken = action;
        flaggedEvent.ReviewedByUserId = reviewerId;
        flaggedEvent.ReviewedAt = DateTime.UtcNow;

        await _eventRepository.UpdateAsync(flaggedEvent, ct);

        _logger.LogInformation("Flagged event {EventId} marked as actioned by moderator {ReviewerId}",
            eventId, reviewerId);

        return await MapToDtoAsync(flaggedEvent, ct);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FlaggedEventDto>> GetUserEventsAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving flagged events for user {UserId} in guild {GuildId}", userId, guildId);

        var (events, _) = await _eventRepository.GetByUserAsync(guildId, userId, 1, int.MaxValue, ct);
        var eventsList = events.ToList();

        var dtos = new List<FlaggedEventDto>();
        foreach (var flaggedEvent in eventsList)
        {
            dtos.Add(await MapToDtoAsync(flaggedEvent, ct));
        }

        _logger.LogDebug("Retrieved {Count} flagged events for user {UserId} in guild {GuildId}",
            dtos.Count, userId, guildId);

        return dtos;
    }

    /// <summary>
    /// Maps a FlaggedEvent entity to a DTO with resolved usernames and channel names.
    /// </summary>
    private async Task<FlaggedEventDto> MapToDtoAsync(FlaggedEvent flaggedEvent, CancellationToken ct = default)
    {
        var username = await GetUsernameAsync(flaggedEvent.UserId);
        var channelName = flaggedEvent.ChannelId.HasValue && flaggedEvent.ChannelId.Value != 0
            ? GetChannelName(flaggedEvent.GuildId, flaggedEvent.ChannelId.Value)
            : "Unknown";

        string? reviewedByUsername = null;
        if (flaggedEvent.ReviewedByUserId.HasValue)
        {
            reviewedByUsername = await GetUsernameAsync(flaggedEvent.ReviewedByUserId.Value);
        }

        return new FlaggedEventDto
        {
            Id = flaggedEvent.Id,
            GuildId = flaggedEvent.GuildId,
            UserId = flaggedEvent.UserId,
            Username = username,
            ChannelId = flaggedEvent.ChannelId ?? 0,
            ChannelName = channelName,
            RuleType = flaggedEvent.RuleType,
            Severity = flaggedEvent.Severity,
            Description = flaggedEvent.Description,
            Evidence = flaggedEvent.Evidence,
            Status = flaggedEvent.Status,
            ActionTaken = flaggedEvent.ActionTaken,
            ReviewedByUserId = flaggedEvent.ReviewedByUserId,
            ReviewedByUsername = reviewedByUsername,
            CreatedAt = flaggedEvent.CreatedAt,
            ReviewedAt = flaggedEvent.ReviewedAt
        };
    }

    /// <summary>
    /// Resolves a Discord user ID to username.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? $"Unknown#{userId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return $"Unknown#{userId}";
        }
    }

    /// <summary>
    /// Resolves a Discord channel ID to channel name.
    /// </summary>
    private string GetChannelName(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving channel {ChannelId}", guildId, channelId);
                return $"Unknown#{channelId}";
            }

            var channel = guild.GetChannel(channelId);
            return channel?.Name ?? $"Unknown#{channelId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve channel name for channel {ChannelId} in guild {GuildId}",
                channelId, guildId);
            return $"Unknown#{channelId}";
        }
    }
}
