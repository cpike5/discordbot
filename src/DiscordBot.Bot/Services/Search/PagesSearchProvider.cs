using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.Pages"/> category.
/// Filters pages by the current user's authorization policies.
/// </summary>
public class PagesSearchProvider : ISearchProvider
{
    private readonly IPageMetadataService _pageMetadataService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<PagesSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.Pages;

    /// <inheritdoc/>
    public bool RequiresAdmin => false;

    public PagesSearchProvider(
        IPageMetadataService pageMetadataService,
        IAuthorizationService authorizationService,
        ILogger<PagesSearchProvider> logger)
    {
        _pageMetadataService = pageMetadataService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching pages for term: {SearchTerm}", searchTerm);

        var allPages = _pageMetadataService.SearchPages(searchTerm);
        var exactMatch = _pageMetadataService.FindExactMatch(searchTerm);

        // Filter pages based on user authorization
        var authorizedPages = new List<PageMetadataDto>();
        foreach (var page in allPages)
        {
            if (string.IsNullOrWhiteSpace(page.RequiredPolicy))
            {
                authorizedPages.Add(page);
            }
            else
            {
                var authResult = await _authorizationService.AuthorizeAsync(user, page.RequiredPolicy);
                if (authResult.Succeeded)
                {
                    authorizedPages.Add(page);
                }
            }
        }

        var items = authorizedPages
            .Select(p => new
            {
                Page = p,
                Score = SearchScoringHelper.CalculateRelevanceScore(p.Name, searchTerm)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Page.Route,
                Title = x.Page.Name,
                Subtitle = x.Page.Section,
                Description = x.Page.Description ?? string.Empty,
                BadgeText = x.Page.Section ?? "Main",
                BadgeVariant = SearchDisplayHelper.GetSectionBadgeVariant(x.Page.Section),
                Url = x.Page.RequiresGuildContext ? string.Empty : x.Page.Route,
                RequiresGuildContext = x.Page.RequiresGuildContext,
                RouteTemplate = x.Page.RouteTemplate,
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Metadata = new Dictionary<string, string>
                {
                    ["Section"] = x.Page.Section ?? "Main",
                    ["isExactMatch"] = (exactMatch != null && exactMatch.Route == x.Page.Route).ToString()
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.Pages,
            DisplayName = "Pages",
            Items = items,
            TotalCount = authorizedPages.Count,
            HasMore = authorizedPages.Count > maxResults,
            ViewAllUrl = null
        };
    }
}
