using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for generating moderation analytics and metrics.
/// Aggregates data from moderation case repository to provide insights into moderation patterns.
/// </summary>
public class ModerationAnalyticsService : IModerationAnalyticsService
{
    private readonly IModerationCaseRepository _moderationCaseRepository;
    private readonly ILogger<ModerationAnalyticsService> _logger;

    public ModerationAnalyticsService(
        IModerationCaseRepository moderationCaseRepository,
        ILogger<ModerationAnalyticsService> logger)
    {
        _moderationCaseRepository = moderationCaseRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ModerationAnalyticsSummaryDto> GetSummaryAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Generating moderation analytics summary for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get all cases in the time period
        var (cases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: start,
            endDate: end,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        var casesList = cases.ToList();

        // Count cases by type
        var totalCases = casesList.Count;
        var warnCount = casesList.Count(c => c.Type == CaseType.Warn);
        var muteCount = casesList.Count(c => c.Type == CaseType.Mute);
        var kickCount = casesList.Count(c => c.Type == CaseType.Kick);
        var banCount = casesList.Count(c => c.Type == CaseType.Ban);
        var noteCount = casesList.Count(c => c.Type == CaseType.Note);

        // Calculate time-based metrics
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var last7d = now.AddDays(-7);

        var casesLast24h = casesList.Count(c => c.CreatedAt >= last24h);
        var casesLast7d = casesList.Count(c => c.CreatedAt >= last7d);

        // Calculate average cases per day
        var daySpan = (end - start).TotalDays;
        var casesPerDay = daySpan > 0 ? (decimal)totalCases / (decimal)daySpan : 0m;

        // Calculate change from previous period
        var periodLength = end - start;
        var previousPeriodStart = start - periodLength;
        var previousPeriodEnd = start;

        var (previousCases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: previousPeriodStart,
            endDate: previousPeriodEnd,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        var previousCaseCount = previousCases.Count();
        var changeFromPreviousPeriod = previousCaseCount > 0
            ? ((decimal)(totalCases - previousCaseCount) / previousCaseCount) * 100
            : totalCases > 0 ? 100m : 0m;

        _logger.LogInformation(
            "Moderation analytics summary generated for guild {GuildId}: {TotalCases} total cases, {Cases24h} in last 24h",
            guildId, totalCases, casesLast24h);

        return new ModerationAnalyticsSummaryDto
        {
            TotalCases = totalCases,
            WarnCount = warnCount,
            MuteCount = muteCount,
            KickCount = kickCount,
            BanCount = banCount,
            NoteCount = noteCount,
            Cases24h = casesLast24h,
            Cases7d = casesLast7d,
            CasesPerDay = casesPerDay,
            ChangeFromPreviousPeriod = changeFromPreviousPeriod
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModerationTrendDto>> GetTrendsAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Generating moderation trends for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get all cases in the time period
        var (cases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: start,
            endDate: end,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        // Group by date and count by type
        var trends = cases
            .GroupBy(c => DateOnly.FromDateTime(c.CreatedAt))
            .Select(g => new ModerationTrendDto
            {
                Date = g.Key.ToDateTime(TimeOnly.MinValue),
                TotalCases = g.Count(),
                WarnCount = g.Count(c => c.Type == CaseType.Warn),
                MuteCount = g.Count(c => c.Type == CaseType.Mute),
                KickCount = g.Count(c => c.Type == CaseType.Kick),
                BanCount = g.Count(c => c.Type == CaseType.Ban)
            })
            .OrderBy(t => t.Date)
            .ToList();

        _logger.LogDebug(
            "Generated {Count} trend data points for guild {GuildId}",
            trends.Count, guildId);

        return trends;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepeatOffenderDto>> GetRepeatOffendersAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting top {Limit} repeat offenders for guild {GuildId} from {Start} to {End}",
            limit, guildId, start, end);

        // Get all cases in the time period
        var (cases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: start,
            endDate: end,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        // Group by target user and filter for repeat offenders (2+ cases)
        var offenders = cases
            .GroupBy(c => c.TargetUserId)
            .Where(g => g.Count() >= 2)
            .Select(g => new
            {
                UserId = g.Key,
                Cases = g.OrderBy(c => c.CreatedAt).ToList()
            })
            .Select(x => new RepeatOffenderDto
            {
                UserId = x.UserId,
                Username = $"User {x.UserId}", // Username not stored in ModerationCase
                AvatarUrl = null, // Avatar not tracked in ModerationCase entity
                TotalCases = x.Cases.Count,
                WarnCount = x.Cases.Count(c => c.Type == CaseType.Warn),
                MuteCount = x.Cases.Count(c => c.Type == CaseType.Mute),
                KickCount = x.Cases.Count(c => c.Type == CaseType.Kick),
                BanCount = x.Cases.Count(c => c.Type == CaseType.Ban),
                FirstIncident = x.Cases.First().CreatedAt,
                LastIncident = x.Cases.Last().CreatedAt,
                EscalationPath = x.Cases.Select(c => c.Type.ToString()).ToList()
            })
            .OrderByDescending(x => x.TotalCases)
            .ThenByDescending(x => x.LastIncident)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} repeat offenders for guild {GuildId}",
            offenders.Count, guildId);

        return offenders;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModeratorWorkloadDto>> GetModeratorWorkloadAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting moderator workload for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get all cases in the time period
        var (cases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: start,
            endDate: end,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        var casesList = cases.ToList();
        var totalActions = casesList.Count;

        // Group by moderator and calculate workload
        var workload = casesList
            .GroupBy(c => c.ModeratorUserId)
            .Select(g => new
            {
                ModeratorId = g.Key,
                Cases = g.ToList()
            })
            .Select(x => new ModeratorWorkloadDto
            {
                ModeratorId = x.ModeratorId,
                ModeratorUsername = $"Moderator {x.ModeratorId}", // Username not stored in ModerationCase
                AvatarUrl = null, // Avatar not tracked in ModerationCase entity
                TotalActions = x.Cases.Count,
                WarnCount = x.Cases.Count(c => c.Type == CaseType.Warn),
                MuteCount = x.Cases.Count(c => c.Type == CaseType.Mute),
                KickCount = x.Cases.Count(c => c.Type == CaseType.Kick),
                BanCount = x.Cases.Count(c => c.Type == CaseType.Ban),
                Percentage = totalActions > 0 ? ((decimal)x.Cases.Count / totalActions) * 100 : 0m
            })
            .OrderByDescending(x => x.TotalActions)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Retrieved workload metrics for {Count} moderators in guild {GuildId}",
            workload.Count, guildId);

        return workload;
    }

    /// <inheritdoc/>
    public async Task<CaseTypeDistributionDto> GetCaseDistributionAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting case type distribution for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get all cases in the time period
        var (cases, _) = await _moderationCaseRepository.GetByGuildAsync(
            guildId,
            startDate: start,
            endDate: end,
            page: 1,
            pageSize: int.MaxValue,
            cancellationToken: ct);

        var casesList = cases.ToList();

        // Count by type
        var warnCount = casesList.Count(c => c.Type == CaseType.Warn);
        var muteCount = casesList.Count(c => c.Type == CaseType.Mute);
        var kickCount = casesList.Count(c => c.Type == CaseType.Kick);
        var banCount = casesList.Count(c => c.Type == CaseType.Ban);
        var noteCount = casesList.Count(c => c.Type == CaseType.Note);

        var distribution = new CaseTypeDistributionDto
        {
            WarnCount = warnCount,
            MuteCount = muteCount,
            KickCount = kickCount,
            BanCount = banCount,
            NoteCount = noteCount,
            Total = casesList.Count
        };

        _logger.LogDebug(
            "Case distribution for guild {GuildId}: {Total} total ({Warn} warns, {Mute} mutes, {Kick} kicks, {Ban} bans, {Note} notes)",
            guildId, distribution.Total, warnCount, muteCount, kickCount, banCount, noteCount);

        return distribution;
    }
}
