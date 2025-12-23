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
    private readonly ILogger<DetailsModel> _logger;

    private const int RecentCommandsLimit = 10;

    public DetailsModel(
        IGuildService guildService,
        ICommandLogService commandLogService,
        ILogger<DetailsModel> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing guild details.
    /// </summary>
    public GuildDetailViewModel ViewModel { get; set; } = new();

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

        _logger.LogDebug("Retrieved guild {GuildId} with {CommandCount} recent commands",
            id, recentCommandsResponse.Items.Count);

        // Build view model
        ViewModel = GuildDetailViewModel.FromDto(guild, recentCommandsResponse.Items);

        // TODO: Set CanEdit based on user's guild-specific permissions
        // For now, all moderators can view but edit capability depends on future authorization

        return Page();
    }
}
