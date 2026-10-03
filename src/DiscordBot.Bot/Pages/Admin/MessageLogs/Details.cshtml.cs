using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.MessageLogs;

/// <summary>
/// Page model for displaying detailed information about a single message log entry.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class DetailsModel : PageModel
{
    private readonly IMessageLogService _messageLogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IMessageLogService messageLogService,
        ILogger<DetailsModel> logger)
    {
        _messageLogService = messageLogService;
        _logger = logger;
    }

    public MessageLogDetailViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Where Back goes: the Messages tab with the filters and page the user came from.
    /// </summary>
    public string ReturnUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(long id, string? returnUrl)
    {
        _logger.LogDebug("Loading message log details for ID: {MessageLogId}", id);

        // The return URL lands in an href, so only same-site paths are accepted
        ReturnUrl = ReturnUrlHelper.Sanitize(returnUrl, Url.Page("/Admin/Logs/Index", new { tab = "messages" }) ?? "/Admin/Logs?tab=messages");

        var message = await _messageLogService.GetByIdAsync(id);

        if (message == null)
        {
            _logger.LogWarning("Message log not found: {MessageLogId}", id);
            return NotFound();
        }

        _logger.LogInformation("Retrieved message log {MessageLogId} (Discord message {DiscordMessageId} from author {AuthorId})",
            id, message.DiscordMessageId, message.AuthorId);

        ViewModel = new MessageLogDetailViewModel
        {
            Message = message
        };

        return Page();
    }
}
