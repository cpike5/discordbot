using DiscordBot.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.AuditLogs;

/// <summary>
/// The old audit log address. The list now lives on the unified Logs page (the Audit tab), so this
/// page only forwards there and keeps the filters of old links and bookmarks.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public AuditLogCategory? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditLogAction? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TargetType { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserTimezone { get; set; }

    /// <summary>
    /// Forwards to the Audit tab of the Logs page.
    /// </summary>
    public IActionResult OnGet()
    {
        return RedirectToPage("/Admin/Logs/Index", new { tab = "audit", UserTimezone });
    }

    /// <summary>
    /// Forwards an old export link to the Logs page's streaming export, with the same filters.
    /// </summary>
    public IActionResult OnGetExport()
    {
        return RedirectToPage("/Admin/Logs/Index", "Export", new
        {
            Category = Category.HasValue ? (int?)Category.Value : null,
            Action = Action.HasValue ? (int?)Action.Value : null,
            ActorId,
            TargetType,
            AuditGuildId = GuildId,
            AuditStartDate = StartDate?.ToString("yyyy-MM-dd"),
            AuditEndDate = EndDate?.ToString("yyyy-MM-dd"),
            AuditSearchTerm = SearchTerm,
            UserTimezone
        });
    }
}
