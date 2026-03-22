using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.ScheduledMessages"/> category.
/// Requires admin authorization.
/// </summary>
public class ScheduledMessagesSearchProvider : ISearchProvider
{
    private readonly IScheduledMessageRepository _scheduledMessageRepository;
    private readonly ILogger<ScheduledMessagesSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.ScheduledMessages;

    /// <inheritdoc/>
    public bool RequiresAdmin => true;

    public ScheduledMessagesSearchProvider(
        IScheduledMessageRepository scheduledMessageRepository,
        ILogger<ScheduledMessagesSearchProvider> logger)
    {
        _scheduledMessageRepository = scheduledMessageRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching scheduled messages for term: {SearchTerm}", searchTerm);

        var allMessages = (await _scheduledMessageRepository.SearchAsync(searchTerm, 75, cancellationToken)).ToList();

        var scoredMessages = allMessages
            .Select(m => new
            {
                Message = m,
                Score = SearchScoringHelper.CalculateRelevanceScore(m.Content, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(m.Title, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(m.ChannelId.ToString(), searchTerm) / 2
            })
            .Where(x => x.Score > 0)
            .ToList();

        var totalCount = scoredMessages.Count;

        var items = scoredMessages
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Message.CreatedAt)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Message.Id.ToString(),
                Title = SearchDisplayHelper.Truncate(x.Message.Content, 50),
                Subtitle = $"Channel ID: {x.Message.ChannelId}",
                Description = x.Message.NextExecutionAt.HasValue
                    ? $"Next: {x.Message.NextExecutionAt.Value:MMM d, yyyy h:mm tt} UTC"
                    : $"Frequency: {x.Message.Frequency}",
                BadgeText = x.Message.IsEnabled ? "Active" : "Disabled",
                BadgeVariant = x.Message.IsEnabled ? "success" : "secondary",
                Url = $"/Guilds/ScheduledMessages/Edit/{x.Message.GuildId}/{x.Message.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Message.CreatedAt,
                Metadata = new Dictionary<string, string>
                {
                    ["GuildId"] = x.Message.GuildId.ToString(),
                    ["ChannelId"] = x.Message.ChannelId.ToString(),
                    ["Frequency"] = x.Message.Frequency.ToString(),
                    ["IsEnabled"] = x.Message.IsEnabled.ToString()
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.ScheduledMessages,
            DisplayName = "Scheduled Messages",
            Items = items,
            TotalCount = totalCount,
            HasMore = totalCount > maxResults,
            ViewAllUrl = null
        };
    }
}
