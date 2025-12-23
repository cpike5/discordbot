using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Bot Control Panel.
/// Provides lifecycle management controls for the bot.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class BotControlModel : PageModel
{
    private readonly IBotService _botService;
    private readonly ILogger<BotControlModel> _logger;

    /// <summary>
    /// Gets the view model for the page.
    /// </summary>
    public BotControlViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the restart confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel RestartModal { get; private set; } = null!;

    /// <summary>
    /// Gets the shutdown typed confirmation modal configuration.
    /// </summary>
    public TypedConfirmationModalViewModel ShutdownModal { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotControlModel"/> class.
    /// </summary>
    public BotControlModel(
        IBotService botService,
        ILogger<BotControlModel> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Bot Control page.
    /// </summary>
    public void OnGet()
    {
        _logger.LogDebug("Bot Control page accessed by user {UserId}", User.Identity?.Name);
        LoadViewModel();
    }

    /// <summary>
    /// Handles POST requests to restart the bot.
    /// </summary>
    public async Task<IActionResult> OnPostRestartBotAsync()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to restart bot", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogWarning("Bot restart requested by user {UserId}", User.Identity?.Name);

        try
        {
            await _botService.RestartAsync();
            _logger.LogInformation("Bot restart completed successfully, initiated by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = true,
                message = "Bot is restarting..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to restart bot. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to shutdown the bot.
    /// </summary>
    public async Task<IActionResult> OnPostShutdownBotAsync()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to shutdown bot", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogCritical("Bot SHUTDOWN requested by user {UserId}", User.Identity?.Name);

        try
        {
            await _botService.ShutdownAsync();
            _logger.LogCritical("Bot shutdown initiated by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = true,
                message = "Bot is shutting down..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown bot, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to shutdown bot. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    private void LoadViewModel()
    {
        var status = _botService.GetStatus();
        var config = _botService.GetConfiguration();

        ViewModel = new BotControlViewModel
        {
            Status = BotStatusViewModel.FromDto(status),
            Configuration = config,
            CanRestart = true,
            CanShutdown = true
        };

        RestartModal = new ConfirmationModalViewModel
        {
            Id = "restartModal",
            Title = "Restart Bot",
            Message = "Are you sure you want to restart the bot? This will briefly disconnect the bot from all servers. The bot will automatically reconnect after a few seconds.",
            ConfirmText = "Restart Bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "RestartBot"
        };

        ShutdownModal = new TypedConfirmationModalViewModel
        {
            Id = "shutdownModal",
            Title = "Shutdown Bot",
            Message = "This action will completely shut down the bot. The bot will NOT restart automatically and will need to be manually started from the server. This action is critical and should only be used when necessary.",
            RequiredText = "SHUTDOWN",
            InputLabel = "Type SHUTDOWN to confirm",
            ConfirmText = "Shutdown Bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormHandler = "ShutdownBot"
        };

        _logger.LogDebug("Bot Control ViewModel loaded: ConnectionState={ConnectionState}, GuildCount={GuildCount}",
            ViewModel.Status.ConnectionState, ViewModel.Status.GuildCount);
    }
}
