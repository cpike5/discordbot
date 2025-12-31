using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Members;

/// <summary>
/// Page model for displaying the guild member directory with search, filter, sort, and pagination.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class IndexModel : PageModel
{
    private readonly IGuildMemberService _memberService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildMemberService memberService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _memberService = memberService;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The Discord guild snowflake ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Search term for filtering by username, display name, or user ID.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by role IDs (comma-separated in query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public List<ulong>? RoleFilter { get; set; }

    /// <summary>
    /// Filter by join date start (inclusive).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedAfter { get; set; }

    /// <summary>
    /// Filter by join date end (inclusive).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedBefore { get; set; }

    /// <summary>
    /// Filter by activity status.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ActivityFilter { get; set; }

    /// <summary>
    /// Field to sort by (JoinedAt, Username, LastActiveAt).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "JoinedAt";

    /// <summary>
    /// Sort in descending order if true.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// The view model containing member list data.
    /// </summary>
    public MemberDirectoryViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// The guild information.
    /// </summary>
    public GuildDto? Guild { get; set; }

    /// <summary>
    /// Available roles for the filter dropdown.
    /// </summary>
    public List<GuildRoleDto> AvailableRoles { get; set; } = new();

    /// <summary>
    /// Total member count (unfiltered) for the badge.
    /// </summary>
    public int TotalMemberCount { get; set; }

    /// <summary>
    /// Whether any filters are currently active.
    /// </summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        (RoleFilter?.Any() ?? false) ||
        JoinedAfter.HasValue ||
        JoinedBefore.HasValue ||
        !string.IsNullOrWhiteSpace(ActivityFilter);

    /// <summary>
    /// Count of active filters for the badge.
    /// </summary>
    public int ActiveFilterCount
    {
        get
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(SearchTerm)) count++;
            if (RoleFilter?.Any() ?? false) count++;
            if (JoinedAfter.HasValue) count++;
            if (JoinedBefore.HasValue) count++;
            if (!string.IsNullOrWhiteSpace(ActivityFilter)) count++;
            return count;
        }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User accessing member directory for guild {GuildId}. Search={Search}, Sort={Sort}, Page={Page}",
            GuildId, SearchTerm, SortBy, CurrentPage);

        // Get guild info
        Guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (Guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        // Get available roles from Discord
        var discordGuild = _discordClient.GetGuild(GuildId);
        if (discordGuild != null)
        {
            AvailableRoles = discordGuild.Roles
                .Where(r => !r.IsEveryone && !r.IsManaged)
                .OrderByDescending(r => r.Position)
                .Select(r => new GuildRoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Color = r.Color.RawValue,
                    Position = r.Position
                })
                .ToList();
        }

        // Build the query
        var query = new GuildMemberQueryDto
        {
            SearchTerm = SearchTerm,
            RoleIds = RoleFilter,
            JoinedAtStart = JoinedAfter,
            JoinedAtEnd = JoinedBefore?.AddDays(1).AddSeconds(-1), // Include entire end day
            SortBy = SortBy,
            SortDescending = SortDescending,
            Page = CurrentPage,
            PageSize = PageSize,
            IsActive = true
        };

        // Apply activity filter
        if (!string.IsNullOrWhiteSpace(ActivityFilter))
        {
            var now = DateTime.UtcNow;
            switch (ActivityFilter)
            {
                case "active-today":
                    query.LastActiveAtStart = now.Date;
                    break;
                case "active-week":
                    query.LastActiveAtStart = now.AddDays(-7);
                    break;
                case "active-month":
                    query.LastActiveAtStart = now.AddDays(-30);
                    break;
                case "inactive-week":
                    query.LastActiveAtEnd = now.AddDays(-7);
                    break;
                case "inactive-month":
                    query.LastActiveAtEnd = now.AddDays(-30);
                    break;
                case "never-messaged":
                    query.LastActiveAtEnd = null;
                    // Need to indicate "never messaged" - we'll handle this specially
                    break;
            }
        }

        var result = await _memberService.GetMembersAsync(GuildId, query, cancellationToken);

        // Get total unfiltered count for badge
        TotalMemberCount = await _memberService.GetMemberCountAsync(
            GuildId,
            new GuildMemberQueryDto { IsActive = true },
            cancellationToken);

        // Build view model
        ViewModel = new MemberDirectoryViewModel
        {
            GuildId = GuildId,
            GuildName = Guild.Name,
            Members = result.Items.Select(MapToListItem).ToList(),
            TotalCount = result.TotalCount,
            TotalMemberCount = TotalMemberCount,
            CurrentPage = result.Page,
            TotalPages = result.TotalPages,
            PageSize = result.PageSize,
            HasActiveFilters = HasActiveFilters,
            ActiveFilterCount = ActiveFilterCount,
            SearchTerm = SearchTerm,
            SelectedRoles = RoleFilter ?? new List<ulong>(),
            JoinedAfter = JoinedAfter,
            JoinedBefore = JoinedBefore,
            ActivityFilter = ActivityFilter,
            SortBy = SortBy,
            SortDescending = SortDescending,
            AvailableRoles = AvailableRoles
        };

        return Page();
    }

    /// <summary>
    /// Maps a GuildMemberDto to a MemberListItemViewModel.
    /// </summary>
    private static MemberListItemViewModel MapToListItem(GuildMemberDto dto)
    {
        // Build avatar URL from hash
        string? avatarUrl = null;
        if (!string.IsNullOrEmpty(dto.AvatarHash))
        {
            var extension = dto.AvatarHash.StartsWith("a_") ? "gif" : "png";
            avatarUrl = $"https://cdn.discordapp.com/avatars/{dto.UserId}/{dto.AvatarHash}.{extension}?size=80";
        }

        return new MemberListItemViewModel
        {
            UserId = dto.UserId,
            DisplayName = dto.DisplayName,
            Username = dto.Username,
            Nickname = dto.Nickname,
            GlobalDisplayName = dto.GlobalDisplayName,
            AvatarUrl = avatarUrl,
            JoinedAt = dto.JoinedAt,
            LastActiveAt = dto.LastActiveAt,
            AccountCreatedAt = dto.AccountCreatedAt,
            Roles = dto.Roles.OrderByDescending(r => r.Position).Select(r => new RoleViewModel
            {
                Id = r.Id,
                Name = r.Name,
                ColorHex = r.Color > 0 ? $"#{r.Color:X6}" : "#99aab5"
            }).ToList()
        };
    }

    /// <summary>
    /// Sort options for the dropdown.
    /// </summary>
    public static readonly Dictionary<string, string> SortOptions = new()
    {
        { "JoinedAt", "Join Date" },
        { "Username", "Username" },
        { "LastActiveAt", "Last Active" }
    };

    /// <summary>
    /// Activity filter options for the dropdown.
    /// </summary>
    public static readonly Dictionary<string, string> ActivityOptions = new()
    {
        { "", "All Members" },
        { "active-today", "Active Today" },
        { "active-week", "Active This Week" },
        { "active-month", "Active This Month" },
        { "inactive-week", "Inactive 7+ Days" },
        { "inactive-month", "Inactive 30+ Days" },
        { "never-messaged", "Never Messaged" }
    };
}
