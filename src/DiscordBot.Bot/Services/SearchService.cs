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
/// Service for unified search operations across multiple categories.
/// Orchestrates searches, applies authorization, caches results, and ranks by relevance.
/// </summary>
public class SearchService : ISearchService
{
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IUserManagementService _userManagementService;
    private readonly IPageMetadataService _pageMetadataService;
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMemoryCache _cache;
    private readonly CachingOptions _cachingOptions;
    private readonly ILogger<SearchService> _logger;

    private const int ExactMatchScore = 100;
    private const int StartsWithScore = 75;
    private const int ContainsScore = 50;

    public SearchService(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IUserManagementService userManagementService,
        IPageMetadataService pageMetadataService,
        ICommandMetadataService commandMetadataService,
        IAuthorizationService authorizationService,
        IMemoryCache cache,
        IOptions<CachingOptions> cachingOptions,
        ILogger<SearchService> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _userManagementService = userManagementService;
        _pageMetadataService = pageMetadataService;
        _commandMetadataService = commandMetadataService;
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
        _logger.LogInformation("Unified search initiated for term: {SearchTerm}, MaxResults: {MaxResults}, CategoryFilter: {CategoryFilter}",
            searchTerm, query.MaxResultsPerCategory, query.CategoryFilter);

        // Generate cache key based on search parameters and user identity
        var userId = user.Identity?.Name ?? "anonymous";
        var cacheKey = $"search:{userId}:{searchTerm}:{query.MaxResultsPerCategory}:{query.CategoryFilter}";

        // Try to get cached results
        if (_cache.TryGetValue(cacheKey, out UnifiedSearchResultDto? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Returning cached search results for term: {SearchTerm}", searchTerm);
            return cachedResult;
        }

        // Check authorization for Admin+ categories
        var canViewAdminCategories = (await _authorizationService.AuthorizeAsync(user, "RequireAdmin")).Succeeded;

        // Determine which categories to search
        var categoriesToSearch = DetermineCategoriesToSearch(query.CategoryFilter, canViewAdminCategories);

        // Execute searches in parallel
        var searchTasks = categoriesToSearch.Select(category =>
            SearchCategoryInternalAsync(category, searchTerm, query.MaxResultsPerCategory, user, canViewAdminCategories, cancellationToken)
        ).ToList();

        var categoryResults = await Task.WhenAll(searchTasks);

        // Build unified result
        var result = new UnifiedSearchResultDto
        {
            SearchTerm = searchTerm,
            Guilds = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Guilds) ?? CreateEmptyResult(SearchCategory.Guilds),
            CommandLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.CommandLogs) ?? CreateEmptyResult(SearchCategory.CommandLogs),
            Users = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Users) ?? CreateEmptyResult(SearchCategory.Users),
            Commands = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Commands) ?? CreateEmptyResult(SearchCategory.Commands),
            AuditLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.AuditLogs) ?? CreateEmptyResult(SearchCategory.AuditLogs),
            MessageLogs = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.MessageLogs) ?? CreateEmptyResult(SearchCategory.MessageLogs),
            Pages = categoryResults.FirstOrDefault(r => r.Category == SearchCategory.Pages) ?? CreateEmptyResult(SearchCategory.Pages)
        };

        // Cache the result
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

        var normalizedSearchTerm = searchTerm.Trim();
        _logger.LogDebug("Category search for {Category}: {SearchTerm}, MaxResults: {MaxResults}",
            category, normalizedSearchTerm, maxResults);

        // Check authorization
        var canViewAdminCategories = (await _authorizationService.AuthorizeAsync(user, "RequireAdmin")).Succeeded;

        if (IsAdminCategory(category) && !canViewAdminCategories)
        {
            _logger.LogWarning("User {UserId} attempted to search admin category {Category} without permission",
                user.Identity?.Name, category);
            return CreateEmptyResult(category);
        }

        return await SearchCategoryInternalAsync(category, normalizedSearchTerm, maxResults, user, canViewAdminCategories, cancellationToken);
    }

    private async Task<SearchCategoryResult> SearchCategoryInternalAsync(
        SearchCategory category,
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        bool canViewAdminCategories,
        CancellationToken cancellationToken)
    {
        try
        {
            return category switch
            {
                SearchCategory.Guilds => await SearchGuildsAsync(searchTerm, maxResults, cancellationToken),
                SearchCategory.CommandLogs => await SearchCommandLogsAsync(searchTerm, maxResults, cancellationToken),
                SearchCategory.Users when canViewAdminCategories => await SearchUsersAsync(searchTerm, maxResults, cancellationToken),
                SearchCategory.Commands => await SearchCommandsAsync(searchTerm, maxResults, cancellationToken),
                SearchCategory.AuditLogs when canViewAdminCategories => CreateEmptyResult(SearchCategory.AuditLogs), // Phase 3
                SearchCategory.MessageLogs when canViewAdminCategories => CreateEmptyResult(SearchCategory.MessageLogs), // Phase 3
                SearchCategory.Pages => await SearchPagesAsync(searchTerm, maxResults, user, cancellationToken),
                _ => CreateEmptyResult(category)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching category {Category} for term: {SearchTerm}", category, searchTerm);
            return CreateEmptyResult(category);
        }
    }

    private async Task<SearchCategoryResult> SearchGuildsAsync(string searchTerm, int maxResults, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching guilds for term: {SearchTerm}", searchTerm);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 100 // Get more results for better ranking
        };

        var guilds = await _guildService.GetGuildsAsync(query, cancellationToken);
        var searchLower = searchTerm.ToLowerInvariant();

        var items = guilds.Items
            .Select(g => new
            {
                Guild = g,
                Score = CalculateRelevanceScore(g.Name, searchLower) +
                        (g.Id.ToString() == searchTerm ? ExactMatchScore : 0)
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
                Url = $"/Guilds/Details?id={x.Guild.Id}",
                RelevanceScore = x.Score,
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

    private async Task<SearchCategoryResult> SearchCommandLogsAsync(string searchTerm, int maxResults, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching command logs for term: {SearchTerm}", searchTerm);

        var query = new CommandLogQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 100 // Get more results for better ranking
        };

        var logs = await _commandLogService.GetLogsAsync(query, cancellationToken);
        var searchLower = searchTerm.ToLowerInvariant();

        var items = logs.Items
            .Select(log => new
            {
                Log = log,
                Score = CalculateRelevanceScore(log.CommandName, searchLower) +
                        CalculateRelevanceScore(log.Username ?? "", searchLower) / 2 +
                        CalculateRelevanceScore(log.GuildName ?? "", searchLower) / 2
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
                RelevanceScore = x.Score,
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
            ViewAllUrl = $"/CommandLogs?search={Uri.EscapeDataString(searchTerm)}"
        };
    }

    private async Task<SearchCategoryResult> SearchUsersAsync(string searchTerm, int maxResults, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching users for term: {SearchTerm}", searchTerm);

        var query = new UserSearchQueryDto
        {
            SearchTerm = searchTerm,
            Page = 1,
            PageSize = 100 // Get more results for better ranking
        };

        var users = await _userManagementService.GetUsersAsync(query, cancellationToken);
        var searchLower = searchTerm.ToLowerInvariant();

        var items = users.Items
            .Select(u => new
            {
                User = u,
                Score = CalculateRelevanceScore(u.Email, searchLower) +
                        CalculateRelevanceScore(u.DisplayName ?? "", searchLower) +
                        CalculateRelevanceScore(u.DiscordUsername ?? "", searchLower)
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
                BadgeVariant = GetRoleBadgeVariant(x.User.HighestRole),
                Url = $"/Admin/Users/Details?id={x.User.Id}",
                RelevanceScore = x.Score,
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


    private async Task<SearchCategoryResult> SearchCommandsAsync(string searchTerm, int maxResults, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching commands for term: {SearchTerm}", searchTerm);

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);
        var searchLower = searchTerm.ToLowerInvariant();

        // Flatten all commands from all modules
        var allCommands = modules.SelectMany(m => m.Commands).ToList();

        var items = allCommands
            .Select(cmd => new
            {
                Command = cmd,
                Score = CalculateRelevanceScore(cmd.FullName, searchLower) +
                        CalculateRelevanceScore(cmd.Name, searchLower) +
                        CalculateRelevanceScore(cmd.Description, searchLower) / 2 +
                        CalculateRelevanceScore(cmd.ModuleName, searchLower) / 2
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Command.FullName,
                Title = $"/{x.Command.FullName}",
                Subtitle = x.Command.ModuleName,
                Description = x.Command.Description,
                BadgeText = x.Command.ModuleName,
                BadgeVariant = "primary",
                Url = $"/Commands#cmd-{x.Command.FullName.Replace(" ", "-")}",
                RelevanceScore = x.Score,
                Metadata = new Dictionary<string, string>
                {
                    ["ParameterCount"] = x.Command.Parameters.Count.ToString(),
                    ["PreconditionCount"] = x.Command.Preconditions.Count.ToString(),
                    ["ModuleName"] = x.Command.ModuleName
                }
            })
            .ToList();

        return new SearchCategoryResult
        {
            Category = SearchCategory.Commands,
            DisplayName = "Commands",
            Items = items,
            TotalCount = items.Count,
            HasMore = allCommands.Count(cmd =>
                CalculateRelevanceScore(cmd.FullName, searchLower) +
                CalculateRelevanceScore(cmd.Name, searchLower) > 0) > maxResults,
            ViewAllUrl = $"/Commands?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
    private async Task<SearchCategoryResult> SearchPagesAsync(string searchTerm, int maxResults, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching pages for term: {SearchTerm}", searchTerm);

        var allPages = _pageMetadataService.SearchPages(searchTerm);
        var searchLower = searchTerm.ToLowerInvariant();

        // Check exact match for "Jump to" button
        var exactMatch = _pageMetadataService.FindExactMatch(searchTerm);

        // Filter pages based on user authorization
        var authorizedPages = new List<PageMetadataDto>();
        foreach (var page in allPages)
        {
            if (string.IsNullOrWhiteSpace(page.RequiredPolicy))
            {
                // No policy required, accessible to all authenticated users
                authorizedPages.Add(page);
            }
            else
            {
                // Check if user has required policy
                var authResult = await _authorizationService.AuthorizeAsync(user, page.RequiredPolicy);
                if (authResult.Succeeded)
                {
                    authorizedPages.Add(page);
                }
            }
        }

        var items = authorizedPages
            .Take(maxResults)
            .Select(p => new SearchResultItemDto
            {
                Id = p.Route,
                Title = p.Name,
                Subtitle = p.Section,
                Description = p.Description ?? string.Empty,
                BadgeText = p.Section ?? "Main",
                BadgeVariant = GetSectionBadgeVariant(p.Section),
                Url = p.Route,
                RelevanceScore = CalculateRelevanceScore(p.Name, searchLower),
                Metadata = new Dictionary<string, string>
                {
                    ["Section"] = p.Section ?? "Main",
                    ["isExactMatch"] = (exactMatch != null && exactMatch.Route == p.Route).ToString()
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
            ViewAllUrl = null // No "view all" page for navigation
        };
    }

    private string GetSectionBadgeVariant(string? section)
    {
        return section switch
        {
            "Main" => "primary",
            "Guild" => "success",
            "Admin" => "warning",
            "Performance" => "info",
            "Account" => "secondary",
            "Dev" => "dark",
            _ => "secondary"
        };
    }

    /// <summary>
    /// Calculates a relevance score for a field value against the search term.
    /// </summary>
    private double CalculateRelevanceScore(string fieldValue, string searchTermLower)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
            return 0;

        var fieldLower = fieldValue.ToLowerInvariant();

        if (fieldLower == searchTermLower)
            return ExactMatchScore;

        if (fieldLower.StartsWith(searchTermLower))
            return StartsWithScore;

        if (fieldLower.Contains(searchTermLower))
            return ContainsScore;

        return 0;
    }

    /// <summary>
    /// Determines which categories to search based on filter and authorization.
    /// </summary>
    private List<SearchCategory> DetermineCategoriesToSearch(SearchCategory? filter, bool canViewAdminCategories)
    {
        if (filter.HasValue)
        {
            // If specific category requested, check authorization
            if (IsAdminCategory(filter.Value) && !canViewAdminCategories)
                return new List<SearchCategory>();

            return new List<SearchCategory> { filter.Value };
        }

        // Search all currently implemented categories
        var categories = new List<SearchCategory>
        {
            SearchCategory.Guilds,
            SearchCategory.CommandLogs
        };

        // Add admin categories if authorized
        if (canViewAdminCategories)
        {
            categories.Add(SearchCategory.Users);
            // AuditLogs and MessageLogs will be added in Phase 3
        }

        // Pages are always searchable (filtered by authorization in SearchPagesAsync)
        categories.Add(SearchCategory.Pages);
        categories.Add(SearchCategory.Commands);

        return categories;
    }

    /// <summary>
    /// Checks if a category requires admin authorization.
    /// </summary>
    private bool IsAdminCategory(SearchCategory category)
    {
        return category switch
        {
            SearchCategory.Users => true,
            SearchCategory.AuditLogs => true,
            SearchCategory.MessageLogs => true,
            _ => false
        };
    }

    /// <summary>
    /// Creates an empty result for a category.
    /// </summary>
    private SearchCategoryResult CreateEmptyResult(SearchCategory category)
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

    /// <summary>
    /// Gets the display name for a category.
    /// </summary>
    private string GetCategoryDisplayName(SearchCategory category)
    {
        return category switch
        {
            SearchCategory.Guilds => "Guilds",
            SearchCategory.CommandLogs => "Command Logs",
            SearchCategory.Users => "Users",
            SearchCategory.Commands => "Commands",
            SearchCategory.AuditLogs => "Audit Logs",
            SearchCategory.MessageLogs => "Message Logs",
            SearchCategory.Pages => "Pages",
            _ => category.ToString()
        };
    }

    /// <summary>
    /// Gets the view all URL for a category.
    /// </summary>
    private string? GetCategoryViewAllUrl(SearchCategory category)
    {
        return category switch
        {
            SearchCategory.Guilds => "/Guilds",
            SearchCategory.CommandLogs => "/CommandLogs",
            SearchCategory.Users => "/Admin/Users",
            SearchCategory.Commands => "/Commands",
            SearchCategory.AuditLogs => "/Admin/AuditLogs",
            SearchCategory.MessageLogs => "/Admin/MessageLogs",
            SearchCategory.Pages => null,
            _ => null
        };
    }

    /// <summary>
    /// Gets the badge variant for a user role.
    /// </summary>
    private string GetRoleBadgeVariant(string role)
    {
        return role switch
        {
            "SuperAdmin" => "danger",
            "Admin" => "warning",
            "Moderator" => "info",
            "Viewer" => "success",
            _ => "secondary"
        };
    }
}
