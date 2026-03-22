using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.MessageLogs"/> category.
/// Requires admin authorization.
/// </summary>
public class MessageLogsSearchProvider : ISearchProvider
{
    private readonly IMessageLogService _messageLogService;
    private readonly ILogger<MessageLogsSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.MessageLogs;

    /// <inheritdoc/>
    public bool RequiresAdmin => true;

    public MessageLogsSearchProvider(IMessageLogService messageLogService, ILogger<MessageLogsSearchProvider> logger)
    {
        _messageLogService = messageLogService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching message logs for term: {SearchTerm}", searchTerm);

        var query = new MessageLogQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 25
        };

        var logs = await _messageLogService.GetLogsAsync(query, cancellationToken);

        var items = logs.Items
            .Select(log => new
            {
                Log = log,
                Score = SearchScoringHelper.CalculateRelevanceScore(log.Content, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(log.AuthorUsername ?? "", searchTerm) / 2 +
                        SearchScoringHelper.CalculateRelevanceScore(log.ChannelName ?? "", searchTerm) / 3 +
                        SearchScoringHelper.CalculateRelevanceScore(log.GuildName ?? "", searchTerm) / 3
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Log.Timestamp)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Log.Id.ToString(),
                Title = SearchDisplayHelper.Truncate(x.Log.Content, 60),
                Subtitle = $"{x.Log.AuthorUsername} in #{x.Log.ChannelName}",
                Description = $"Guild: {x.Log.GuildName ?? "DM"} | {x.Log.Timestamp:MMM d, yyyy h:mm tt}",
                BadgeText = x.Log.Source.ToString(),
                BadgeVariant = x.Log.Source == MessageSource.ServerChannel ? "primary" : "secondary",
                Url = $"/Admin/MessageLogs/Details/{x.Log.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Log.Timestamp,
                Metadata = new Dictionary<string, string>
                {
                    ["AuthorId"] = x.Log.AuthorId.ToString(),
                    ["ChannelId"] = x.Log.ChannelId.ToString(),
                    ["GuildId"] = x.Log.GuildId?.ToString() ?? "DM",
                    ["HasAttachments"] = x.Log.HasAttachments.ToString(),
                    ["HasEmbeds"] = x.Log.HasEmbeds.ToString()
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.MessageLogs,
            DisplayName = "Message Logs",
            Items = items,
            TotalCount = logs.TotalCount,
            HasMore = logs.TotalCount > maxResults,
            ViewAllUrl = $"/Admin/MessageLogs?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
