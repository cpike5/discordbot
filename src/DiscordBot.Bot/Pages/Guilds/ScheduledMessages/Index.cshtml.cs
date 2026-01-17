using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Page model for the scheduled messages list (index) page.
/// Displays all scheduled messages for a guild with pagination support.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public ScheduledMessageListViewModel ViewModel { get; set; } = new();

    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();
    public GuildHeaderViewModel Header { get; set; } = new();
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message from TempData.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Handles GET requests to display the scheduled messages list.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="page">The page number from query parameter (default: 1).</param>
    /// <param name="pageSize">The page size from query parameter (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing scheduled messages list for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, page, pageSize);

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "Scheduled Messages", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "Scheduled Messages",
            PageDescription = "Manage scheduled and recurring messages",
            Actions = new List<HeaderAction>
            {
                new()
                {
                    Label = "Create New",
                    Url = $"/Guilds/ScheduledMessages/Create/{guildId}",
                    Style = HeaderActionStyle.Primary,
                    Icon = "M12 4v16m8-8H4"
                }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "messages",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        // Get paginated scheduled messages
        var (messages, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(
            guildId,
            page,
            pageSize,
            cancellationToken);

        _logger.LogDebug("Retrieved {Count} scheduled messages for guild {GuildId} (page {Page} of {TotalPages})",
            messages.Count(), guildId, page, (int)Math.Ceiling((double)totalCount / pageSize));

        // Build view model with channel name resolution
        ViewModel = ScheduledMessageListViewModel.Create(
            guildId,
            guild.Name,
            guild.IconUrl,
            messages,
            ResolveChannelName,
            page,
            pageSize,
            totalCount);

        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a scheduled message.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="messageId">The scheduled message ID to delete.</param>
    /// <param name="page">The current page number to return to.</param>
    /// <param name="pageSize">The current page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(
        ulong guildId,
        Guid messageId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to delete scheduled message {MessageId} for guild {GuildId}",
            messageId, guildId);

        var success = await _scheduledMessageService.DeleteAsync(messageId, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully deleted scheduled message {MessageId}", messageId);
            SuccessMessage = "Scheduled message deleted successfully.";
        }
        else
        {
            _logger.LogWarning("Failed to delete scheduled message {MessageId} - not found", messageId);
            ErrorMessage = "Scheduled message not found. It may have already been deleted.";
        }

        return RedirectToPage("Index", new { guildId, page, pageSize });
    }

    /// <summary>
    /// Handles POST requests to toggle a scheduled message's enabled state (pause/resume).
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="messageId">The scheduled message ID to toggle.</param>
    /// <param name="page">The current page number to return to.</param>
    /// <param name="pageSize">The current page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostToggleAsync(
        ulong guildId,
        Guid messageId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to toggle scheduled message {MessageId} for guild {GuildId}",
            messageId, guildId);

        // Get the current scheduled message
        var scheduledMessage = await _scheduledMessageService.GetByIdAsync(messageId, cancellationToken);

        if (scheduledMessage == null)
        {
            _logger.LogWarning("Failed to toggle scheduled message {MessageId} - not found", messageId);
            ErrorMessage = "Scheduled message not found. It may have been deleted.";
            return RedirectToPage("Index", new { guildId, page, pageSize });
        }

        // Toggle the enabled state
        var updateDto = new Core.DTOs.ScheduledMessageUpdateDto
        {
            IsEnabled = !scheduledMessage.IsEnabled
        };

        var result = await _scheduledMessageService.UpdateAsync(messageId, updateDto, cancellationToken);

        if (result != null)
        {
            var action = result.IsEnabled ? "resumed" : "paused";
            _logger.LogInformation("Successfully {Action} scheduled message {MessageId}", action, messageId);
            SuccessMessage = $"Scheduled message {action} successfully.";
        }
        else
        {
            _logger.LogWarning("Failed to toggle scheduled message {MessageId}", messageId);
            ErrorMessage = "Failed to update scheduled message.";
        }

        return RedirectToPage("Index", new { guildId, page, pageSize });
    }

    /// <summary>
    /// Resolves a channel ID to its display name using the Discord client.
    /// Returns "Unknown Channel" if the channel cannot be resolved.
    /// </summary>
    /// <param name="channelId">The Discord channel snowflake ID.</param>
    /// <returns>The channel name or "Unknown Channel" if not found.</returns>
    private string ResolveChannelName(ulong channelId)
    {
        try
        {
            var channel = _discordClient.GetChannel(channelId);
            if (channel is SocketTextChannel textChannel)
            {
                return textChannel.Name;
            }
            else if (channel is SocketVoiceChannel voiceChannel)
            {
                return voiceChannel.Name;
            }
            else if (channel is SocketNewsChannel newsChannel)
            {
                return newsChannel.Name;
            }
            else if (channel is SocketStageChannel stageChannel)
            {
                return stageChannel.Name;
            }
            else if (channel != null)
            {
                // Generic fallback for other channel types
                return $"Channel {channelId}";
            }

            _logger.LogWarning("Could not resolve channel name for channel {ChannelId}", channelId);
            return "Unknown Channel";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving channel name for channel {ChannelId}", channelId);
            return "Unknown Channel";
        }
    }
}
