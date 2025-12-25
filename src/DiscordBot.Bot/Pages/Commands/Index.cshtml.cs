using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Commands;

/// <summary>
/// Page model for displaying all registered command modules and their commands.
/// </summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="commandMetadataService">The command metadata service.</param>
    /// <param name="logger">The logger.</param>
    public IndexModel(
        ICommandMetadataService commandMetadataService,
        ILogger<IndexModel> logger)
    {
        _commandMetadataService = commandMetadataService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing command list data.
    /// </summary>
    public CommandsListViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Handles the GET request for the Commands page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing commands page");

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);

        ViewModel = CommandsListViewModel.FromDtos(modules);

        _logger.LogDebug(
            "Loaded {ModuleCount} modules with {CommandCount} total commands",
            ViewModel.ModuleCount,
            ViewModel.TotalCommandCount);
    }
}
