using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.FeatureRequests;
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
    private readonly IFeatureRequestDocGenQueue _docGenQueue;
    private readonly IGuildService _guildService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IFeatureRequestService service,
        IFeatureRequestDocGenQueue docGenQueue,
        IGuildService guildService,
        ILogger<DetailsModel> logger)
    {
        _service = service;
        _docGenQueue = docGenQueue;
        _guildService = guildService;
        _logger = logger;
    }

    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;

    public FeatureRequest? Item { get; private set; }

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
                new() { Label = "Feature Requests", Url = $"/Guilds/{guildId}/FeatureRequests" },
                new() { Label = "Details", IsCurrent = true }
            }
        };

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin approving feature request {RequestId} in guild {GuildId}", id, guildId);

        var reviewerId = GetCurrentDiscordUserId();
        await _service.UpdateStatusAsync(id, FeatureRequestStatus.Approved, reviewerId, ReviewNotes);

        TempData["SuccessMessage"] = "Feature request approved.";
        return RedirectToPage(new { guildId });
    }

    public async Task<IActionResult> OnPostRejectAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin rejecting feature request {RequestId} in guild {GuildId}", id, guildId);

        var reviewerId = GetCurrentDiscordUserId();
        await _service.UpdateStatusAsync(id, FeatureRequestStatus.Rejected, reviewerId, ReviewNotes);

        TempData["SuccessMessage"] = "Feature request rejected.";
        return RedirectToPage(new { guildId });
    }

    public async Task<IActionResult> OnPostRetryDocGenAsync(ulong guildId, Guid id)
    {
        _logger.LogInformation("Admin retrying doc gen for feature request {RequestId} in guild {GuildId}", id, guildId);

        var item = await _service.GetByIdAsync(id);
        if (item == null || item.GuildId != guildId)
            return NotFound();

        if (item.Status != FeatureRequestStatus.DocGenFailed)
        {
            TempData["ErrorMessage"] = "Doc gen retry is only available for requests with a failed doc gen status.";
            return RedirectToPage("Details", new { id, guildId });
        }

        await _service.UpdateStatusAsync(id, FeatureRequestStatus.Submitted, null, null);
        _docGenQueue.Enqueue(id);

        TempData["SuccessMessage"] = "Doc generation re-queued.";
        return RedirectToPage("Details", new { id, guildId });
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
