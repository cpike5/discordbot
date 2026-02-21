using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.CommandLogs;

/// <summary>
/// Page model for displaying command log details.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class DetailsModel : PageModel
{
    private readonly ICommandLogService _commandLogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ICommandLogService commandLogService,
        ILogger<DetailsModel> logger)
    {
        _commandLogService = commandLogService;
        _logger = logger;
    }

    /// <summary>
    /// The command log detail view model.
    /// </summary>
    public CommandLogDetailViewModel ViewModel { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing command log details for ID {Id}", id);

        var log = await _commandLogService.GetByIdAsync(id, cancellationToken);

        if (log is null)
        {
            _logger.LogWarning("Command log with ID {Id} not found", id);
            return NotFound();
        }

        ViewModel = CommandLogDetailViewModel.FromDto(log);

        return Page();
    }
}
