using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Page model for creating a new scheduled message.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class CreateModel : GuildPageModelBase
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        ILogger<CreateModel> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _guildService = guildService;
        _channelResolver = channelResolver;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>What the shared message form renders.</summary>
    public ScheduledMessageEditorViewModel Editor { get; set; } = new();

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required.")]
        [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters (Discord limit).")]
        [Display(Name = "Message Content")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose the channel to send this message to.")]
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

        /// <summary>The time the user typed, in their own time zone (no zone information).</summary>
        [Required(ErrorMessage = "Choose when the message should next run.")]
        [Display(Name = "Next Execution Time")]
        public DateTime? NextExecutionAt { get; set; }

        [Display(Name = "User Timezone")]
        public string? UserTimezone { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing scheduled message create page for guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // The default next run is set by the browser (timezone.js) so it is in the user's own time zone.
        Input = new InputModel();

        LoadPage(guild.Id, guild.Name, guild.IconUrl, dirtyOnLoad: false);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for creating scheduled message in guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        if (Input.Frequency == ScheduleFrequency.Custom && ModelState.FieldError("Input.CronExpression") == null)
        {
            if (string.IsNullOrWhiteSpace(Input.CronExpression))
            {
                ModelState.AddModelError("Input.CronExpression", "Cron expression is required for custom schedules.");
            }
            else
            {
                var (isValid, errorMessage) = await _scheduledMessageService.ValidateCronExpressionAsync(Input.CronExpression);
                if (!isValid)
                {
                    ModelState.AddModelError("Input.CronExpression", errorMessage ?? "Invalid cron expression.");
                }
            }
        }

        // A wall-clock time inside a spring-forward gap does not exist, so it has no UTC instant
        if (Input.NextExecutionAt.HasValue
            && TimezoneHelper.IsNonexistentLocalTime(Input.NextExecutionAt.Value, Input.UserTimezone))
        {
            ModelState.AddModelError(
                "Input.NextExecutionAt",
                $"That time doesn't exist in {Input.UserTimezone} because of a daylight-saving change. Pick a time before or after it.");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Scheduled message for guild {GuildId} is invalid. Errors: {Errors}",
                guildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            LoadPage(guild.Id, guild.Name, guild.IconUrl, dirtyOnLoad: true);
            return Page();
        }

        // Get current user identifier for CreatedBy field
        var userId = User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        // The datetime-local input sends a time without a zone; the submitted zone says which one it is
        var nextExecutionUtc = TimezoneHelper.ConvertToUtc(Input.NextExecutionAt!.Value, Input.UserTimezone);

        _logger.LogInformation("Creating scheduled message with Title={Title}, Frequency={Frequency}, NextExecution={NextExecution} UTC (from user input: {LocalTime} in {Timezone}), CreatedBy={UserId}",
            Input.Title, Input.Frequency, nextExecutionUtc, Input.NextExecutionAt, Input.UserTimezone ?? "UTC", userId);

        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = guildId,
            ChannelId = Input.ChannelId!.Value,
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
                result.Id, guildId);

            TempData.SetSuccessToast("Scheduled message created.");
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scheduled message for guild {GuildId}", guildId);
            TempData.SetErrorToast("The message could not be created. Check the details and try again.");
            LoadPage(guild.Id, guild.Name, guild.IconUrl, dirtyOnLoad: true);
            return Page();
        }
    }

    /// <summary>
    /// Everything the view needs besides <see cref="Input"/>: the layout chrome and the form view
    /// model. Shared by GET and every failed POST, so a re-rendered form keeps its header,
    /// navigation and breadcrumb.
    /// </summary>
    private void LoadPage(ulong guildId, string guildName, string? guildIconUrl, bool dirtyOnLoad)
    {
        var channels = _channelResolver.GetTextChannels(guildId)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

        Editor = new ScheduledMessageEditorViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            IsEdit = false,
            Title = Input.Title,
            Content = Input.Content,
            ChannelId = Input.ChannelId,
            Frequency = Input.Frequency,
            CronExpression = Input.CronExpression,
            IsEnabled = Input.IsEnabled,
            // Already in the user's own time zone: it is shown as typed and never converted again
            NextExecutionLocal = Input.NextExecutionAt?.ToString(ScheduledMessageEditorViewModel.LocalFormat),
            SummaryNextRunUtcIso = ScheduledMessageEditorViewModel.ToUtcIso(Input.NextExecutionAt, Input.UserTimezone),
            AvailableChannels = channels,
            DirtyOnLoad = dirtyOnLoad
        };

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guildName, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Scheduled Messages", Url = $"/Guilds/ScheduledMessages/{guildId}" },
                new() { Label = "Create", IsCurrent = true }
            }
        };
        Header = BuildHeader(guildId, guildName, guildIconUrl,
            "Create Scheduled Message", $"Schedule a new automated message for {guildName}");
        Navigation = BuildNavigation(guildId, "messages");
    }
}
