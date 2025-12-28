using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Page model for editing an existing scheduled message.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class EditModel : PageModel
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<EditModel> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (guild info, available channels).
    /// </summary>
    public ScheduledMessageFormViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Error message to display on the page.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// List of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// The ID of the scheduled message being edited.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Created date for display.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Created date in ISO format for client-side timezone conversion.
    /// </summary>
    public string CreatedAtUtcIso => CreatedAt.ToString("o");

    /// <summary>
    /// Updated date for display.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Updated date in ISO format for client-side timezone conversion.
    /// </summary>
    public string UpdatedAtUtcIso => UpdatedAt.ToString("o");

    /// <summary>
    /// Last executed date for display.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Last executed date in ISO format for client-side timezone conversion.
    /// </summary>
    public string? LastExecutedAtUtcIso => LastExecutedAt?.ToString("o");

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        public ulong GuildId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required.")]
        [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters (Discord limit).")]
        [Display(Name = "Message Content")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Channel is required.")]
        [Display(Name = "Target Channel")]
        public ulong? ChannelId { get; set; }

        [Required(ErrorMessage = "Frequency is required.")]
        [Display(Name = "Schedule Frequency")]
        public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

        [StringLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters.")]
        [Display(Name = "Cron Expression")]
        public string? CronExpression { get; set; }

        [Display(Name = "Active")]
        public bool IsEnabled { get; set; } = true;

        [Required(ErrorMessage = "Next execution time is required.")]
        [Display(Name = "Next Execution Time")]
        public DateTime? NextExecutionAt { get; set; }

        [Display(Name = "User Timezone")]
        public string? UserTimezone { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing scheduled message edit page for message {MessageId} in guild {GuildId}", id, guildId);

        // Get the scheduled message
        var message = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (message == null)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found", id);
            return NotFound();
        }

        // Validate that the message belongs to the specified guild
        if (message.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} does not belong to guild {GuildId}", id, guildId);
            return NotFound();
        }

        MessageId = id;
        CreatedAt = message.CreatedAt;
        UpdatedAt = message.UpdatedAt;
        LastExecutedAt = message.LastExecutedAt;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Get available text channels from Discord
        AvailableChannels = GetTextChannels(guildId);

        _logger.LogDebug("Found {ChannelCount} text-capable channels for guild {GuildId}",
            AvailableChannels.Count, guildId);

        // Populate view model with message data
        ViewModel = new ScheduledMessageFormViewModel
        {
            GuildId = guildId,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            AvailableChannels = AvailableChannels,
            IsEditMode = true,
            Title = message.Title,
            Content = message.Content,
            ChannelId = message.ChannelId,
            Frequency = message.Frequency,
            CronExpression = message.CronExpression,
            IsEnabled = message.IsEnabled,
            NextExecutionAt = message.NextExecutionAt
        };

        // Populate form input model with existing values
        // Keep NextExecutionAt in UTC - JavaScript will convert it to local for display
        Input = new InputModel
        {
            GuildId = guildId,
            Title = message.Title,
            Content = message.Content,
            ChannelId = message.ChannelId,
            Frequency = message.Frequency,
            CronExpression = message.CronExpression,
            IsEnabled = message.IsEnabled,
            NextExecutionAt = message.NextExecutionAt
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for updating scheduled message {MessageId} in guild {GuildId}", id, guildId);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for message {MessageId}. Errors: {Errors}",
                id,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(guildId, id, cancellationToken);
            return Page();
        }

        // Validate that a channel is selected
        if (!Input.ChannelId.HasValue)
        {
            ModelState.AddModelError("Input.ChannelId", "A channel must be selected.");
            await LoadViewModelAsync(guildId, id, cancellationToken);
            return Page();
        }

        // Validate cron expression for custom frequency
        if (Input.Frequency == ScheduleFrequency.Custom)
        {
            if (string.IsNullOrWhiteSpace(Input.CronExpression))
            {
                ModelState.AddModelError("Input.CronExpression", "Cron expression is required for custom schedules.");
                await LoadViewModelAsync(guildId, id, cancellationToken);
                return Page();
            }

            var (isValid, errorMessage) = await _scheduledMessageService.ValidateCronExpressionAsync(Input.CronExpression);
            if (!isValid)
            {
                ModelState.AddModelError("Input.CronExpression", errorMessage ?? "Invalid cron expression.");
                await LoadViewModelAsync(guildId, id, cancellationToken);
                return Page();
            }
        }

        // Validate next execution time
        if (!Input.NextExecutionAt.HasValue)
        {
            ModelState.AddModelError("Input.NextExecutionAt", "Next execution time is required.");
            await LoadViewModelAsync(guildId, id, cancellationToken);
            return Page();
        }

        // Convert the NextExecutionAt from user's local time to UTC
        // The datetime-local input sends time without timezone info
        // Use the submitted UserTimezone to perform the correct conversion
        var nextExecutionUtc = TimezoneHelper.ConvertToUtc(Input.NextExecutionAt.Value, Input.UserTimezone);

        _logger.LogInformation("Updating scheduled message {MessageId} with Title={Title}, Frequency={Frequency}, NextExecution={NextExecution} UTC (from user input: {LocalTime} in {Timezone})",
            id, Input.Title, Input.Frequency, nextExecutionUtc, Input.NextExecutionAt, Input.UserTimezone ?? "UTC");

        // Create the update DTO
        var updateDto = new ScheduledMessageUpdateDto
        {
            ChannelId = Input.ChannelId.Value,
            Title = Input.Title,
            Content = Input.Content,
            Frequency = Input.Frequency,
            CronExpression = Input.Frequency == ScheduleFrequency.Custom ? Input.CronExpression : null,
            IsEnabled = Input.IsEnabled,
            NextExecutionAt = nextExecutionUtc
        };

        try
        {
            var result = await _scheduledMessageService.UpdateAsync(id, updateDto, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found during update", id);
                return NotFound();
            }

            _logger.LogInformation("Successfully updated scheduled message {MessageId} for guild {GuildId}",
                id, guildId);

            SuccessMessage = "Scheduled message updated successfully.";
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled message {MessageId} for guild {GuildId}", id, guildId);
            ErrorMessage = "An error occurred while updating the scheduled message. Please try again.";
            await LoadViewModelAsync(guildId, id, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for deleting scheduled message {MessageId} in guild {GuildId}", id, guildId);

        try
        {
            // Verify the message exists and belongs to this guild
            var message = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found during delete", id);
                return NotFound();
            }

            if (message.GuildId != guildId)
            {
                _logger.LogWarning("Scheduled message {MessageId} does not belong to guild {GuildId}", id, guildId);
                return NotFound();
            }

            var deleted = await _scheduledMessageService.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning("Failed to delete scheduled message {MessageId}", id);
                ErrorMessage = "Failed to delete the scheduled message.";
                return RedirectToPage("Index", new { guildId });
            }

            _logger.LogInformation("Successfully deleted scheduled message {MessageId} for guild {GuildId}", id, guildId);

            SuccessMessage = "Scheduled message deleted successfully.";
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete scheduled message {MessageId} for guild {GuildId}", id, guildId);
            ErrorMessage = "An error occurred while deleting the scheduled message. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Gets the list of text-capable channels for a guild from Discord.
    /// Includes text channels, voice channels (with text chat), announcement channels, and stage channels.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>A list of channel select items sorted by position.</returns>
    private List<ChannelSelectItem> GetTextChannels(ulong guildId)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Could not fetch Discord guild {GuildId} from client", guildId);
            return new List<ChannelSelectItem>();
        }

        var channels = new List<ChannelSelectItem>();

        // Add text channels (regular and announcement/news)
        foreach (var channel in guild.TextChannels.Where(c => c != null))
        {
            // Check if it's an announcement/news channel by checking the concrete type
            var displayType = channel is SocketNewsChannel
                ? ChannelDisplayType.Announcement
                : ChannelDisplayType.Text;

            channels.Add(new ChannelSelectItem
            {
                Id = channel.Id,
                Name = channel.Name,
                Position = channel.Position,
                Type = displayType
            });
        }

        // Add voice channels (they have text chat capability now)
        foreach (var channel in guild.VoiceChannels.Where(c => c != null))
        {
            channels.Add(new ChannelSelectItem
            {
                Id = channel.Id,
                Name = channel.Name,
                Position = channel.Position,
                Type = ChannelDisplayType.Voice
            });
        }

        // Add stage channels (they also have text chat)
        foreach (var channel in guild.StageChannels.Where(c => c != null))
        {
            channels.Add(new ChannelSelectItem
            {
                Id = channel.Id,
                Name = channel.Name,
                Position = channel.Position,
                Type = ChannelDisplayType.Stage
            });
        }

        // Sort by position
        var sortedChannels = channels.OrderBy(c => c.Position).ToList();

        _logger.LogDebug("Retrieved {ChannelCount} text-capable channels for guild {GuildId}",
            sortedChannels.Count, guildId);

        return sortedChannels;
    }

    /// <summary>
    /// Loads the view model for redisplay after validation error.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The scheduled message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadViewModelAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        var message = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);

        if (message != null && guild != null)
        {
            MessageId = id;
            CreatedAt = message.CreatedAt;
            UpdatedAt = message.UpdatedAt;
            LastExecutedAt = message.LastExecutedAt;

            AvailableChannels = GetTextChannels(guildId);

            ViewModel = new ScheduledMessageFormViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                AvailableChannels = AvailableChannels,
                IsEditMode = true,
                Title = Input.Title,
                Content = Input.Content,
                ChannelId = Input.ChannelId,
                Frequency = Input.Frequency,
                CronExpression = Input.CronExpression,
                IsEnabled = Input.IsEnabled,
                NextExecutionAt = Input.NextExecutionAt
            };
        }
    }

    /// <summary>
    /// Computes the status display text based on the message state.
    /// </summary>
    public string GetStatusDisplay()
    {
        if (!Input.IsEnabled)
        {
            return "Paused";
        }

        if (Input.Frequency == ScheduleFrequency.Once && LastExecutedAt.HasValue)
        {
            return "Expired";
        }

        return "Active";
    }

    /// <summary>
    /// Gets the status badge variant class based on the status.
    /// </summary>
    public string GetStatusBadgeClass()
    {
        var status = GetStatusDisplay();
        return status switch
        {
            "Active" => "bg-success/20 text-success",
            "Paused" => "bg-warning/20 text-warning",
            "Expired" => "bg-bg-tertiary text-text-tertiary",
            _ => "bg-bg-tertiary text-text-tertiary"
        };
    }

    /// <summary>
    /// Gets the status dot color class.
    /// </summary>
    public string GetStatusDotClass()
    {
        var status = GetStatusDisplay();
        return status switch
        {
            "Active" => "bg-success",
            "Paused" => "bg-warning",
            "Expired" => "bg-text-tertiary",
            _ => "bg-text-tertiary"
        };
    }
}
