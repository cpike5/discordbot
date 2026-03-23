using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.CommandLogs"/> category.
/// </summary>
public class CommandLogsSearchProvider : ISearchProvider
{
    private readonly ICommandLogService _commandLogService;
    private readonly ILogger<CommandLogsSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.CommandLogs;

    /// <inheritdoc/>
    public bool RequiresAdmin => false;

    public CommandLogsSearchProvider(ICommandLogService commandLogService, ILogger<CommandLogsSearchProvider> logger)
    {
        _commandLogService = commandLogService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching command logs for term: {SearchTerm}", searchTerm);

        var query = new CommandLogQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 25
        };

        var logs = await _commandLogService.GetLogsAsync(query, cancellationToken);

        var items = logs.Items
            .Select(log => new
            {
                Log = log,
                Score = SearchScoringHelper.CalculateRelevanceScore(log.CommandName, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(log.Username ?? "", searchTerm) / 2 +
                        SearchScoringHelper.CalculateRelevanceScore(log.GuildName ?? "", searchTerm) / 2
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Log.ExecutedAt)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Log.Id.ToString(),
                Title = $"/{x.Log.CommandName}",
                Subtitle = $"{x.Log.Username} in {x.Log.GuildName ?? "DM"}",
                Description = x.Log.Success
                    ? $"Executed successfully ({x.Log.ResponseTimeMs}ms)"
                    : $"Failed: {x.Log.ErrorMessage}",
                BadgeText = x.Log.Success ? "Success" : "Failed",
                BadgeVariant = x.Log.Success ? "success" : "danger",
                Url = $"/CommandLogs/{x.Log.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Log.ExecutedAt,
                Metadata = new Dictionary<string, string>
                {
                    ["ResponseTime"] = $"{x.Log.ResponseTimeMs}ms",
                    ["UserId"] = x.Log.UserId.ToString(),
                    ["GuildId"] = x.Log.GuildId?.ToString() ?? "DM"
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.CommandLogs,
            DisplayName = "Command Logs",
            Items = items,
            TotalCount = logs.TotalCount,
            HasMore = logs.TotalCount > maxResults,
            ViewAllUrl = $"/Commands?tab=logs&search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
