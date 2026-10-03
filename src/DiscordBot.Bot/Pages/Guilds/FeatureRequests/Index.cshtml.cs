using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.FeatureRequests;

/// <summary>
/// Page model for the Feature Requests list page.
/// Displays feature request submissions for a guild with status filtering and pagination.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IFeatureRequestService _service;
    private readonly IGuildService _guildService;
    private readonly IDiscordUserResolver _userResolver;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IFeatureRequestService service,
        IGuildService guildService,
        IDiscordUserResolver userResolver,
        ILogger<IndexModel> logger)
    {
        _service = service;
        _guildService = guildService;
        _userResolver = userResolver;
        _logger = logger;
    }

    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public FeatureRequestStatus? StatusFilter { get; set; }

    /// <summary>
    /// The page number. Bound from <c>pageNumber</c>: <c>page</c> is reserved by Razor Pages for the
    /// page route, so a link built with it always landed on page 1.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    public IEnumerable<FeatureRequest> Items { get; private set; } = [];

    /// <summary>The submitter's name for each request on the page, by Discord user id.</summary>
    public Dictionary<ulong, string> SubmitterNames { get; private set; } = new();

    public int Total { get; private set; }
    public int PageSize { get; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (PageNumber < 1) PageNumber = 1;

        GuildId = guildId;

        _logger.LogInformation(
            "User accessing Feature Requests list for guild {GuildId}, page {Page}, status filter {StatusFilter}",
            guildId, PageNumber, StatusFilter);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "feature-requests",
            "Feature Requests", $"Community feature requests for {guild.Name}");

        try
        {
            (Items, Total) = await _service.GetByGuildIdAsync(guildId, StatusFilter, PageNumber, PageSize);
            Items = Items.ToList();

            var users = await _userResolver.ResolveUsersAsync(Items.Select(i => i.SubmittedByUserId));
            SubmitterNames = users.ToDictionary(u => u.Key, u => UserDisplay.Name(u.Value.Username));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load feature requests for guild {GuildId}", guildId);
            Items = [];
            Total = 0;
            ErrorMessage = "The feature requests could not be loaded. Try again in a moment.";
        }

        return Page();
    }

    /// <summary>The submitter's name, or "Unknown user" when they could not be looked up.</summary>
    public string SubmitterName(ulong userId) =>
        SubmitterNames.TryGetValue(userId, out var name) ? name : UserDisplay.UnknownName;
}
