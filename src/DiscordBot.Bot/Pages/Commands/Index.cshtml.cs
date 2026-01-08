using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Commands;

/// <summary>
/// Page model for displaying all registered command modules and their commands.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly ICommandRegistrationService _commandRegistrationService;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="commandMetadataService">The command metadata service.</param>
    /// <param name="commandRegistrationService">The command registration service.</param>
    /// <param name="logger">The logger.</param>
    public IndexModel(
        ICommandMetadataService commandMetadataService,
        ICommandRegistrationService commandRegistrationService,
        ILogger<IndexModel> logger)
    {
        _commandMetadataService = commandMetadataService;
        _commandRegistrationService = commandRegistrationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing command list data.
    /// </summary>
    public CommandsListViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the clear commands confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ClearCommandsModal { get; private set; } = null!;

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

        // Initialize modal
        ClearCommandsModal = new ConfirmationModalViewModel
        {
            Id = "clearCommandsModal",
            Title = "Clear & Re-register Commands Globally",
            Message = "This will clear all registered commands (global and guild-specific) and re-register them globally. Global commands may take up to 1 hour to propagate to all servers. Are you sure you want to continue?",
            ConfirmText = "Clear & Re-register",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "ClearAndRegisterGlobally"
        };
    }

    /// <summary>
    /// Handles POST requests to clear and re-register commands globally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON result indicating success or failure.</returns>
    public async Task<IActionResult> OnPostClearAndRegisterGloballyAsync(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to clear and re-register commands", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogWarning("Clear and re-register commands requested by user {UserId}", User.Identity?.Name);

        try
        {
            var result = await _commandRegistrationService.ClearAndRegisterGloballyAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Commands cleared and re-registered successfully by {UserId}: {CommandCount} commands, {GuildCount} guilds cleared",
                    User.Identity?.Name,
                    result.GlobalCommandsRegistered,
                    result.GuildsCleared);
            }

            return new JsonResult(new
            {
                success = result.Success,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear and re-register commands, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to clear and re-register commands. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }
}
