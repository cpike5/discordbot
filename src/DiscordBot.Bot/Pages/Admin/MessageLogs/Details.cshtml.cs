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

    public async Task<IActionResult> OnGetAsync(long id)
    {
        _logger.LogDebug("Loading message log details for ID: {MessageLogId}", id);

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
