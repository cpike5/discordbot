using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.AuditLogs"/> category.
/// Requires admin authorization.
/// </summary>
public class AuditLogsSearchProvider : ISearchProvider
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.AuditLogs;

    /// <inheritdoc/>
    public bool RequiresAdmin => true;

    public AuditLogsSearchProvider(IAuditLogService auditLogService, ILogger<AuditLogsSearchProvider> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching audit logs for term: {SearchTerm}", searchTerm);

        var query = new AuditLogQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 25
        };

        var (logs, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

        var items = logs
            .Select(log => new
            {
                Log = log,
                Score = SearchScoringHelper.CalculateRelevanceScore(log.ActionName, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(log.CategoryName, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(log.ActorDisplayName ?? "", searchTerm) / 2 +
                        SearchScoringHelper.CalculateRelevanceScore(log.GuildName ?? "", searchTerm) / 2
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Log.Timestamp)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Log.Id.ToString(),
                Title = $"{x.Log.CategoryName}: {x.Log.ActionName}",
                Subtitle = x.Log.ActorDisplayName ?? "System",
                Description = $"Target: {x.Log.TargetType ?? "N/A"} ({x.Log.TargetId ?? "N/A"})",
                BadgeText = x.Log.CategoryName,
                BadgeVariant = SearchDisplayHelper.GetAuditLogBadgeVariant(x.Log.CategoryName),
                Url = $"/Admin/AuditLogs/Details/{x.Log.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Log.Timestamp,
                Metadata = new Dictionary<string, string>
                {
                    ["Action"] = x.Log.ActionName,
                    ["ActorType"] = x.Log.ActorTypeName,
                    ["GuildId"] = x.Log.GuildId?.ToString() ?? "N/A"
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.AuditLogs,
            DisplayName = "Audit Logs",
            Items = items,
            TotalCount = totalCount,
            HasMore = totalCount > maxResults,
            ViewAllUrl = $"/Admin/AuditLogs?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
