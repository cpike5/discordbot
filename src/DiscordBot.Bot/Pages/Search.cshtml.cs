using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// Page model for the unified search page.
/// Searches across Guilds, Command Logs, and Users (Admin+ only).
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class SearchModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IUserManagementService _userManagementService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<SearchModel> _logger;

    public SearchModel(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IUserManagementService userManagementService,
        IAuthorizationService authorizationService,
        ILogger<SearchModel> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _userManagementService = userManagementService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Search term from the query string.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// The view model containing all search results.
    /// </summary>
    public SearchResultsViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Return early with empty results if search term is empty or whitespace
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            _logger.LogDebug("Search page accessed with empty search term");
            ViewModel = new SearchResultsViewModel
            {
                SearchTerm = string.Empty,
                CanViewUsers = false
            };
            return Page();
        }

        _logger.LogInformation("User {UserId} searching for term: {SearchTerm}", User.Identity?.Name, SearchTerm);

        // Check if user has permission to view user results (Admin+ only)
        var canViewUsers = (await _authorizationService.AuthorizeAsync(User, "RequireAdmin")).Succeeded;

        // Execute searches in parallel for performance
        var guildSearchTask = SearchGuildsAsync(SearchTerm, cancellationToken);
        var commandLogSearchTask = SearchCommandLogsAsync(SearchTerm, cancellationToken);
        var userSearchTask = canViewUsers
            ? SearchUsersAsync(SearchTerm, cancellationToken)
            : Task.FromResult((Results: Array.Empty<UserSearchResultItem>(), TotalCount: 0));

        await Task.WhenAll(guildSearchTask, commandLogSearchTask, userSearchTask);

        var (guildResults, guildTotalCount) = await guildSearchTask;
        var (commandLogResults, commandLogTotalCount) = await commandLogSearchTask;
        var (userResults, userTotalCount) = await userSearchTask;

        ViewModel = new SearchResultsViewModel
        {
            SearchTerm = SearchTerm,
            CanViewUsers = canViewUsers,
            GuildResults = guildResults,
            TotalGuildResults = guildTotalCount,
            CommandLogResults = commandLogResults,
            TotalCommandLogResults = commandLogTotalCount,
            UserResults = userResults,
            TotalUserResults = userTotalCount
        };

        _logger.LogInformation("Search completed. Found {GuildCount} guilds, {CommandLogCount} command logs, {UserCount} users",
            guildResults.Length, commandLogResults.Length, userResults.Length);

        return Page();
    }

    /// <summary>
    /// Searches guilds by name or ID.
    /// </summary>
    private async Task<(GuildSearchResultItem[] Results, int TotalCount)> SearchGuildsAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GuildSearchQueryDto
            {
                SearchTerm = searchTerm,
                Page = 1,
                PageSize = 5,
                SortBy = "Name",
                SortDescending = false
            };

            var paginatedResponse = await _guildService.GetGuildsAsync(query, cancellationToken);

            var results = paginatedResponse.Items
                .Select(g => new GuildSearchResultItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    IconUrl = g.IconUrl,
                    MemberCount = g.MemberCount,
                    IsActive = g.IsActive
                })
                .ToArray();

            _logger.LogDebug("Guild search returned {Count} results out of {Total} total", results.Length, paginatedResponse.TotalCount);

            return (results, paginatedResponse.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching guilds for term: {SearchTerm}", searchTerm);
            return (Array.Empty<GuildSearchResultItem>(), 0);
        }
    }

    /// <summary>
    /// Searches command logs by command name, username, or guild name.
    /// </summary>
    private async Task<(CommandLogSearchResultItem[] Results, int TotalCount)> SearchCommandLogsAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new CommandLogQueryDto
            {
                SearchTerm = searchTerm,
                Page = 1,
                PageSize = 5
            };

            var paginatedResponse = await _commandLogService.GetLogsAsync(query, cancellationToken);

            var results = paginatedResponse.Items
                .Select(log => new CommandLogSearchResultItem
                {
                    Id = log.Id,
                    CommandName = log.CommandName,
                    ExecutedAt = log.ExecutedAt,
                    GuildName = log.GuildName,
                    UserIdentifier = log.Username ?? log.UserId.ToString(),
                    Success = log.Success
                })
                .ToArray();

            _logger.LogDebug("Command log search returned {Count} results out of {Total} total", results.Length, paginatedResponse.TotalCount);

            return (results, paginatedResponse.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching command logs for term: {SearchTerm}", searchTerm);
            return (Array.Empty<CommandLogSearchResultItem>(), 0);
        }
    }

    /// <summary>
    /// Searches users by email or display name (Admin+ only).
    /// </summary>
    private async Task<(UserSearchResultItem[] Results, int TotalCount)> SearchUsersAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new UserSearchQueryDto
            {
                SearchTerm = searchTerm,
                Page = 1,
                PageSize = 5,
                SortBy = "Email",
                SortDescending = false
            };

            var paginatedResponse = await _userManagementService.GetUsersAsync(query, cancellationToken);

            var results = paginatedResponse.Items
                .Select(u => new UserSearchResultItem
                {
                    Id = u.Id,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    Role = u.HighestRole,
                    AvatarUrl = u.DiscordAvatarUrl,
                    IsActive = u.IsActive
                })
                .ToArray();

            _logger.LogDebug("User search returned {Count} results out of {Total} total", results.Length, paginatedResponse.TotalCount);

            return (results, paginatedResponse.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users for term: {SearchTerm}", searchTerm);
            return (Array.Empty<UserSearchResultItem>(), 0);
        }
    }
}
