using System.Text.Json;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for bulk data purge operations.
/// <para>
/// The preview is a GET (the criteria live in the query string, so Refresh and Back just show
/// the preview again). The purge is a POST that always redirects (post/redirect/get): Refresh
/// after a purge loads the page, it never re-runs the purge. The outcome travels in TempData.
/// </para>
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class BulkPurgeModel : PageModel
{
    private const string ResultTempDataKey = "BulkPurgeResult";

    private readonly IBulkPurgeService _bulkPurgeService;
    private readonly IGuildService _guildService;
    private readonly ILogger<BulkPurgeModel> _logger;

    public BulkPurgeModel(
        IBulkPurgeService bulkPurgeService,
        IGuildService guildService,
        ILogger<BulkPurgeModel> logger)
    {
        _bulkPurgeService = bulkPurgeService;
        _guildService = guildService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public BulkPurgeEntityType EntityType { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GuildIdInput { get; set; }

    /// <summary>Set by the criteria form; asks for a preview of the criteria in the query string.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Preview { get; set; }

    public BulkPurgePreviewDto? PreviewResult { get; set; }

    /// <summary>The outcome of the purge the user just ran, shown once after the redirect.</summary>
    public BulkPurgeOutcome? Outcome { get; set; }

    /// <summary>The name of the server the preview is limited to, when the bot knows it (C-1).</summary>
    public string? GuildFilterName { get; set; }

    /// <summary>Page-state error when the preview could not be produced.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The summary of a finished purge, as it travels through TempData.</summary>
    public sealed record BulkPurgeOutcome(BulkPurgeEntityType EntityType, int DeletedCount, string? CorrelationId);

    public async Task<IActionResult> OnGetAsync()
    {
        Outcome = TakeOutcome();

        if (!Preview)
        {
            return Page();
        }

        var criteria = BuildCriteria();
        if (criteria == null)
        {
            return Page();
        }

        _logger.LogDebug(
            "Preview requested for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
            criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

        PreviewResult = await _bulkPurgeService.PreviewPurgeAsync(criteria);

        if (criteria.GuildId.HasValue)
        {
            GuildFilterName = (await _guildService.GetGuildByIdAsync(criteria.GuildId.Value))?.Name;
        }

        if (!PreviewResult.Success)
        {
            // The service's preview error carries exception text (already logged there), so show a plain sentence.
            ErrorMessage = "The preview could not be generated. Try again, and check the logs if it keeps failing.";
            PreviewResult = null;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostExecuteAsync()
    {
        var criteria = BuildCriteria();
        if (criteria == null)
        {
            return Page();
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        _logger.LogInformation(
            "Bulk purge execution requested by {AdminUserId} for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
            adminUserId, criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

        var result = await _bulkPurgeService.ExecutePurgeAsync(criteria, adminUserId);

        if (result.Success)
        {
            TempData[ResultTempDataKey] = JsonSerializer.Serialize(
                new BulkPurgeOutcome(result.EntityType, result.DeletedCount, result.AuditLogCorrelationId));
            _logger.LogInformation(
                "Bulk purge completed: {DeletedCount} {EntityType} records deleted",
                result.DeletedCount, result.EntityType);

            // Post/redirect/get: a refresh now repeats only this GET.
            return RedirectToPage();
        }

        // A failed transaction's message carries exception text; it is logged below, not shown.
        TempData.SetErrorToast(result.ErrorCode == BulkPurgeResultDto.TransactionFailed
            ? "An error occurred during the purge. Nothing was reported as deleted; check the logs."
            : result.ErrorMessage ?? "An error occurred during the purge.");
        _logger.LogError(
            "Bulk purge failed for {EntityType}: {Error}",
            criteria.EntityType, result.ErrorMessage);

        // Back to the same preview so the criteria are still there to retry.
        return RedirectToPage(new
        {
            Preview = true,
            EntityType = criteria.EntityType,
            StartDate = StartDate?.ToString("yyyy-MM-dd"),
            EndDate = EndDate?.ToString("yyyy-MM-dd"),
            GuildIdInput
        });
    }

    private BulkPurgeOutcome? TakeOutcome()
    {
        if (TempData[ResultTempDataKey] is not string json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BulkPurgeOutcome>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private BulkPurgeCriteriaDto? BuildCriteria()
    {
        // Validate entity type is selected (enum starts at 1, default 0 is invalid)
        if (!Enum.IsDefined(typeof(BulkPurgeEntityType), EntityType))
        {
            ModelState.AddModelError(nameof(EntityType), "Choose which records to purge.");
            return null;
        }

        // Validate date range
        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            ModelState.AddModelError(nameof(StartDate), "The start date cannot be after the end date.");
            return null;
        }

        ulong? guildId = null;
        if (!string.IsNullOrWhiteSpace(GuildIdInput))
        {
            if (!ulong.TryParse(GuildIdInput.Trim(), out var parsedGuildId))
            {
                ModelState.AddModelError(nameof(GuildIdInput), "The server ID must be a number, such as 123456789012345678.");
                return null;
            }
            guildId = parsedGuildId;
        }

        return new BulkPurgeCriteriaDto
        {
            EntityType = EntityType,
            StartDate = StartDate?.ToUniversalTime(),
            EndDate = EndDate?.ToUniversalTime(),
            GuildId = guildId
        };
    }
}
