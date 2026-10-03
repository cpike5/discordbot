using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.FeatureRequests;

/// <summary>
/// Page model for the Feature Request details page.
/// Displays full feature request details and provides admin review actions.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class DetailsModel : GuildPageModelBase
{
    private readonly IFeatureRequestService _service;
    private readonly IGuildService _guildService;
    private readonly IDiscordUserResolver _userResolver;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IFeatureRequestService service,
        IGuildService guildService,
        IDiscordUserResolver userResolver,
        ILogger<DetailsModel> logger)
    {
        _service = service;
        _guildService = guildService;
        _userResolver = userResolver;
        _logger = logger;
    }

    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;

    public FeatureRequest? Item { get; private set; }

    /// <summary>Who submitted the request, by name.</summary>
    public string SubmitterName { get; private set; } = UserDisplay.UnknownName;

    /// <summary>Who reviewed the request, by name; null while it has not been reviewed.</summary>
    public string? ReviewerName { get; private set; }

    [BindProperty]
    public string? ReviewNotes { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong guildId, Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Feature Request details for {RequestId} in guild {GuildId}", id, guildId);

        GuildId = guildId;

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        Item = await _service.GetByIdAsync(id);
        if (Item == null || Item.GuildId != guildId)
        {
            _logger.LogWarning("Feature request {RequestId} not found for guild {GuildId}", id, guildId);
            return NotFound();
        }

        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "feature-requests",
            "Feature Request Details", $"Request details for {guild.Name}");

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Feature Requests", Url = $"/Guilds/FeatureRequests/{guildId}" },
                new() { Label = "Details", IsCurrent = true }
            }
        };

        try
        {
            var ids = new List<ulong> { Item.SubmittedByUserId };
            if (Item.ReviewedByUserId.HasValue) ids.Add(Item.ReviewedByUserId.Value);

            var users = await _userResolver.ResolveUsersAsync(ids);
            if (users.TryGetValue(Item.SubmittedByUserId, out var submitter))
            {
                SubmitterName = UserDisplay.Name(submitter.Username);
            }

            if (Item.ReviewedByUserId.HasValue)
            {
                ReviewerName = users.TryGetValue(Item.ReviewedByUserId.Value, out var reviewer)
                    ? UserDisplay.Name(reviewer.Username)
                    : UserDisplay.UnknownName;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Names are a convenience; the request itself still renders
            _logger.LogWarning(ex, "Could not resolve names for feature request {RequestId}", id);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin approving feature request {RequestId} in guild {GuildId}", id, guildId);

        var item = await _service.GetByIdAsync(id);
        if (item == null || item.GuildId != guildId)
            return NotFound();

        var reviewerId = GetCurrentDiscordUserId();
        await _service.UpdateStatusAsync(id, FeatureRequestStatus.Approved, reviewerId, ReviewNotes);

        TempData.SetSuccessToast("Feature request approved.");
        return RedirectToPage(new { guildId, id });
    }

    public async Task<IActionResult> OnPostRejectAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin rejecting feature request {RequestId} in guild {GuildId}", id, guildId);

        var item = await _service.GetByIdAsync(id);
        if (item == null || item.GuildId != guildId)
            return NotFound();

        // Rejecting closes the request, so the record says why. The dialog asks for it; this is
        // the check for a request that did not come through the dialog.
        if (string.IsNullOrWhiteSpace(ReviewNotes))
        {
            TempData.SetErrorToast("Give a reason before rejecting a request.");
            return RedirectToPage(new { guildId, id });
        }

        var reviewerId = GetCurrentDiscordUserId();
        await _service.UpdateStatusAsync(id, FeatureRequestStatus.Rejected, reviewerId, ReviewNotes.Trim());

        TempData.SetSuccessToast("Feature request rejected.");
        return RedirectToPage(new { guildId, id });
    }

    /// <summary>
    /// Puts a request whose documentation run failed back in the queue. The documentation
    /// generator is a separate command-line run, so this does not generate anything itself.
    /// </summary>
    public async Task<IActionResult> OnPostRetryDocGenAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin re-queuing documentation for feature request {RequestId} in guild {GuildId}", id, guildId);

        var item = await _service.GetByIdAsync(id);
        if (item == null || item.GuildId != guildId)
            return NotFound();

        if (await _service.RequeueDocGenAsync(id))
        {
            TempData.SetSuccessToast("Queued for the next documentation run.");
        }
        else
        {
            TempData.SetErrorToast("This request is not waiting on a failed documentation run.");
        }

        return RedirectToPage(new { guildId, id });
    }

    /// <summary>
    /// Extracts the Discord user ID from the authenticated user's claims.
    /// Uses the "discord:user_id" claim set during OAuth login.
    /// </summary>
    private ulong? GetCurrentDiscordUserId()
    {
        var claim = User.FindFirst("discord:user_id");
        if (claim != null && ulong.TryParse(claim.Value, out var id))
            return id;
        return null;
    }
}
