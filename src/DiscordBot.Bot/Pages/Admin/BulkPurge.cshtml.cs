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
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class BulkPurgeModel : PageModel
{
    private readonly IBulkPurgeService _bulkPurgeService;
    private readonly ILogger<BulkPurgeModel> _logger;

    public BulkPurgeModel(
        IBulkPurgeService bulkPurgeService,
        ILogger<BulkPurgeModel> logger)
    {
        _bulkPurgeService = bulkPurgeService;
        _logger = logger;
    }

    [BindProperty]
    public BulkPurgeEntityType EntityType { get; set; }

    [BindProperty]
    public DateTime? StartDate { get; set; }

    [BindProperty]
    public DateTime? EndDate { get; set; }

    [BindProperty]
    public string? GuildIdInput { get; set; }

    public BulkPurgePreviewDto? PreviewResult { get; set; }
    public BulkPurgeResultDto? PurgeResult { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // Default to no specific entity type selected
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        var criteria = BuildCriteria();
        if (criteria == null)
        {
            return Page();
        }

        _logger.LogDebug(
            "Preview requested for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
            criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

        PreviewResult = await _bulkPurgeService.PreviewPurgeAsync(criteria);

        if (!PreviewResult.Success)
        {
            ErrorMessage = PreviewResult.ErrorMessage ?? "Failed to generate preview.";
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

        PurgeResult = await _bulkPurgeService.ExecutePurgeAsync(criteria, adminUserId);

        if (PurgeResult.Success)
        {
            SuccessMessage = $"Successfully purged {PurgeResult.DeletedCount:N0} {PurgeResult.EntityType} records.";
            _logger.LogInformation(
                "Bulk purge completed: {DeletedCount} {EntityType} records deleted",
                PurgeResult.DeletedCount, PurgeResult.EntityType);
        }
        else
        {
            ErrorMessage = PurgeResult.ErrorMessage ?? "An error occurred during purge.";
            _logger.LogError(
                "Bulk purge failed for {EntityType}: {Error}",
                criteria.EntityType, PurgeResult.ErrorMessage);
        }

        return Page();
    }

    private BulkPurgeCriteriaDto? BuildCriteria()
    {
        // Validate entity type is selected (enum starts at 1, default 0 is invalid)
        if (!Enum.IsDefined(typeof(BulkPurgeEntityType), EntityType))
        {
            ModelState.AddModelError(nameof(EntityType), "Please select an entity type.");
            return null;
        }

        // Validate date range
        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            ModelState.AddModelError(nameof(StartDate), "Start date cannot be after end date.");
            return null;
        }

        ulong? guildId = null;
        if (!string.IsNullOrWhiteSpace(GuildIdInput))
        {
            if (!ulong.TryParse(GuildIdInput, out var parsedGuildId))
            {
                ModelState.AddModelError(nameof(GuildIdInput), "Invalid Guild ID format.");
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
