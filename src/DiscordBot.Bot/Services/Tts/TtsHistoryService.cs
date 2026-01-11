using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Service implementation for managing TTS message history and statistics.
/// </summary>
public class TtsHistoryService : ITtsHistoryService
{
    private readonly ITtsMessageRepository _messageRepository;
    private readonly ILogger<TtsHistoryService> _logger;

    public TtsHistoryService(
        ITtsMessageRepository messageRepository,
        ILogger<TtsHistoryService> logger)
    {
        _messageRepository = messageRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogMessageAsync(TtsMessage message, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_history",
            "log_message",
            guildId: message.GuildId,
            userId: message.UserId);

        try
        {
            _logger.LogDebug(
                "Logging TTS message for user {UserId} in guild {GuildId}",
                message.UserId, message.GuildId);

            // Ensure ID and timestamp are set
            if (message.Id == Guid.Empty)
            {
                message.Id = Guid.NewGuid();
            }

            if (message.CreatedAt == default)
            {
                message.CreatedAt = DateTime.UtcNow;
            }

            await _messageRepository.AddAsync(message, ct);

            _logger.LogInformation(
                "Logged TTS message {MessageId} for user {UserId} in guild {GuildId}, duration: {Duration}s",
                message.Id, message.UserId, message.GuildId, message.DurationSeconds);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TtsMessageDto>> GetRecentMessagesAsync(
        ulong guildId,
        int count = 20,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_history",
            "get_recent",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Getting {Count} recent TTS messages for guild {GuildId}",
                count, guildId);

            var messages = await _messageRepository.GetRecentByGuildAsync(guildId, count, ct);

            var dtos = messages.Select(m => new TtsMessageDto
            {
                Id = m.Id,
                UserId = m.UserId,
                Username = m.Username,
                Message = m.Message,
                Voice = m.Voice,
                DurationSeconds = m.DurationSeconds,
                CreatedAt = m.CreatedAt
            }).ToList();

            _logger.LogDebug(
                "Retrieved {Count} TTS messages for guild {GuildId}",
                dtos.Count, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<TtsStatsDto> GetStatsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_history",
            "get_stats",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting TTS stats for guild {GuildId}", guildId);

            var today = DateTime.UtcNow.Date;
            var allTime = DateTime.MinValue;

            // Gather stats in parallel where possible
            var messagesTodayTask = _messageRepository.GetMessageCountAsync(guildId, today, ct);
            var totalMessagesTask = _messageRepository.GetMessageCountAsync(guildId, allTime, ct);
            var totalPlaybackTask = _messageRepository.GetTotalPlaybackSecondsAsync(guildId, today, ct);
            var uniqueUsersTask = _messageRepository.GetUniqueUserCountAsync(guildId, today, ct);
            var mostUsedVoiceTask = _messageRepository.GetMostUsedVoiceAsync(guildId, today, ct);
            var topUserTask = _messageRepository.GetTopUserAsync(guildId, today, ct);

            await Task.WhenAll(
                messagesTodayTask,
                totalMessagesTask,
                totalPlaybackTask,
                uniqueUsersTask,
                mostUsedVoiceTask,
                topUserTask);

            var topUser = await topUserTask;

            var stats = new TtsStatsDto
            {
                GuildId = guildId,
                MessagesToday = await messagesTodayTask,
                TotalMessages = await totalMessagesTask,
                TotalPlaybackSeconds = await totalPlaybackTask,
                UniqueUsers = await uniqueUsersTask,
                MostUsedVoice = await mostUsedVoiceTask,
                TopUserId = topUser?.UserId,
                TopUsername = topUser?.Username,
                TopUserMessageCount = topUser?.MessageCount ?? 0
            };

            _logger.LogDebug(
                "TTS stats for guild {GuildId}: {MessagesToday} today, {TotalMessages} total, {UniqueUsers} unique users",
                guildId, stats.MessagesToday, stats.TotalMessages, stats.UniqueUsers);

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
    public async Task<bool> DeleteMessageAsync(Guid id, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_history",
            "delete_message",
            entityId: id.ToString());

        try
        {
            _logger.LogDebug("Deleting TTS message {MessageId}", id);

            var deleted = await _messageRepository.DeleteAsync(id, ct);

            if (deleted)
            {
                _logger.LogInformation("Deleted TTS message {MessageId}", id);
            }
            else
            {
                _logger.LogDebug("TTS message {MessageId} not found for deletion", id);
            }

            BotActivitySource.SetSuccess(activity);
            return deleted;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
