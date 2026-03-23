using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.Guilds"/> category.
/// </summary>
public class GuildsSearchProvider : ISearchProvider
{
    private readonly IGuildService _guildService;
    private readonly ILogger<GuildsSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.Guilds;

    /// <inheritdoc/>
    public bool RequiresAdmin => false;

    public GuildsSearchProvider(IGuildService guildService, ILogger<GuildsSearchProvider> logger)
    {
        _guildService = guildService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching guilds for term: {SearchTerm}", searchTerm);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 25
        };

        var guilds = await _guildService.GetGuildsAsync(query, cancellationToken);

        var items = guilds.Items
            .Select(g => new
            {
                Guild = g,
                Score = SearchScoringHelper.CalculateRelevanceScore(g.Name, searchTerm) +
                        (g.Id.ToString() == searchTerm ? 100 : 0)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Guild.Id.ToString(),
                Title = x.Guild.Name,
                Subtitle = $"ID: {x.Guild.Id}",
                Description = $"Joined {x.Guild.JoinedAt:MMM d, yyyy}",
                IconUrl = x.Guild.IconUrl,
                BadgeText = x.Guild.IsActive ? "Active" : "Inactive",
                BadgeVariant = x.Guild.IsActive ? "success" : "secondary",
                Url = $"/Guilds/Details/{x.Guild.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.Guild.JoinedAt,
                Metadata = new Dictionary<string, string>
                {
                    ["MemberCount"] = x.Guild.MemberCount?.ToString() ?? "Unknown",
                    ["Prefix"] = x.Guild.Prefix ?? "/"
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.Guilds,
            DisplayName = "Guilds",
            Items = items,
            TotalCount = guilds.TotalCount,
            HasMore = guilds.TotalCount > maxResults,
            ViewAllUrl = $"/Guilds?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
