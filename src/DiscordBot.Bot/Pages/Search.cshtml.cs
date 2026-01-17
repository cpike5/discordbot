using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// Page model for the unified search page.
/// Searches across all categories using the centralized ISearchService.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class SearchModel : PageModel
{
    private readonly ISearchService _searchService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<SearchModel> _logger;

    public SearchModel(
        ISearchService searchService,
        IAuthorizationService authorizationService,
        ILogger<SearchModel> logger)
    {
        _searchService = searchService;
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

        // Check if user has permission to view admin categories
        var canViewUsers = (await _authorizationService.AuthorizeAsync(User, "RequireAdmin")).Succeeded;

        // Execute unified search using the new ISearchService
        var searchQuery = new SearchQueryDto
        {
            SearchTerm = SearchTerm,
            MaxResultsPerCategory = 5,
            CategoryFilter = null // Search all categories
        };

        var unifiedResult = await _searchService.SearchAsync(searchQuery, User, cancellationToken);

        // Map UnifiedSearchResultDto to SearchResultsViewModel
        ViewModel = new SearchResultsViewModel
        {
            SearchTerm = unifiedResult.SearchTerm,
            CanViewUsers = canViewUsers,

            // Map legacy Guilds category (backward compatibility)
            GuildResults = unifiedResult.Guilds.Items
                .Select(MapToGuildSearchResultItem)
                .ToArray(),
            TotalGuildResults = unifiedResult.Guilds.TotalCount,

            // Map legacy CommandLogs category (backward compatibility)
            CommandLogResults = unifiedResult.CommandLogs.Items
                .Select(MapToCommandLogSearchResultItem)
                .ToArray(),
            TotalCommandLogResults = unifiedResult.CommandLogs.TotalCount,

            // Map legacy Users category (backward compatibility)
            UserResults = unifiedResult.Users.Items
                .Select(MapToUserSearchResultItem)
                .ToArray(),
            TotalUserResults = unifiedResult.Users.TotalCount,

            // Map new categories using SearchResultItemDto
            Commands = unifiedResult.Commands.Items,
            TotalCommands = unifiedResult.Commands.TotalCount,
            CommandsViewAllUrl = unifiedResult.Commands.ViewAllUrl,

            AuditLogs = unifiedResult.AuditLogs.Items,
            TotalAuditLogs = unifiedResult.AuditLogs.TotalCount,
            AuditLogsViewAllUrl = unifiedResult.AuditLogs.ViewAllUrl,

            MessageLogs = unifiedResult.MessageLogs.Items,
            TotalMessageLogs = unifiedResult.MessageLogs.TotalCount,
            MessageLogsViewAllUrl = unifiedResult.MessageLogs.ViewAllUrl,

            Pages = unifiedResult.Pages.Items,
            TotalPages = unifiedResult.Pages.TotalCount,
            PagesViewAllUrl = unifiedResult.Pages.ViewAllUrl,

            Reminders = unifiedResult.Reminders.Items,
            TotalReminders = unifiedResult.Reminders.TotalCount,
            RemindersViewAllUrl = unifiedResult.Reminders.ViewAllUrl,

            ScheduledMessages = unifiedResult.ScheduledMessages.Items,
            TotalScheduledMessages = unifiedResult.ScheduledMessages.TotalCount,
            ScheduledMessagesViewAllUrl = unifiedResult.ScheduledMessages.ViewAllUrl
        };

        _logger.LogInformation("Search completed. Found {TotalResults} total results across all categories",
            unifiedResult.TotalResultCount);

        return Page();
    }

    /// <summary>
    /// Maps a SearchResultItemDto to GuildSearchResultItem for backward compatibility.
    /// </summary>
    private GuildSearchResultItem MapToGuildSearchResultItem(SearchResultItemDto dto)
    {
        return new GuildSearchResultItem
        {
            Id = ulong.Parse(dto.Id),
            Name = dto.Title,
            IconUrl = dto.IconUrl,
            MemberCount = dto.Metadata.TryGetValue("MemberCount", out var memberCount) && memberCount != "Unknown"
                ? int.Parse(memberCount)
                : null,
            IsActive = dto.BadgeText?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? false
        };
    }

    /// <summary>
    /// Maps a SearchResultItemDto to CommandLogSearchResultItem for backward compatibility.
    /// </summary>
    private CommandLogSearchResultItem MapToCommandLogSearchResultItem(SearchResultItemDto dto)
    {
        // Parse subtitle to extract username and guild name
        // Format: "{username} in {guildName}"
        var subtitle = dto.Subtitle ?? "";
        var parts = subtitle.Split(" in ", 2);
        var username = parts.Length > 0 ? parts[0] : "";
        var guildName = parts.Length > 1 ? parts[1] : null;

        return new CommandLogSearchResultItem
        {
            Id = Guid.Parse(dto.Id),
            CommandName = dto.Title.TrimStart('/'),
            ExecutedAt = dto.Timestamp ?? DateTime.UtcNow,
            GuildName = guildName == "DM" ? null : guildName,
            UserIdentifier = username,
            Success = dto.BadgeText?.Equals("Success", StringComparison.OrdinalIgnoreCase) ?? false
        };
    }

    /// <summary>
    /// Maps a SearchResultItemDto to UserSearchResultItem for backward compatibility.
    /// </summary>
    private UserSearchResultItem MapToUserSearchResultItem(SearchResultItemDto dto)
    {
        return new UserSearchResultItem
        {
            Id = dto.Id,
            Email = dto.Subtitle ?? "",
            DisplayName = dto.Title,
            Role = dto.BadgeText ?? "Viewer",
            AvatarUrl = dto.IconUrl,
            IsActive = dto.Metadata.TryGetValue("IsActive", out var isActive) && bool.Parse(isActive)
        };
    }
}
