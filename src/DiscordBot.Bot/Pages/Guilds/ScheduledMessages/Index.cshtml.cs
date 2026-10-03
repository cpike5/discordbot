using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Page model for the scheduled messages list (index) page.
/// Displays all scheduled messages for a guild with pagination support.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        ILogger<IndexModel> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _guildService = guildService;
        _channelResolver = channelResolver;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public ScheduledMessageListViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Handles GET requests to display the scheduled messages list.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="pageNumber">The page number from the query string (default: 1). Not named
    /// <c>page</c>, which Razor Pages reserves for the page route.</param>
    /// <param name="pageSize">The page size from the query string (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing scheduled messages list for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "messages",
            "Scheduled Messages", "Manage scheduled and recurring messages");
        Header.Actions = new List<HeaderAction>
        {
            new()
            {
                Label = "Create New",
                Url = $"/Guilds/ScheduledMessages/Create/{guildId}",
                Style = HeaderActionStyle.Primary,
                Icon = "M12 4v16m8-8H4"
            }
        };

        try
        {
            var (messages, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(
                guildId,
                pageNumber,
                pageSize,
                cancellationToken);

            ViewModel = ScheduledMessageListViewModel.Create(
                guildId,
                guild.Name,
                guild.IconUrl,
                messages,
                channelId => _channelResolver.ResolveChannelName(guildId, channelId),
                pageNumber,
                pageSize,
                totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load scheduled messages for guild {GuildId}", guildId);
            ViewModel = new ScheduledMessageListViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Page = pageNumber,
                PageSize = pageSize
            };
            ErrorMessage = "The scheduled messages could not be loaded. Try again in a moment.";
        }

        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a scheduled message.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        ulong guildId,
        Guid messageId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to delete scheduled message {MessageId} for guild {GuildId}",
            messageId, guildId);

        // Guild access is authorized for the route's guild only, so the message must belong to it
        var existing = await _scheduledMessageService.GetByIdAsync(messageId, cancellationToken);
        if (existing != null && existing.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} does not belong to guild {GuildId}", messageId, guildId);
            return NotFound();
        }

        var success = existing != null && await _scheduledMessageService.DeleteAsync(messageId, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully deleted scheduled message {MessageId}", messageId);
            TempData.SetSuccessToast("Scheduled message deleted.");
        }
        else
        {
            _logger.LogWarning("Failed to delete scheduled message {MessageId} - not found", messageId);
            TempData.SetErrorToast("That message was not found. It may already have been deleted.");
        }

        return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
    }

    /// <summary>
    /// Handles POST requests to toggle a scheduled message's enabled state (pause/resume).
    /// </summary>
    public async Task<IActionResult> OnPostToggleAsync(
        ulong guildId,
        Guid messageId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to toggle scheduled message {MessageId} for guild {GuildId}",
            messageId, guildId);

        var scheduledMessage = await _scheduledMessageService.GetByIdAsync(messageId, cancellationToken);

        if (scheduledMessage == null)
        {
            _logger.LogWarning("Failed to toggle scheduled message {MessageId} - not found", messageId);
            TempData.SetErrorToast("That message was not found. It may have been deleted.");
            return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
        }

        // Guild access is authorized for the route's guild only, so the message must belong to it
        if (scheduledMessage.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} does not belong to guild {GuildId}", messageId, guildId);
            return NotFound();
        }

        var updateDto = new ScheduledMessageUpdateDto
        {
            IsEnabled = !scheduledMessage.IsEnabled
        };

        var result = await _scheduledMessageService.UpdateAsync(messageId, updateDto, cancellationToken);

        if (result != null)
        {
            var action = result.IsEnabled ? "resumed" : "paused";
            _logger.LogInformation("Successfully {Action} scheduled message {MessageId}", action, messageId);
            TempData.SetSuccessToast($"Scheduled message {action}.");
        }
        else
        {
            _logger.LogWarning("Failed to toggle scheduled message {MessageId}", messageId);
            TempData.SetErrorToast("The message could not be updated. Try again.");
        }

        return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
    }
}
