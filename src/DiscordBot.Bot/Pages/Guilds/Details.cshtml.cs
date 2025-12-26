using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for displaying detailed guild information.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class DetailsModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IWelcomeService _welcomeService;
    private readonly ILogger<DetailsModel> _logger;

    private const int RecentCommandsLimit = 10;

    public DetailsModel(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IWelcomeService welcomeService,
        ILogger<DetailsModel> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _welcomeService = welcomeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing guild details.
    /// </summary>
    public GuildDetailViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Gets whether welcome messages are enabled for this guild.
    /// </summary>
    public bool WelcomeEnabled { get; set; }

    /// <summary>
    /// Gets the welcome channel name if configured.
    /// </summary>
    public string? WelcomeChannelName { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild details page for guild {GuildId}", id);

        // Fetch guild data
        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);
            return NotFound();
        }

        // Fetch recent command activity for this guild
        var commandQuery = new CommandLogQueryDto
        {
            GuildId = id,
            Page = 1,
            PageSize = RecentCommandsLimit
        };
        var recentCommandsResponse = await _commandLogService.GetLogsAsync(commandQuery, cancellationToken);

        // Fetch welcome configuration status
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(id, cancellationToken);
        WelcomeEnabled = welcomeConfig?.IsEnabled ?? false;

        _logger.LogDebug("Retrieved guild {GuildId} with {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}",
            id, recentCommandsResponse.Items.Count, WelcomeEnabled);

        // Build view model
        ViewModel = GuildDetailViewModel.FromDto(guild, recentCommandsResponse.Items);

        // TODO: Set CanEdit based on user's guild-specific permissions
        // For now, all moderators can view but edit capability depends on future authorization

        return Page();
    }

    /// <summary>
    /// Handles POST request to sync a single guild from Discord.
    /// </summary>
    public async Task<IActionResult> OnPostSyncAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User requesting sync for guild {GuildId}", id);

        try
        {
            var success = await _guildService.SyncGuildAsync(id, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully synced guild {GuildId}", id);

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = "Guild synced successfully" });
                }

                SuccessMessage = "Guild synced successfully";
                return RedirectToPage(new { id });
            }
            else
            {
                _logger.LogWarning("Failed to sync guild {GuildId} - guild not found in Discord", id);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = "Guild not found in Discord client" });
                }

                SuccessMessage = null;
                return RedirectToPage(new { id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing guild {GuildId}", id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "An error occurred while syncing the guild" });
            }

            SuccessMessage = null;
            return RedirectToPage(new { id });
        }
    }
}
