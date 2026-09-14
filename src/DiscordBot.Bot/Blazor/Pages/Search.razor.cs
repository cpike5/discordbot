using System.Security.Claims;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Search.cshtml</c> +
/// <c>Search.cshtml.cs</c>'s <c>SearchModel</c> (docs/plans/blazor-port-plan.md §5 Phase 3,
/// "Search with Highlight as the first interactive page"). Reproduces <c>SearchModel</c>'s logic:
/// same empty/short-term handling, the same <see cref="ISearchService"/> call and DTO-to-view-model
/// mapping, and the same <c>RequireAdmin</c> gate for <see cref="ViewModel"/>'s admin-only
/// sections. <c>SearchModel</c>'s direct <c>DiscordSocketClient</c> guild-intersection is behind
/// <see cref="IUserGuildSelectorService"/> instead (see that interface's doc comment for why).
/// </summary>
/// <remarks>
/// Resolves once per <see cref="Q"/> and persists the result across the prerender-to-circuit
/// boundary with <see cref="PersistentComponentState"/>, the same pattern
/// <c>Blazor/Guilds/GuildPageBase.cs</c> uses for a guild id - here keyed by the search term
/// instead of a guild id, since this page has no route parameter.
/// </remarks>
public partial class Search : ComponentBase, IDisposable
{
    /// <summary>The search term from the query string (<c>?q=</c>), matching the legacy page's <c>[BindProperty(Name = "q")]</c>.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "q")]
    public string? Q { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private ISearchService SearchService { get; set; } = default!;

    [Inject]
    private IAuthorizationService AuthorizationService { get; set; } = default!;

    [Inject]
    private IUserGuildSelectorService UserGuildSelectorService { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Search> Logger { get; set; } = default!;

    /// <summary>The mapped results for the current <see cref="Q"/> - empty until the first search resolves.</summary>
    protected SearchResultsViewModel ViewModel { get; private set; } = new();

    /// <summary>Guild selector items for guild-scoped page results, intersected with the bot's active guilds.</summary>
    protected IReadOnlyList<GuildSelectorItem> UserGuilds { get; private set; } = [];

    /// <summary>True while the initial (or a term-changed) search is in flight.</summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>The search box's live text, seeded from <see cref="Q"/> and submitted back onto the query string on search.</summary>
    protected string? SearchBoxValue { get; set; }

    private PersistingComponentStateSubscription _persistingSubscription;
    private string? _resolvedTerm;

    private string PersistenceKey => $"Search.Results.{Q}";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);
        SearchBoxValue = Q;

        if (ApplicationState.TryTakeFromJson<SearchPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            ViewModel = restored.ViewModel;
            UserGuilds = restored.UserGuilds;
            _resolvedTerm = Q;
            IsLoading = false;
        }
        else
        {
            await SearchAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedTerm != Q)
        {
            SearchBoxValue = Q;
            await SearchAsync();
        }
    }

    /// <summary>Navigates to <c>/Search?q=...</c> so the URL stays shareable, matching the navbar's own GET form (<c>MainNavbar.razor</c>).</summary>
    protected void HandleSearchSubmit()
    {
        var term = SearchBoxValue?.Trim() ?? string.Empty;
        NavigationManager.NavigateTo(string.IsNullOrEmpty(term) ? "/Search" : $"/Search?q={Uri.EscapeDataString(term)}");
    }

    private async Task SearchAsync()
    {
        IsLoading = true;
        _resolvedTerm = Q;

        if (string.IsNullOrWhiteSpace(Q) || Q.Trim().Length < 2)
        {
            Logger.LogDebug("Search page accessed with empty or too-short search term");
            ViewModel = new SearchResultsViewModel
            {
                SearchTerm = Q?.Trim() ?? string.Empty,
                CanViewUsers = false,
                ValidationMessage = string.IsNullOrWhiteSpace(Q) ? null : "Please enter at least 2 characters to search."
            };
            UserGuilds = [];
            IsLoading = false;
            return;
        }

        var user = await GetUserAsync();
        Logger.LogDebug("User {UserId} performed a search", user.Identity?.Name);

        var canViewUsers = (await AuthorizationService.AuthorizeAsync(user, "RequireAdmin")).Succeeded;

        var searchQuery = new SearchQueryDto
        {
            SearchTerm = Q,
            MaxResultsPerCategory = 5,
            CategoryFilter = null // Search all categories
        };

        var unifiedResult = await SearchService.SearchAsync(searchQuery, user, CancellationToken.None);

        ViewModel = new SearchResultsViewModel
        {
            SearchTerm = unifiedResult.SearchTerm,
            CanViewUsers = canViewUsers,

            GuildResults = unifiedResult.Guilds.Items
                .Select(MapToGuildSearchResultItem)
                .Where(x => x != null)
                .ToArray()!,
            TotalGuildResults = unifiedResult.Guilds.TotalCount,

            CommandLogResults = unifiedResult.CommandLogs.Items
                .Select(MapToCommandLogSearchResultItem)
                .Where(x => x != null)
                .ToArray()!,
            TotalCommandLogResults = unifiedResult.CommandLogs.TotalCount,

            UserResults = unifiedResult.Users.Items
                .Select(MapToUserSearchResultItem)
                .ToArray(),
            TotalUserResults = unifiedResult.Users.TotalCount,

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

        UserGuilds = ViewModel.Pages.Any(p => p.RequiresGuildContext)
            ? await UserGuildSelectorService.GetUserGuildsAsync(user, CancellationToken.None)
            : [];

        Logger.LogInformation(
            "Search completed. Found {TotalResults} total results across all categories",
            unifiedResult.TotalResultCount);

        IsLoading = false;
    }

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new SearchPersistedState(ViewModel, UserGuilds));
        return Task.CompletedTask;
    }

    /// <summary>Maps a <see cref="SearchResultItemDto"/> to <see cref="GuildSearchResultItem"/> for backward compatibility.</summary>
    private GuildSearchResultItem? MapToGuildSearchResultItem(SearchResultItemDto dto)
    {
        if (!ulong.TryParse(dto.Id, out var id))
        {
            Logger.LogWarning("Skipping guild search result with malformed ID: {Id}", dto.Id);
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

    /// <summary>Maps a <see cref="SearchResultItemDto"/> to <see cref="CommandLogSearchResultItem"/> for backward compatibility.</summary>
    private CommandLogSearchResultItem? MapToCommandLogSearchResultItem(SearchResultItemDto dto)
    {
        if (!Guid.TryParse(dto.Id, out var id))
        {
            Logger.LogWarning("Skipping command log search result with malformed ID: {Id}", dto.Id);
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

    /// <summary>Maps a <see cref="SearchResultItemDto"/> to <see cref="UserSearchResultItem"/> for backward compatibility.</summary>
    private static UserSearchResultItem MapToUserSearchResultItem(SearchResultItemDto dto) => new()
    {
        Id = dto.Id,
        Email = dto.Subtitle ?? "",
        DisplayName = dto.Title,
        Role = dto.BadgeText ?? "Viewer",
        AvatarUrl = dto.IconUrl,
        IsActive = dto.Metadata.TryGetValue("IsActive", out var isActive) && bool.TryParse(isActive, out var active) && active
    };

    /// <summary>Maps a <c>SearchResultItemDto.BadgeVariant</c> string to <see cref="BadgeVariant"/>.</summary>
    protected static BadgeVariant ParseBadgeVariant(string? variant) => variant switch
    {
        "primary" => BadgeVariant.Blue,
        "success" => BadgeVariant.Success,
        "warning" => BadgeVariant.Warning,
        "danger" => BadgeVariant.Error,
        "info" => BadgeVariant.Info,
        "secondary" => BadgeVariant.Default,
        _ => BadgeVariant.Default
    };

    // Hand-written equivalents of the legacy page's asp-page/asp-route-* tag helpers - there's no
    // tag helper in a component, so these build the same URLs directly. Guilds/Index, Guilds/Details
    // and Admin/Users/Index/Details aren't ported yet (still Razor Pages), so these stay plain
    // hrefs rather than NavigationManager.NavigateTo calls.
    // Deviation: the legacy markup passed guild/user ids via asp-route-id, but Guilds/Details's
    // route template is "{guildId:long}" (Guilds/Index.cshtml itself links with asp-route-guildId)
    // and Commands/Index's tab param binds to "ActiveTab", not "tab" - both are corrected here
    // rather than reproduced, since the corrected URLs are what those pages actually read.
    protected static string GuildIndexUrl(string searchTerm) => $"/Guilds?SearchTerm={Uri.EscapeDataString(searchTerm)}";

    protected static string GuildDetailsUrl(ulong guildId) => $"/Guilds/Details/{guildId}";

    protected static string CommandLogsViewAllUrl(string searchTerm) => $"/Commands?ActiveTab=logs&SearchTerm={Uri.EscapeDataString(searchTerm)}";

    protected static string CommandLogDetailsUrl(Guid id) => $"/CommandLogs/Details/{id}";

    protected static string UsersIndexUrl(string searchTerm) => $"/Admin/Users?SearchTerm={Uri.EscapeDataString(searchTerm)}";

    protected static string UserDetailsUrl(string id) => $"/Admin/Users/Details?id={Uri.EscapeDataString(id)}";

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Persisted shape for the prerender-to-circuit round trip - the mapped view model plus the guild selector list, keyed by search term (see <see cref="PersistenceKey"/>).</summary>
    private sealed record SearchPersistedState(SearchResultsViewModel ViewModel, IReadOnlyList<GuildSelectorItem> UserGuilds);
}
