using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// Service implementation for managing moderation cases and moderation actions.
/// Handles creation, retrieval, updates, and exports of moderation cases.
/// </summary>
public class ModerationService : IModerationService
{
    private readonly IModerationCaseRepository _caseRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ModerationService> _logger;

    public ModerationService(
        IModerationCaseRepository caseRepository,
        DiscordSocketClient client,
        ILogger<ModerationService> logger)
    {
        _caseRepository = caseRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ModerationCaseDto> CreateCaseAsync(ModerationCaseCreateDto dto, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "create_case",
            guildId: dto.GuildId,
            userId: dto.TargetUserId);

        try
        {
            _logger.LogInformation("Creating moderation case for user {TargetUserId} in guild {GuildId}, type: {Type}",
                dto.TargetUserId, dto.GuildId, dto.Type);

            // Get next case number atomically
            var caseNumber = await _caseRepository.GetNextCaseNumberAsync(dto.GuildId, ct);

            var now = DateTime.UtcNow;
            var moderationCase = new ModerationCase
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                GuildId = dto.GuildId,
                TargetUserId = dto.TargetUserId,
                ModeratorUserId = dto.ModeratorUserId,
                Type = dto.Type,
                Reason = dto.Reason,
                Duration = dto.Duration,
                CreatedAt = now,
                ExpiresAt = dto.Duration.HasValue ? now.Add(dto.Duration.Value) : null,
                RelatedFlaggedEventId = dto.RelatedFlaggedEventId
            };

            await _caseRepository.AddAsync(moderationCase, ct);

            _logger.LogInformation("Moderation case {CaseNumber} created successfully for user {TargetUserId} in guild {GuildId}",
                caseNumber, dto.TargetUserId, dto.GuildId);

            var result = await MapToDtoAsync(moderationCase, ct);

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
    public async Task<ModerationCaseDto?> GetCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_case",
            entityId: caseId.ToString());

        try
        {
            _logger.LogDebug("Retrieving moderation case {CaseId}", caseId);

            var moderationCase = await _caseRepository.GetByIdAsync(caseId, ct);
            if (moderationCase == null)
            {
                _logger.LogWarning("Moderation case {CaseId} not found", caseId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var result = await MapToDtoAsync(moderationCase, ct);

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
    public async Task<ModerationCaseDto?> GetCaseByNumberAsync(ulong guildId, long caseNumber, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_case_by_number",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving moderation case #{CaseNumber} in guild {GuildId}", caseNumber, guildId);

            var moderationCase = await _caseRepository.GetByCaseNumberAsync(guildId, caseNumber, ct);
            if (moderationCase == null)
            {
                _logger.LogWarning("Moderation case #{CaseNumber} not found in guild {GuildId}", caseNumber, guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var result = await MapToDtoAsync(moderationCase, ct);

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
    public async Task<(IEnumerable<ModerationCaseDto> Items, int TotalCount)> GetCasesAsync(ModerationCaseQueryDto query, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_cases",
            guildId: query.GuildId,
            userId: query.TargetUserId);

        try
        {
            _logger.LogDebug("Retrieving moderation cases for guild {GuildId} with filters", query.GuildId);

            var (cases, totalCount) = await _caseRepository.GetByGuildAsync(
                query.GuildId,
                query.Type,
                query.TargetUserId,
                query.ModeratorUserId,
                query.StartDate,
                query.EndDate,
                query.Page,
                query.PageSize,
                ct);

            var dtos = new List<ModerationCaseDto>();
            foreach (var moderationCase in cases)
            {
                dtos.Add(await MapToDtoAsync(moderationCase, ct));
            }

            _logger.LogDebug("Retrieved {Count} moderation cases out of {TotalCount} for guild {GuildId}",
                dtos.Count, totalCount, query.GuildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
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
    public async Task<ModerationCaseDto?> UpdateCaseReasonAsync(ulong guildId, long caseNumber, string reason, ulong moderatorId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "update_case_reason",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Updating reason for moderation case #{CaseNumber} in guild {GuildId} by moderator {ModeratorId}",
                caseNumber, guildId, moderatorId);

            var moderationCase = await _caseRepository.GetByCaseNumberAsync(guildId, caseNumber, ct);
            if (moderationCase == null)
            {
                _logger.LogWarning("Moderation case #{CaseNumber} not found in guild {GuildId}", caseNumber, guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            moderationCase.Reason = reason;
            await _caseRepository.UpdateAsync(moderationCase, ct);

            _logger.LogInformation("Moderation case #{CaseNumber} reason updated successfully in guild {GuildId}",
                caseNumber, guildId);

            var result = await MapToDtoAsync(moderationCase, ct);

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
    public async Task<(IEnumerable<ModerationCaseDto> Items, int TotalCount)> GetUserCasesAsync(ulong guildId, ulong userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_user_cases",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogDebug("Retrieving moderation cases for user {UserId} in guild {GuildId}", userId, guildId);

            var (cases, totalCount) = await _caseRepository.GetByUserAsync(guildId, userId, page, pageSize, ct);

            var dtos = new List<ModerationCaseDto>();
            foreach (var moderationCase in cases)
            {
                dtos.Add(await MapToDtoAsync(moderationCase, ct));
            }

            _logger.LogDebug("Retrieved {Count} moderation cases out of {TotalCount} for user {UserId} in guild {GuildId}",
                dtos.Count, totalCount, userId, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
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
    public async Task<byte[]> ExportUserHistoryAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "export_user_history",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogInformation("Exporting moderation history for user {UserId} in guild {GuildId}", userId, guildId);

            var (cases, _) = await _caseRepository.GetByUserAsync(guildId, userId, 1, int.MaxValue, ct);

            var dtos = new List<ModerationCaseDto>();
            foreach (var moderationCase in cases)
            {
                dtos.Add(await MapToDtoAsync(moderationCase, ct));
            }

            var exportData = new
            {
                GuildId = guildId,
                UserId = userId,
                ExportedAt = DateTime.UtcNow,
                TotalCases = dtos.Count,
                Cases = dtos
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            _logger.LogInformation("Exported {Count} moderation cases for user {UserId} in guild {GuildId}",
                dtos.Count, userId, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModeratorStatsSummaryDto> GetModeratorStatsAsync(ulong guildId, ulong? moderatorId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_moderator_stats",
            guildId: guildId,
            userId: moderatorId);

        try
        {
            _logger.LogDebug("Retrieving moderator statistics for guild {GuildId}, moderator: {ModeratorId}", guildId, moderatorId);

            var (cases, _) = await _caseRepository.GetByGuildAsync(
                guildId,
                null,
                null,
                moderatorId,
                startDate,
                endDate,
                1,
                int.MaxValue,
                ct);

            var casesList = cases.ToList();

            // Aggregate by case type
            var warnCount = casesList.Count(c => c.Type == CaseType.Warn);
            var kickCount = casesList.Count(c => c.Type == CaseType.Kick);
            var banCount = casesList.Count(c => c.Type == CaseType.Ban);
            var muteCount = casesList.Count(c => c.Type == CaseType.Mute);

            var summary = new ModeratorStatsSummaryDto
            {
                GuildId = guildId,
                ModeratorId = moderatorId,
                StartDate = startDate,
                EndDate = endDate,
                TotalCases = casesList.Count,
                WarnCount = warnCount,
                KickCount = kickCount,
                BanCount = banCount,
                MuteCount = muteCount
            };

            // If querying guild-wide stats, get top moderators
            if (moderatorId == null)
            {
                var moderatorGroups = casesList
                    .GroupBy(c => c.ModeratorUserId)
                    .Select(g => new
                    {
                        ModeratorId = g.Key,
                        Cases = g.ToList(),
                        TotalActions = g.Count(),
                        WarnCount = g.Count(c => c.Type == CaseType.Warn),
                        KickCount = g.Count(c => c.Type == CaseType.Kick),
                        BanCount = g.Count(c => c.Type == CaseType.Ban),
                        MuteCount = g.Count(c => c.Type == CaseType.Mute)
                    })
                    .OrderByDescending(g => g.TotalActions)
                    .Take(10)
                    .ToList();

                var topModerators = new List<ModeratorStatsEntryDto>();
                foreach (var group in moderatorGroups)
                {
                    var username = await GetUsernameAsync(group.ModeratorId);
                    topModerators.Add(new ModeratorStatsEntryDto
                    {
                        UserId = group.ModeratorId,
                        Username = username,
                        TotalActions = group.TotalActions,
                        WarnCount = group.WarnCount,
                        KickCount = group.KickCount,
                        BanCount = group.BanCount,
                        MuteCount = group.MuteCount
                    });
                }

                summary.TopModerators = topModerators;
            }
            else
            {
                summary.ModeratorUsername = await GetUsernameAsync(moderatorId.Value);
            }

            _logger.LogDebug("Calculated moderator statistics for guild {GuildId}: {TotalCases} total cases",
                guildId, summary.TotalCases);

            BotActivitySource.SetSuccess(activity);
            return summary;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ModerationCase>> GetExpiredTemporaryActionsAsync(CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "moderation",
            "get_expired_temporary_actions");

        try
        {
            _logger.LogDebug("Retrieving expired temporary moderation actions");

            var expired = await _caseRepository.GetExpiredCasesAsync(DateTime.UtcNow, ct);
            var expiredList = expired.ToList();

            _logger.LogDebug("Found {Count} expired temporary actions", expiredList.Count);

            BotActivitySource.SetRecordsReturned(activity, expiredList.Count);
            BotActivitySource.SetSuccess(activity);
            return expiredList;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a ModerationCase entity to a DTO with resolved usernames.
    /// </summary>
    private async Task<ModerationCaseDto> MapToDtoAsync(ModerationCase moderationCase, CancellationToken ct = default)
    {
        var targetUsername = await GetUsernameAsync(moderationCase.TargetUserId);
        var moderatorUsername = await GetUsernameAsync(moderationCase.ModeratorUserId);

        return new ModerationCaseDto
        {
            Id = moderationCase.Id,
            CaseNumber = (int)moderationCase.CaseNumber,
            GuildId = moderationCase.GuildId,
            TargetUserId = moderationCase.TargetUserId,
            TargetUsername = targetUsername,
            ModeratorUserId = moderationCase.ModeratorUserId,
            ModeratorUsername = moderatorUsername,
            Type = moderationCase.Type,
            Reason = moderationCase.Reason,
            Duration = moderationCase.Duration,
            CreatedAt = moderationCase.CreatedAt,
            ExpiresAt = moderationCase.ExpiresAt,
            RelatedFlaggedEventId = moderationCase.RelatedFlaggedEventId
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
}
