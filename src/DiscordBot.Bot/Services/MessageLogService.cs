using System.Text;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for message log retrieval, statistics, and data management.
/// </summary>
public class MessageLogService : IMessageLogService
{
    private readonly IMessageLogRepository _repository;
    private readonly IOptions<MessageLogRetentionOptions> _retentionOptions;
    private readonly ILogger<MessageLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageLogService"/> class.
    /// </summary>
    /// <param name="repository">The message log repository.</param>
    /// <param name="retentionOptions">The retention policy configuration options.</param>
    /// <param name="logger">The logger.</param>
    public MessageLogService(
        IMessageLogRepository repository,
        IOptions<MessageLogRetentionOptions> retentionOptions,
        ILogger<MessageLogService> logger)
    {
        _repository = repository;
        _retentionOptions = retentionOptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<MessageLogDto>> GetLogsAsync(MessageLogQueryDto query, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "get_logs",
            guildId: query.GuildId,
            userId: query.AuthorId);

        try
        {
            _logger.LogDebug(
                "Querying message logs with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}, Source={Source}, SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
                query.AuthorId, query.GuildId, query.ChannelId, query.Source, query.SearchTerm, query.Page, query.PageSize);

            // Validate pagination parameters
            if (query.Page < 1)
            {
                query.Page = 1;
            }

            if (query.PageSize < 1 || query.PageSize > 100)
            {
                query.PageSize = 25;
            }

            // Execute repository query
            var (items, totalCount) = await _repository.GetPaginatedAsync(query, cancellationToken);

            // Map entities to DTOs
            var dtos = items.Select(MapToDto).ToList();

            _logger.LogInformation(
                "Retrieved {Count} of {TotalCount} message logs (Page {Page}/{TotalPages})",
                dtos.Count, totalCount, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize));

            var result = new PaginatedResponseDto<MessageLogDto>
            {
                Items = dtos.AsReadOnly(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MessageLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "get_by_id");

        try
        {
            _logger.LogDebug("Retrieving message log with ID {Id}", id);

            var log = await _repository.GetByIdAsync(id, cancellationToken);

            if (log is null)
            {
                _logger.LogWarning("Message log with ID {Id} not found", id);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            _logger.LogDebug("Retrieved message log {Id} for author {AuthorId}", id, log.AuthorId);

            var result = MapToDto(log);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MessageLogStatsDto> GetStatsAsync(ulong? guildId = null, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "get_stats",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving message statistics for guildId: {GuildId}", guildId);

            // Get basic statistics
            var (total, dmCount, serverCount, uniqueAuthors) = await _repository.GetBasicStatsAsync(guildId, cancellationToken);

            // Get trend data for last 7 days
            var messagesByDay = await _repository.GetMessagesByDayAsync(7, guildId, cancellationToken);
            var dailyCounts = messagesByDay
                .Select(x => new DailyMessageCount(x.Date, x.Count))
                .ToList();

            // Get oldest and newest message timestamps
            var oldestMessage = await _repository.GetOldestMessageDateAsync(cancellationToken);
            var newestMessage = await _repository.GetNewestMessageDateAsync(cancellationToken);

            var stats = new MessageLogStatsDto
            {
                TotalMessages = total,
                DmMessages = dmCount,
                ServerMessages = serverCount,
                UniqueAuthors = uniqueAuthors,
                MessagesByDay = dailyCounts,
                OldestMessage = oldestMessage,
                NewestMessage = newestMessage
            };

            _logger.LogInformation(
                "Retrieved message statistics - Total: {Total}, DM: {DmCount}, Server: {ServerCount}, UniqueAuthors: {UniqueAuthors}",
                total, dmCount, serverCount, uniqueAuthors);

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
    public async Task<int> DeleteUserMessagesAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "delete_user_messages",
            userId: userId);

        try
        {
            _logger.LogInformation("Deleting all messages for user {UserId} (GDPR compliance)", userId);

            var deletedCount = await _repository.DeleteByUserIdAsync(userId, cancellationToken);

            _logger.LogInformation(
                "Deleted {Count} messages for user {UserId} per GDPR data deletion request",
                deletedCount, userId);

            BotActivitySource.SetRecordsReturned(activity, deletedCount);
            BotActivitySource.SetSuccess(activity);
            return deletedCount;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOldMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "cleanup_old_messages");

        try
        {
            var options = _retentionOptions.Value;

            if (!options.Enabled)
            {
                _logger.LogDebug("Message log cleanup is disabled in configuration");
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-options.RetentionDays);

            _logger.LogInformation(
                "Starting message log cleanup: deleting messages older than {CutoffDate} (RetentionDays: {RetentionDays})",
                cutoffDate, options.RetentionDays);

            var totalDeleted = 0;
            var batchCount = 0;

            // Delete in batches to prevent long-running transactions
            while (!cancellationToken.IsCancellationRequested)
            {
                var batchDeleted = await _repository.DeleteBatchOlderThanAsync(
                    cutoffDate,
                    options.CleanupBatchSize,
                    cancellationToken);

                if (batchDeleted == 0)
                {
                    // No more records to delete
                    break;
                }

                totalDeleted += batchDeleted;
                batchCount++;

                _logger.LogDebug(
                    "Cleanup batch {BatchCount} deleted {BatchDeleted} messages (total: {TotalDeleted})",
                    batchCount, batchDeleted, totalDeleted);

                // If we deleted fewer than the batch size, we're done
                if (batchDeleted < options.CleanupBatchSize)
                {
                    break;
                }
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "Message log cleanup completed: deleted {TotalDeleted} messages in {BatchCount} batches",
                    totalDeleted, batchCount);
            }
            else
            {
                _logger.LogDebug("Message log cleanup completed: no old messages to delete");
            }

            BotActivitySource.SetRecordsReturned(activity, totalDeleted);
            BotActivitySource.SetSuccess(activity);
            return totalDeleted;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportToCsvAsync(MessageLogQueryDto query, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "message_log",
            "export_to_csv",
            guildId: query.GuildId,
            userId: query.AuthorId);

        try
        {
            _logger.LogInformation(
                "Exporting message logs to CSV with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}, Source={Source}",
                query.AuthorId, query.GuildId, query.ChannelId, query.Source);

            // Set large page size to get all matching records (or use chunked export for very large datasets)
            var exportQuery = new MessageLogQueryDto
            {
                AuthorId = query.AuthorId,
                GuildId = query.GuildId,
                ChannelId = query.ChannelId,
                Source = query.Source,
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                SearchTerm = query.SearchTerm,
                Page = 1,
                PageSize = 10000 // Export up to 10k records at once
            };

            var (items, totalCount) = await _repository.GetPaginatedAsync(exportQuery, cancellationToken);

            if (totalCount > 10000)
            {
                _logger.LogWarning(
                    "Export query matched {TotalCount} records, but only exporting first {PageSize}. Consider adding filters.",
                    totalCount, exportQuery.PageSize);
            }

            // Build CSV content
            var csv = new StringBuilder();

            // Write CSV header
            csv.AppendLine("Id,DiscordMessageId,AuthorId,ChannelId,GuildId,Source,Content,Timestamp,LoggedAt,HasAttachments,HasEmbeds,ReplyToMessageId");

            // Write data rows
            foreach (var message in items)
            {
                csv.AppendLine(FormatCsvRow(
                    message.Id,
                    message.DiscordMessageId,
                    message.AuthorId,
                    message.ChannelId,
                    message.GuildId?.ToString() ?? "",
                    message.Source.ToString(),
                    EscapeCsvField(message.Content),
                    message.Timestamp.ToString("O"),
                    message.LoggedAt.ToString("O"),
                    message.HasAttachments,
                    message.HasEmbeds,
                    message.ReplyToMessageId?.ToString() ?? ""
                ));
            }

            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());

            _logger.LogInformation(
                "Exported {Count} message logs to CSV ({Size} bytes)",
                items.Count(), csvBytes.Length);

            BotActivitySource.SetRecordsReturned(activity, items.Count());
            BotActivitySource.SetSuccess(activity);
            return csvBytes;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a MessageLog entity to a MessageLogDto.
    /// </summary>
    /// <param name="entity">The message log entity.</param>
    /// <returns>The mapped MessageLogDto.</returns>
    private static MessageLogDto MapToDto(MessageLog entity)
    {
        return new MessageLogDto
        {
            Id = entity.Id,
            DiscordMessageId = entity.DiscordMessageId,
            AuthorId = entity.AuthorId,
            AuthorUsername = entity.User?.Username, // From navigation property if loaded
            ChannelId = entity.ChannelId,
            ChannelName = null, // Not stored in database
            GuildId = entity.GuildId,
            GuildName = entity.Guild?.Name, // From navigation property if loaded
            Source = entity.Source,
            Content = entity.Content,
            Timestamp = entity.Timestamp,
            LoggedAt = entity.LoggedAt,
            HasAttachments = entity.HasAttachments,
            HasEmbeds = entity.HasEmbeds,
            ReplyToMessageId = entity.ReplyToMessageId
        };
    }

    /// <summary>
    /// Escapes a CSV field by wrapping it in quotes and escaping internal quotes.
    /// </summary>
    /// <param name="field">The field value to escape.</param>
    /// <returns>The escaped CSV field.</returns>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        // If the field contains quotes, commas, or newlines, wrap in quotes and escape internal quotes
        if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// Formats a CSV row from individual field values.
    /// </summary>
    /// <param name="fields">The field values to format.</param>
    /// <returns>A comma-separated CSV row.</returns>
    private static string FormatCsvRow(params object[] fields)
    {
        return string.Join(",", fields);
    }
}
