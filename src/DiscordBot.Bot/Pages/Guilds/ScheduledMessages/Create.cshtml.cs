using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Page model for creating a new scheduled message.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class CreateModel : PageModel
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<CreateModel> logger)
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
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing scheduled message create page for guild {GuildId}", guildId);

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

        // Populate view model with defaults
        ViewModel = new ScheduledMessageFormViewModel
        {
            GuildId = guildId,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            AvailableChannels = AvailableChannels,
            IsEditMode = false,
            IsEnabled = true,
            Frequency = ScheduleFrequency.Daily
        };

        // Populate form input model with defaults
        // Use local time for the datetime-local input, rounded to the next 5 minutes
        var now = DateTime.Now;
        var minutes = now.Minute;
        var roundedMinutes = ((minutes / 5) + 1) * 5; // Round up to next 5-minute mark
        var defaultTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddMinutes(roundedMinutes);
        if (defaultTime <= now)
        {
            defaultTime = defaultTime.AddMinutes(5);
        }

        Input = new InputModel
        {
            GuildId = guildId,
            IsEnabled = true,
            Frequency = ScheduleFrequency.Daily,
            NextExecutionAt = defaultTime
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for creating scheduled message in guild {GuildId}", Input.GuildId);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                Input.GuildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Validate that a channel is selected
        if (!Input.ChannelId.HasValue)
        {
            ModelState.AddModelError("Input.ChannelId", "A channel must be selected.");
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Validate cron expression for custom frequency
        if (Input.Frequency == ScheduleFrequency.Custom)
        {
            if (string.IsNullOrWhiteSpace(Input.CronExpression))
            {
                ModelState.AddModelError("Input.CronExpression", "Cron expression is required for custom schedules.");
                await LoadViewModelAsync(Input.GuildId, cancellationToken);
                return Page();
            }

            var (isValid, errorMessage) = await _scheduledMessageService.ValidateCronExpressionAsync(Input.CronExpression);
            if (!isValid)
            {
                ModelState.AddModelError("Input.CronExpression", errorMessage ?? "Invalid cron expression.");
                await LoadViewModelAsync(Input.GuildId, cancellationToken);
                return Page();
            }
        }

        // Validate next execution time
        if (!Input.NextExecutionAt.HasValue)
        {
            ModelState.AddModelError("Input.NextExecutionAt", "Next execution time is required.");
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Get current user identifier for CreatedBy field
        var userId = User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        // Convert the NextExecutionAt from local time to UTC
        // The datetime-local input sends time in local timezone without timezone info
        // Treat it as local time and convert to UTC for storage
        var nextExecutionUtc = DateTime.SpecifyKind(Input.NextExecutionAt.Value, DateTimeKind.Local).ToUniversalTime();

        _logger.LogDebug("Creating scheduled message with Title={Title}, Frequency={Frequency}, NextExecution={NextExecution} (local: {LocalTime}), CreatedBy={UserId}",
            Input.Title, Input.Frequency, nextExecutionUtc, Input.NextExecutionAt, userId);

        // Create the scheduled message DTO
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = Input.GuildId,
            ChannelId = Input.ChannelId.Value,
            Title = Input.Title,
            Content = Input.Content,
            Frequency = Input.Frequency,
            CronExpression = Input.Frequency == ScheduleFrequency.Custom ? Input.CronExpression : null,
            IsEnabled = Input.IsEnabled,
            NextExecutionAt = nextExecutionUtc,
            CreatedBy = userId
        };

        try
        {
            var result = await _scheduledMessageService.CreateAsync(createDto, cancellationToken);

            _logger.LogInformation("Successfully created scheduled message {MessageId} for guild {GuildId}",
                result.Id, Input.GuildId);

            SuccessMessage = "Scheduled message created successfully.";
            return RedirectToPage("Index", new { guildId = Input.GuildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scheduled message for guild {GuildId}", Input.GuildId);
            ErrorMessage = "An error occurred while creating the scheduled message. Please try again.";
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
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
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild != null)
        {
            AvailableChannels = GetTextChannels(guildId);

            ViewModel = new ScheduledMessageFormViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                AvailableChannels = AvailableChannels,
                IsEditMode = false,
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
}
