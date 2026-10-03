using DiscordBot.Bot.Helpers;
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

    /// <summary>
    /// Where Back goes: the page the person came from (a Commands view with its filters and
    /// page, or a search), or the Execution Logs tab. Always a same-site path, because it is
    /// written into an href.
    /// </summary>
    public string BackUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Builds the Back target for <paramref name="returnUrl"/>.
    /// </summary>
    public static string ResolveBackUrl(string? returnUrl, string logsTabUrl) =>
        ReturnUrlHelper.Sanitize(returnUrl, logsTabUrl);

    /// <summary>
    /// The address to go back to, passed by the page that linked here (<c>?returnUrl=</c>).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

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
        BackUrl = ResolveBackUrl(ReturnUrl, Url?.Page("/Commands/Index", new { tab = "execution-logs" }) ?? "/Commands?tab=execution-logs");

        return Page();
    }
}
