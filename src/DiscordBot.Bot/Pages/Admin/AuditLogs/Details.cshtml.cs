using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.AuditLogs;

/// <summary>
/// Page model for displaying detailed information about a specific audit log entry.
/// Includes full entry details, formatted JSON viewer, and related entries by correlation ID.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class DetailsModel : PageModel
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IAuditLogService auditLogService, ILogger<DetailsModel> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing audit log details and related entries.
    /// </summary>
    public AuditLogDetailViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// Gets the return URL to preserve filter parameters when navigating back to the list.
    /// </summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>
    /// Handles GET requests to display audit log entry details.
    /// </summary>
    /// <param name="id">The unique identifier of the audit log entry to display.</param>
    /// <param name="returnUrl">Optional URL to return to (defaults to audit logs index page).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The page result, or NotFound if the entry does not exist.</returns>
    public async Task<IActionResult> OnGetAsync(long id, string? returnUrl, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading audit log details for entry ID {EntryId}", id);

        // Preserve return URL or default to index page
        ReturnUrl = returnUrl ?? Url.Page("Index") ?? "/Admin/AuditLogs";

        // Retrieve the audit log entry
        var log = await _auditLogService.GetByIdAsync(id, cancellationToken);
        if (log is null)
        {
            _logger.LogWarning("Audit log entry {EntryId} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Retrieved audit log entry {EntryId} - {Category}/{Action} by {Actor}",
            id, log.CategoryName, log.ActionName, log.ActorDisplayName ?? log.ActorId ?? "Unknown");

        // Get related entries by correlation ID if present
        IReadOnlyList<Core.DTOs.AuditLogDto>? relatedEntries = null;
        if (!string.IsNullOrEmpty(log.CorrelationId))
        {
            _logger.LogDebug("Loading related audit log entries for correlation ID {CorrelationId}", log.CorrelationId);
            relatedEntries = await _auditLogService.GetByCorrelationIdAsync(log.CorrelationId, cancellationToken);
            _logger.LogInformation("Found {Count} related audit log entries for correlation ID {CorrelationId}",
                relatedEntries.Count, log.CorrelationId);
        }

        // Build view model
        ViewModel = AuditLogDetailViewModel.FromDto(log, relatedEntries);

        return Page();
    }
}
