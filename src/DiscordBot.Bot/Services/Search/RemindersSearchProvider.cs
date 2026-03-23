using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.Reminders"/> category.
/// Requires admin authorization.
/// </summary>
public class RemindersSearchProvider : ISearchProvider
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ILogger<RemindersSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.Reminders;

    /// <inheritdoc/>
    public bool RequiresAdmin => true;

    public RemindersSearchProvider(IReminderRepository reminderRepository, ILogger<RemindersSearchProvider> logger)
    {
        _reminderRepository = reminderRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching reminders for term: {SearchTerm}", searchTerm);

        var allReminders = (await _reminderRepository.SearchAsync(searchTerm, 75, cancellationToken)).ToList();

        var scoredReminders = allReminders
            .Select(r => new
            {
                Reminder = r,
                Score = SearchScoringHelper.CalculateRelevanceScore(r.Message, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(r.UserId.ToString(), searchTerm) / 2
            })
            .Where(x => x.Score > 0)
            .ToList();

        var totalCount = scoredReminders.Count;

        var items = scoredReminders
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Reminder.CreatedAt)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Reminder.Id.ToString(),
                Title = SearchDisplayHelper.Truncate(x.Reminder.Message, 50),
                Subtitle = $"User ID: {x.Reminder.UserId}",
                Description = x.Reminder.Status == ReminderStatus.Pending
                    ? $"Triggers {SearchDisplayHelper.GetRelativeTime(x.Reminder.TriggerAt)}"
                    : $"Status: {x.Reminder.Status}",
                BadgeText = x.Reminder.Status.ToString(),
                BadgeVariant = x.Reminder.Status switch
                {
                    ReminderStatus.Pending   => "warning",
                    ReminderStatus.Delivered => "success",
                    ReminderStatus.Failed    => "danger",
                    ReminderStatus.Cancelled => "secondary",
                    _                        => "secondary"
                },
                Url = $"/Guilds/{x.Reminder.GuildId}/Reminders",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Reminder.CreatedAt,
                Metadata = new Dictionary<string, string>
                {
                    ["GuildId"] = x.Reminder.GuildId.ToString(),
                    ["UserId"] = x.Reminder.UserId.ToString(),
                    ["Status"] = x.Reminder.Status.ToString()
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.Reminders,
            DisplayName = "Reminders",
            Items = items,
            TotalCount = totalCount,
            HasMore = totalCount > maxResults,
            ViewAllUrl = null
        };
    }
}
