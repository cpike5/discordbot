using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<SearchModel> _logger;

    public SearchModel(
        ISearchService searchService,
        IAuthorizationService authorizationService,
        IUserDiscordGuildService userDiscordGuildService,
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient,
        ILogger<SearchModel> logger)
    {
        _searchService = searchService;
        _authorizationService = authorizationService;
        _userDiscordGuildService = userDiscordGuildService;
        _userManager = userManager;
        _discordClient = discordClient;
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

    /// <summary>
    /// Guild selector items for guild-scoped page results, intersected with bot's active guilds.
    /// </summary>
    public IReadOnlyList<GuildSelectorItem> UserGuilds { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Return early with empty results if search term is empty or whitespace
        if (string.IsNullOrWhiteSpace(SearchTerm) || SearchTerm.Trim().Length < 2)
        {
            _logger.LogDebug("Search page accessed with empty or too-short search term");
            ViewModel = new SearchResultsViewModel
            {
                SearchTerm = SearchTerm?.Trim() ?? string.Empty,
                CanViewUsers = false,
                ValidationMessage = string.IsNullOrWhiteSpace(SearchTerm) ? null : "Please enter at least 2 characters to search."
            };
            return Page();
        }

        _logger.LogDebug("User {UserId} performed a search", User.Identity?.Name);

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
                .Where(x => x != null)
                .ToArray()!,
            TotalGuildResults = unifiedResult.Guilds.TotalCount,

            // Map legacy CommandLogs category (backward compatibility)
            CommandLogResults = unifiedResult.CommandLogs.Items
                .Select(MapToCommandLogSearchResultItem)
                .Where(x => x != null)
                .ToArray()!,
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

        // Load user guilds if any page results require guild context
        if (ViewModel.Pages.Any(p => p.RequiresGuildContext))
        {
            UserGuilds = await LoadUserGuildsAsync(cancellationToken);
        }

        _logger.LogInformation("Search completed. Found {TotalResults} total results across all categories",
            unifiedResult.TotalResultCount);

        return Page();
    }

    private async Task<IReadOnlyList<GuildSelectorItem>> LoadUserGuildsAsync(CancellationToken cancellationToken)
    {
        var appUser = await _userManager.GetUserAsync(User);
        if (appUser == null)
            return [];

        var userGuilds = await _userDiscordGuildService.GetUserGuildsAsync(appUser.Id, cancellationToken);
        var botGuildIds = _discordClient.Guilds.Select(g => g.Id).ToHashSet();

        return userGuilds
            .Where(g => botGuildIds.Contains(g.GuildId))
            .Select(g => new GuildSelectorItem
            {
                GuildId = g.GuildId.ToString(),
                GuildName = g.GuildName,
                GuildIconUrl = g.GuildIconUrl
            })
            .OrderBy(g => g.GuildName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Maps a SearchResultItemDto to GuildSearchResultItem for backward compatibility.
    /// </summary>
    private GuildSearchResultItem? MapToGuildSearchResultItem(SearchResultItemDto dto)
    {
        if (!ulong.TryParse(dto.Id, out var id))
        {
            _logger.LogWarning("Skipping guild search result with malformed ID: {Id}", dto.Id);
            return null;
        }

        return new GuildSearchResultItem
        {
            Id = id,
            Name = dto.Title,
            IconUrl = dto.IconUrl,
            MemberCount = dto.Metadata.TryGetValue("MemberCount", out var memberCount) && memberCount != "Unknown"
                && int.TryParse(memberCount, out var count)
                ? count
                : null,
            IsActive = dto.BadgeText?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? false
        };
    }

    /// <summary>
    /// Maps a SearchResultItemDto to CommandLogSearchResultItem for backward compatibility.
    /// </summary>
    private CommandLogSearchResultItem? MapToCommandLogSearchResultItem(SearchResultItemDto dto)
    {
        if (!Guid.TryParse(dto.Id, out var id))
        {
            _logger.LogWarning("Skipping command log search result with malformed ID: {Id}", dto.Id);
            return null;
        }

        // Parse subtitle to extract username and guild name
        // Format: "{username} in {guildName}"
        var subtitle = dto.Subtitle ?? "";
        var parts = subtitle.Split(" in ", 2);
        var username = parts.Length > 0 ? parts[0] : "";
        var guildName = parts.Length > 1 ? parts[1] : null;

        return new CommandLogSearchResultItem
        {
            Id = id,
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
            IsActive = dto.Metadata.TryGetValue("IsActive", out var isActive) && bool.TryParse(isActive, out var active) && active
        };
    }
}
