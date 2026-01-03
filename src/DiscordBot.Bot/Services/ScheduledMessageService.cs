using Cronos;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for scheduled message operations and execution management.
/// </summary>
public class ScheduledMessageService : IScheduledMessageService
{
    private readonly IScheduledMessageRepository _repository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ScheduledMessageService> _logger;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledMessageService"/> class.
    /// </summary>
    /// <param name="repository">The scheduled message repository.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="auditLogService">The audit log service.</param>
    public ScheduledMessageService(
        IScheduledMessageRepository repository,
        DiscordSocketClient client,
        ILogger<ScheduledMessageService> logger,
        IAuditLogService auditLogService)
    {
        _repository = repository;
        _client = client;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc/>
    public async Task<ScheduledMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "get_by_id",
            entityId: id.ToString());

        try
        {
            _logger.LogDebug("Retrieving scheduled message {MessageId}", id);

            var message = await _repository.GetByIdWithGuildAsync(id, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found", id);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var dto = MapToDto(message);
            _logger.LogDebug("Retrieved scheduled message {MessageId}: {Title}", id, dto.Title);

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
    public async Task<(IEnumerable<ScheduledMessageDto> Items, int TotalCount)> GetByGuildIdAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "get_by_guild",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving scheduled messages for guild {GuildId}, page {Page}, pageSize {PageSize}",
                guildId, page, pageSize);

            var (messages, totalCount) = await _repository.GetByGuildIdAsync(guildId, page, pageSize, cancellationToken);
            var dtos = messages.Select(MapToDto);

            _logger.LogInformation("Retrieved {Count} of {Total} scheduled messages for guild {GuildId}",
                messages.Count(), totalCount, guildId);

            BotActivitySource.SetRecordsReturned(activity, messages.Count());
            BotActivitySource.SetSuccess(activity);
            return (dtos, totalCount);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ScheduledMessageDto> CreateAsync(ScheduledMessageCreateDto dto, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "create",
            guildId: dto.GuildId);

        try
        {
            _logger.LogInformation("Creating scheduled message for guild {GuildId}, channel {ChannelId}: {Title}",
                dto.GuildId, dto.ChannelId, dto.Title);

            // Validate cron expression if frequency is Custom
            if (dto.Frequency == ScheduleFrequency.Custom)
            {
                if (string.IsNullOrWhiteSpace(dto.CronExpression))
                {
                    var error = "Cron expression is required when frequency is Custom";
                    _logger.LogWarning(error);
                    throw new ArgumentException(error, nameof(dto));
                }

                var (isValid, errorMessage) = await ValidateCronExpressionAsync(dto.CronExpression);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid cron expression: {Error}", errorMessage);
                    throw new ArgumentException(errorMessage, nameof(dto));
                }
            }

            var now = DateTime.UtcNow;
            var message = new ScheduledMessage
            {
                Id = Guid.NewGuid(),
                GuildId = dto.GuildId,
                ChannelId = dto.ChannelId,
                Title = dto.Title,
                Content = dto.Content,
                CronExpression = dto.CronExpression,
                Frequency = dto.Frequency,
                IsEnabled = dto.IsEnabled,
                NextExecutionAt = dto.NextExecutionAt,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = dto.CreatedBy
            };

            await _repository.AddAsync(message, cancellationToken);

            // Audit log
            try
            {
                var builder = _auditLogService.CreateBuilder()
                    .ForCategory(AuditLogCategory.Message)
                    .WithAction(AuditLogAction.Created)
                    .InGuild(dto.GuildId)
                    .OnTarget("ScheduledMessage", message.Id.ToString())
                    .WithDetails(new
                    {
                        title = dto.Title,
                        channelId = dto.ChannelId,
                        frequency = dto.Frequency.ToString(),
                        isEnabled = dto.IsEnabled,
                        guildId = dto.GuildId
                    });

                // Use ByUser if CreatedBy is provided, otherwise BySystem
                if (!string.IsNullOrEmpty(dto.CreatedBy))
                {
                    builder.ByUser(dto.CreatedBy);
                }
                else
                {
                    builder.BySystem();
                }

                builder.Enqueue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for scheduled message creation {MessageId}", message.Id);
            }

            _logger.LogInformation("Scheduled message {MessageId} created successfully for guild {GuildId}",
                message.Id, dto.GuildId);

            BotActivitySource.SetSuccess(activity);
            return MapToDto(message);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ScheduledMessageDto?> UpdateAsync(Guid id, ScheduledMessageUpdateDto dto, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "update",
            entityId: id.ToString());

        try
        {
            _logger.LogInformation("Updating scheduled message {MessageId}", id);

            var message = await _repository.GetByIdAsync(id, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found for update", id);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            // Apply updates only for non-null fields
            if (dto.ChannelId.HasValue)
            {
                message.ChannelId = dto.ChannelId.Value;
            }

            if (dto.Title != null)
            {
                message.Title = dto.Title;
            }

            if (dto.Content != null)
            {
                message.Content = dto.Content;
            }

            if (dto.Frequency.HasValue)
            {
                message.Frequency = dto.Frequency.Value;

                // Validate cron expression if frequency is changed to Custom
                if (dto.Frequency.Value == ScheduleFrequency.Custom)
                {
                    var cronExpr = dto.CronExpression ?? message.CronExpression;
                    if (string.IsNullOrWhiteSpace(cronExpr))
                    {
                        var error = "Cron expression is required when frequency is Custom";
                        _logger.LogWarning(error);
                        throw new ArgumentException(error, nameof(dto));
                    }

                    var (isValid, errorMessage) = await ValidateCronExpressionAsync(cronExpr);
                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid cron expression: {Error}", errorMessage);
                        throw new ArgumentException(errorMessage, nameof(dto));
                    }
                }
            }

            if (dto.CronExpression != null)
            {
                // Validate if frequency is Custom
                if (message.Frequency == ScheduleFrequency.Custom)
                {
                    var (isValid, errorMessage) = await ValidateCronExpressionAsync(dto.CronExpression);
                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid cron expression: {Error}", errorMessage);
                        throw new ArgumentException(errorMessage, nameof(dto));
                    }
                }

                message.CronExpression = dto.CronExpression;
            }

            if (dto.IsEnabled.HasValue)
            {
                message.IsEnabled = dto.IsEnabled.Value;
            }

            if (dto.NextExecutionAt.HasValue)
            {
                message.NextExecutionAt = dto.NextExecutionAt.Value;
            }

            message.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(message, cancellationToken);

            // Audit log
            try
            {
                _auditLogService.CreateBuilder()
                    .ForCategory(AuditLogCategory.Message)
                    .WithAction(AuditLogAction.Updated)
                    .BySystem()
                    .InGuild(message.GuildId)
                    .OnTarget("ScheduledMessage", id.ToString())
                    .WithDetails(new
                    {
                        title = message.Title,
                        channelId = message.ChannelId,
                        frequency = message.Frequency.ToString(),
                        isEnabled = message.IsEnabled,
                        guildId = message.GuildId
                    })
                    .Enqueue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for scheduled message update {MessageId}", id);
            }

            _logger.LogInformation("Scheduled message {MessageId} updated successfully", id);

            BotActivitySource.SetSuccess(activity);
            return MapToDto(message);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "delete",
            entityId: id.ToString());

        try
        {
            _logger.LogInformation("Deleting scheduled message {MessageId}", id);

            var message = await _repository.GetByIdAsync(id, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found for deletion", id);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            // Capture details before deletion
            var guildId = message.GuildId;
            var title = message.Title;

            await _repository.DeleteAsync(message, cancellationToken);

            // Audit log
            try
            {
                _auditLogService.CreateBuilder()
                    .ForCategory(AuditLogCategory.Message)
                    .WithAction(AuditLogAction.Deleted)
                    .BySystem()
                    .InGuild(guildId)
                    .OnTarget("ScheduledMessage", id.ToString())
                    .WithDetails(new { title, guildId })
                    .Enqueue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for scheduled message deletion {MessageId}", id);
            }

            _logger.LogInformation("Scheduled message {MessageId} deleted successfully", id);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<DateTime?> CalculateNextExecutionAsync(
        ScheduleFrequency frequency,
        string? cronExpression,
        DateTime? baseTime = null)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "calculate_next_execution");

        try
        {
            var now = baseTime ?? DateTime.UtcNow;

            var nextExecution = frequency switch
            {
                ScheduleFrequency.Once => null,
                ScheduleFrequency.Hourly => now.AddHours(1),
                ScheduleFrequency.Daily => now.AddDays(1),
                ScheduleFrequency.Weekly => now.AddDays(7),
                ScheduleFrequency.Monthly => now.AddMonths(1),
                ScheduleFrequency.Custom => CalculateNextFromCron(cronExpression, now),
                _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Invalid schedule frequency")
            };

            _logger.LogDebug("Calculated next execution for {Frequency}: {NextExecution}",
                frequency, nextExecution);

            BotActivitySource.SetSuccess(activity);
            return Task.FromResult(nextExecution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate next execution for frequency {Frequency}", frequency);
            BotActivitySource.RecordException(activity, ex);
            return Task.FromResult<DateTime?>(null);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteScheduledMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "execute",
            entityId: id.ToString());

        try
        {
            _logger.LogInformation("Executing scheduled message {MessageId}", id);

            var message = await _repository.GetByIdAsync(id, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found for execution", id);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            try
            {
                // Get the Discord channel
                var channel = _client.GetChannel(message.ChannelId) as IMessageChannel;
                if (channel == null)
                {
                    _logger.LogError("Channel {ChannelId} not found for scheduled message {MessageId}",
                        message.ChannelId, id);
                    BotActivitySource.SetSuccess(activity);
                    return false;
                }

                // Store the original scheduled time BEFORE updating it
                // This is used to calculate the next execution time to prevent drift
                var originalScheduledTime = message.NextExecutionAt;

                // Send the message to Discord
                await channel.SendMessageAsync(message.Content);

                _logger.LogInformation("Scheduled message {MessageId} sent successfully to channel {ChannelId}",
                    id, message.ChannelId);

                // Audit log
                try
                {
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.Message)
                        .WithAction(AuditLogAction.CommandExecuted)
                        .BySystem()
                        .InGuild(message.GuildId)
                        .OnTarget("ScheduledMessage", id.ToString())
                        .WithDetails(new
                        {
                            title = message.Title,
                            channelId = message.ChannelId,
                            guildId = message.GuildId,
                            action = "message_executed"
                        })
                        .Enqueue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log audit entry for scheduled message execution {MessageId}", id);
                }

                // Update execution state
                message.LastExecutedAt = DateTime.UtcNow;

                // Calculate next execution time or disable if OneTime
                if (message.Frequency == ScheduleFrequency.Once)
                {
                    message.IsEnabled = false;
                    message.NextExecutionAt = null;
                    _logger.LogInformation("Scheduled message {MessageId} disabled after one-time execution", id);
                }
                else
                {
                    // Use the ORIGINAL scheduled time as the base for calculating the next execution,
                    // NOT the actual execution time. This prevents time drift when execution is
                    // slightly early or late (e.g., scheduled for 10:58 but runs at 10:56).
                    var baseTime = originalScheduledTime ?? message.LastExecutedAt;

                    var nextExecution = await CalculateNextExecutionAsync(
                        message.Frequency,
                        message.CronExpression,
                        baseTime);

                    // If the calculated next time is in the past (e.g., we missed executions),
                    // keep adding intervals until we get a future time
                    while (nextExecution.HasValue && nextExecution.Value <= DateTime.UtcNow)
                    {
                        nextExecution = await CalculateNextExecutionAsync(
                            message.Frequency,
                            message.CronExpression,
                            nextExecution.Value);
                    }

                    if (nextExecution.HasValue)
                    {
                        message.NextExecutionAt = nextExecution.Value;
                        _logger.LogDebug("Next execution for message {MessageId} scheduled at {NextExecution}",
                            id, nextExecution.Value);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to calculate next execution for message {MessageId}, disabling", id);
                        message.IsEnabled = false;
                        message.NextExecutionAt = null;
                    }
                }

                message.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(message, cancellationToken);

                BotActivitySource.SetSuccess(activity);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scheduled message {MessageId}", id);
                BotActivitySource.RecordException(activity, ex);
                return false;
            }
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<(bool IsValid, string? ErrorMessage)> ValidateCronExpressionAsync(string cronExpression)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "scheduled_message",
            "validate_cron_expression");

        try
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                BotActivitySource.SetSuccess(activity);
                return Task.FromResult<(bool, string?)>((false, "Cron expression cannot be empty"));
            }

            try
            {
                // Try to parse the cron expression
                var expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);

                // Try to get next occurrence to verify it's valid
                var next = expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
                if (!next.HasValue)
                {
                    BotActivitySource.SetSuccess(activity);
                    return Task.FromResult<(bool, string?)>((false, "Cron expression has no future occurrences"));
                }

                _logger.LogDebug("Cron expression validated: {CronExpression}, next occurrence: {Next}",
                    cronExpression, next.Value);

                BotActivitySource.SetSuccess(activity);
                return Task.FromResult<(bool, string?)>((true, null));
            }
            catch (CronFormatException ex)
            {
                _logger.LogWarning(ex, "Invalid cron expression: {CronExpression}", cronExpression);
                BotActivitySource.SetSuccess(activity);
                return Task.FromResult<(bool, string?)>((false, $"Invalid cron expression format: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating cron expression: {CronExpression}", cronExpression);
                BotActivitySource.RecordException(activity, ex);
                return Task.FromResult<(bool, string?)>((false, $"Error validating cron expression: {ex.Message}"));
            }
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Calculates the next execution time from a cron expression.
    /// </summary>
    /// <param name="cronExpression">The cron expression.</param>
    /// <param name="baseTime">The base time to calculate from.</param>
    /// <returns>The next execution time, or null if calculation fails.</returns>
    private DateTime? CalculateNextFromCron(string? cronExpression, DateTime baseTime)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            _logger.LogWarning("Cron expression is null or empty");
            return null;
        }

        try
        {
            var expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            var next = expression.GetNextOccurrence(baseTime, TimeZoneInfo.Utc);

            if (!next.HasValue)
            {
                _logger.LogWarning("Cron expression {CronExpression} has no next occurrence", cronExpression);
                return null;
            }

            return next.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse cron expression: {CronExpression}", cronExpression);
            return null;
        }
    }

    /// <summary>
    /// Maps a ScheduledMessage entity to a ScheduledMessageDto.
    /// </summary>
    /// <param name="message">The scheduled message entity.</param>
    /// <returns>The mapped ScheduledMessageDto.</returns>
    private static ScheduledMessageDto MapToDto(ScheduledMessage message)
    {
        return new ScheduledMessageDto
        {
            Id = message.Id,
            GuildId = message.GuildId,
            GuildName = message.Guild?.Name,
            ChannelId = message.ChannelId,
            Title = message.Title,
            Content = message.Content,
            CronExpression = message.CronExpression,
            Frequency = message.Frequency,
            IsEnabled = message.IsEnabled,
            LastExecutedAt = message.LastExecutedAt,
            NextExecutionAt = message.NextExecutionAt,
            CreatedAt = message.CreatedAt,
            CreatedBy = message.CreatedBy,
            UpdatedAt = message.UpdatedAt
        };
    }
}
