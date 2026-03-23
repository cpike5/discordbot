using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.Users"/> category.
/// Requires admin authorization.
/// </summary>
public class UsersSearchProvider : ISearchProvider
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<UsersSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.Users;

    /// <inheritdoc/>
    public bool RequiresAdmin => true;

    public UsersSearchProvider(IUserManagementService userManagementService, ILogger<UsersSearchProvider> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching users for term: {SearchTerm}", searchTerm);

        var query = new UserSearchQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 25
        };

        var users = await _userManagementService.GetUsersAsync(query, cancellationToken);

        var items = users.Items
            .Select(u => new
            {
                User = u,
                Score = SearchScoringHelper.CalculateRelevanceScore(u.Email, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(u.DisplayName ?? "", searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(u.DiscordUsername ?? "", searchTerm)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.User.Id,
                Title = x.User.DisplayName ?? x.User.Email,
                Subtitle = x.User.Email,
                Description = x.User.IsDiscordLinked
                    ? $"Discord: {x.User.DiscordUsername}"
                    : "No Discord account linked",
                IconUrl = x.User.DiscordAvatarUrl,
                BadgeText = x.User.HighestRole,
                BadgeVariant = SearchDisplayHelper.GetRoleBadgeVariant(x.User.HighestRole),
                Url = $"/Admin/Users/Details?id={x.User.Id}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Timestamp = x.User.CreatedAt,
                Metadata = new Dictionary<string, string>
                {
                    ["IsActive"] = x.User.IsActive.ToString(),
                    ["IsDiscordLinked"] = x.User.IsDiscordLinked.ToString(),
                    ["EmailConfirmed"] = x.User.EmailConfirmed.ToString()
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.Users,
            DisplayName = "Users",
            Items = items,
            TotalCount = users.TotalCount,
            HasMore = users.TotalCount > maxResults,
            ViewAllUrl = $"/Admin/Users?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
