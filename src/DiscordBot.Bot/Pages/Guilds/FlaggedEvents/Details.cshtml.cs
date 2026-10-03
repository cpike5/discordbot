using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Page model for the Flagged Event details page.
/// Displays full event details, evidence, and the review actions.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class DetailsModel : GuildPageModelBase
{
    /// <summary>The longest outcome text the event record keeps.</summary>
    public const int MaxOutcomeLength = 2000;

    private readonly IFlaggedEventService _flaggedEventService;
    private readonly IGuildService _guildService;
    private readonly IGuildMemberService _memberService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IFlaggedEventService flaggedEventService,
        IGuildService guildService,
        IGuildMemberService memberService,
        ILogger<DetailsModel> logger)
    {
        _flaggedEventService = flaggedEventService;
        _guildService = guildService;
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// The guild ID from route parameter.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// The flagged event details.
    /// </summary>
    public FlaggedEventDto Event { get; set; } = null!;

    /// <summary>
    /// User's other flagged events for history.
    /// </summary>
    public IEnumerable<FlaggedEventDto> UserHistory { get; set; } = Array.Empty<FlaggedEventDto>();

    /// <summary>
    /// Whether the flagged user is still a member, so their moderation profile can be opened.
    /// </summary>
    public bool UserIsMember { get; set; }

    /// <summary>
    /// Handles GET requests to display the flagged event details.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="id">The event ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Flagged Event details for event {EventId} in guild {GuildId}",
            id, guildId);

        GuildId = guildId;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        // Get event details
        var eventDto = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (eventDto == null || eventDto.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for guild {GuildId}", id, guildId);
            return NotFound();
        }

        Event = eventDto;

        // Get user's other flagged events for history (limit to last 10, excluding current event)
        var userEvents = await _flaggedEventService.GetUserEventsAsync(guildId, Event.UserId, cancellationToken);
        UserHistory = userEvents
            .Where(e => e.Id != id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(10)
            .ToList();

        UserIsMember = await _memberService.GetMemberAsync(guildId, Event.UserId, cancellationToken) != null;

        _logger.LogDebug("Retrieved flagged event {EventId} details with {HistoryCount} user history items",
            id, UserHistory.Count());

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Moderation", Url = $"/Guilds/ModerationSettings/{guildId}" },
                new() { Label = "Flagged Events", Url = $"/Guilds/FlaggedEvents/{guildId}" },
                new() { Label = "Details", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Flagged Event Details", $"Event details for {guild.Name}");

        Navigation = BuildNavigation(guild.Id, "moderation");

        return Page();
    }

    /// <summary>Acknowledges the event: seen, still open.</summary>
    public Task<IActionResult> OnPostAcknowledgeAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
        => ReviewAsync(guildId, id, FlaggedEventReviewAction.Acknowledge, cancellationToken);

    /// <summary>Dismisses the event: closed as not needing action.</summary>
    public Task<IActionResult> OnPostDismissAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
        => ReviewAsync(guildId, id, FlaggedEventReviewAction.Dismiss, cancellationToken);

    /// <summary>
    /// Records what the moderator did in Discord as the event's outcome. Nothing is sent to Discord.
    /// </summary>
    public async Task<IActionResult> OnPostRecordOutcomeAsync(
        ulong guildId, Guid id, string? outcome, CancellationToken cancellationToken)
    {
        // Never record 0 as the reviewer: an admin without a linked Discord account has no ID
        if (!User.TryGetDiscordUserId(out var reviewerId))
        {
            TempData.SetErrorToast(FlaggedEventReviewRules.LinkDiscordMessage);
            return Back(guildId, id);
        }

        var text = outcome?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            TempData.SetErrorToast("Describe the outcome first.");
            return Back(guildId, id);
        }

        if (text.Length > MaxOutcomeLength)
        {
            TempData.SetErrorToast($"The outcome is too long. Keep it under {MaxOutcomeLength:N0} characters.");
            return Back(guildId, id);
        }

        var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            return NotFound();
        }

        if (!FlaggedEventReviewRules.CanRecordOutcome(existing.Status))
        {
            TempData.SetErrorToast("This event is already closed, so its outcome cannot be changed.");
            return Back(guildId, id);
        }

        try
        {
            await _flaggedEventService.TakeActionAsync(id, text, reviewerId, cancellationToken);
            TempData.SetSuccessToast("Outcome recorded.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record outcome for flagged event {EventId} in guild {GuildId}", id, guildId);
            TempData.SetErrorToast("Could not record the outcome. Try again.");
        }

        return Back(guildId, id);
    }

    private async Task<IActionResult> ReviewAsync(
        ulong guildId, Guid id, FlaggedEventReviewAction action, CancellationToken cancellationToken)
    {
        // Never record 0 as the reviewer: an admin without a linked Discord account has no ID
        if (!User.TryGetDiscordUserId(out var reviewerId))
        {
            TempData.SetErrorToast(FlaggedEventReviewRules.LinkDiscordMessage);
            return Back(guildId, id);
        }

        var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            return NotFound();
        }

        if (!FlaggedEventReviewRules.CanApply(action, existing.Status))
        {
            TempData.SetErrorToast($"This event is already {existing.Status.ToString().ToLowerInvariant()}.");
            return Back(guildId, id);
        }

        try
        {
            if (action == FlaggedEventReviewAction.Dismiss)
            {
                await _flaggedEventService.DismissEventAsync(id, reviewerId, cancellationToken);
                TempData.SetSuccessToast("Event dismissed.");
            }
            else
            {
                await _flaggedEventService.AcknowledgeEventAsync(id, reviewerId, cancellationToken);
                TempData.SetSuccessToast("Event acknowledged.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} flagged event {EventId} in guild {GuildId}", action, id, guildId);
            TempData.SetErrorToast("Could not update the event. Try again.");
        }

        return Back(guildId, id);
    }

    private IActionResult Back(ulong guildId, Guid id)
        => RedirectToPage("/Guilds/FlaggedEvents/Details", new { guildId, id });
}
