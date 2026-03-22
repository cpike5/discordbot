using System.Security.Claims;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Orchestrates unified search operations across all registered <see cref="ISearchProvider"/> implementations.
/// Handles authorization, caching, and result assembly. Individual search logic lives in each provider.
/// </summary>
public class SearchService : ISearchService
{
    private readonly IEnumerable<ISearchProvider> _providers;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMemoryCache _cache;
    private readonly CachingOptions _cachingOptions;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IEnumerable<ISearchProvider> providers,
        IAuthorizationService authorizationService,
        IMemoryCache cache,
        IOptions<CachingOptions> cachingOptions,
        ILogger<SearchService> logger)
    {
        _providers = providers;
        _authorizationService = authorizationService;
        _cache = cache;
        _cachingOptions = cachingOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UnifiedSearchResultDto> SearchAsync(
        SearchQueryDto query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            _logger.LogDebug("Search called with empty search term");
            return new UnifiedSearchResultDto { SearchTerm = string.Empty };
        }

        var searchTerm = query.SearchTerm.Trim();

        if (searchTerm.Length < 2)
        {
            _logger.LogDebug("Search term too short (< 2 characters): {SearchTerm}", searchTerm);
            return new UnifiedSearchResultDto { SearchTerm = searchTerm };
        }

        _logger.LogInformation(
            "Unified search initiated for term: {SearchTerm}, MaxResults: {MaxResults}, CategoryFilter: {CategoryFilter}",
            searchTerm, query.MaxResultsPerCategory, query.CategoryFilter);

        var userId = user.Identity?.Name ?? "anonymous";
        var cacheKey = $"search:{userId}:{searchTerm.ToLowerInvariant()}:{query.MaxResultsPerCategory}:{query.CategoryFilter}";

        if (_cache.TryGetValue(cacheKey, out UnifiedSearchResultDto? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Returning cached search results for term: {SearchTerm}", searchTerm);
            return cachedResult;
        }

        var canViewAdminCategories = (await _authorizationService.AuthorizeAsync(user, "RequireAdmin")).Succeeded;
        var searchTermLower = searchTerm.ToLowerInvariant();

        // Determine which providers to run based on category filter and authorization
        var providersToRun = _providers
            .Where(p => query.CategoryFilter == null || p.Category == query.CategoryFilter)
            .Where(p => !p.RequiresAdmin || canViewAdminCategories)
            .ToList();

        // Execute searches sequentially — underlying DbContext is not thread-safe
        var categoryResults = new List<SearchCategoryResult>();
        foreach (var provider in providersToRun)
        {
            try
            {
                categoryResults.Add(await provider.SearchAsync(searchTermLower, query.MaxResultsPerCategory, user, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching category {Category} for term: {SearchTerm}", provider.Category, searchTerm);
                categoryResults.Add(CreateEmptyResult(provider.Category));
            }
        }

        var result = new UnifiedSearchResultDto
        {
            SearchTerm = searchTerm,
            Guilds = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Guilds) ?? CreateEmptyResult(SearchCategory.Guilds),
            CommandLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.CommandLogs) ?? CreateEmptyResult(SearchCategory.CommandLogs),
            Users = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Users) ?? CreateEmptyResult(SearchCategory.Users),
            Commands = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Commands) ?? CreateEmptyResult(SearchCategory.Commands),
            AuditLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.AuditLogs) ?? CreateEmptyResult(SearchCategory.AuditLogs),
            MessageLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.MessageLogs) ?? CreateEmptyResult(SearchCategory.MessageLogs),
            Pages = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Pages) ?? CreateEmptyResult(SearchCategory.Pages),
            Reminders = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Reminders) ?? CreateEmptyResult(SearchCategory.Reminders),
            ScheduledMessages = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.ScheduledMessages) ?? CreateEmptyResult(SearchCategory.ScheduledMessages)
        };

        var cacheExpiry = TimeSpan.FromSeconds(_cachingOptions.SearchResultsCacheDurationSeconds);
        _cache.Set(cacheKey, result, cacheExpiry);

        _logger.LogInformation("Search completed for term: {SearchTerm}. Total results: {TotalResults}",
            searchTerm, result.TotalResultCount);

        return result;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchCategoryAsync(
        SearchCategory category,
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _logger.LogDebug("Category search called with empty search term");
            return CreateEmptyResult(category);
        }

        var normalizedSearchTerm = searchTerm.Trim().ToLowerInvariant();
        _logger.LogDebug("Category search for {Category}: {SearchTerm}, MaxResults: {MaxResults}",
            category, normalizedSearchTerm, maxResults);

        var provider = _providers.FirstOrDefault(p => p.Category == category);
        if (provider == null)
        {
            _logger.LogWarning("No search provider registered for category {Category}", category);
            return CreateEmptyResult(category);
        }

        if (provider.RequiresAdmin)
        {
            var canViewAdminCategories = (await _authorizationService.AuthorizeAsync(user, "RequireAdmin")).Succeeded;
            if (!canViewAdminCategories)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to search admin category {Category} without permission",
                    user.Identity?.Name, category);
                return CreateEmptyResult(category);
            }
        }

        try
        {
            return await provider.SearchAsync(normalizedSearchTerm, maxResults, user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching category {Category} for term: {SearchTerm}", category, searchTerm);
            return CreateEmptyResult(category);
        }
    }

    private static SearchCategoryResult CreateEmptyResult(SearchCategory category)
    {
        return new SearchCategoryResult
        {
            Category = category,
            DisplayName = GetCategoryDisplayName(category),
            Items = new List<SearchResultItemDto>(),
            TotalCount = 0,
            HasMore = false,
            ViewAllUrl = GetCategoryViewAllUrl(category)
        };
    }

    private static string GetCategoryDisplayName(SearchCategory category) => category switch
    {
        SearchCategory.Guilds            => "Guilds",
        SearchCategory.CommandLogs       => "Command Logs",
        SearchCategory.Users             => "Users",
        SearchCategory.Commands          => "Commands",
        SearchCategory.AuditLogs         => "Audit Logs",
        SearchCategory.MessageLogs       => "Message Logs",
        SearchCategory.Pages             => "Pages",
        SearchCategory.Reminders         => "Reminders",
        SearchCategory.ScheduledMessages => "Scheduled Messages",
        _                                => category.ToString()
    };

    private static string? GetCategoryViewAllUrl(SearchCategory category) => category switch
    {
        SearchCategory.Guilds            => "/Guilds",
        SearchCategory.CommandLogs       => "/Commands?tab=logs",
        SearchCategory.Users             => "/Admin/Users",
        SearchCategory.Commands          => "/Commands",
        SearchCategory.AuditLogs         => "/Admin/AuditLogs",
        SearchCategory.MessageLogs       => "/Admin/MessageLogs",
        SearchCategory.Pages             => null,
        SearchCategory.Reminders         => null,
        SearchCategory.ScheduledMessages => null,
        _                                => null
    };
}
