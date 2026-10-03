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
/// Page model for editing (and deleting) an existing scheduled message.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class EditModel : GuildPageModelBase
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IScheduledMessageService scheduledMessageService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        ILogger<EditModel> logger)
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

    public async Task<IActionResult> OnGetAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing scheduled message edit page for message {MessageId} in guild {GuildId}", id, guildId);

        var message = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (message == null || message.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found in guild {GuildId}", id, guildId);
            return NotFound();
        }

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        Input = new InputModel
        {
            Title = message.Title,
            Content = message.Content,
            ChannelId = message.ChannelId,
            Frequency = message.Frequency,
            CronExpression = message.CronExpression,
            IsEnabled = message.IsEnabled,
            NextExecutionAt = message.NextExecutionAt
        };

        LoadPage(guild, message, postedBack: false);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for updating scheduled message {MessageId} in guild {GuildId}", id, guildId);

        // Guild access is authorized for the route's guild only, so the message must belong to it
        var existing = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found in guild {GuildId}", id, guildId);
            return NotFound();
        }

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
            _logger.LogWarning("Scheduled message {MessageId} is invalid. Errors: {Errors}",
                id,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            LoadPage(guild, existing, postedBack: true);
            return Page();
        }

        // The datetime-local input sends a time without a zone; the submitted zone says which one it is
        var nextExecutionUtc = TimezoneHelper.ConvertToUtc(Input.NextExecutionAt!.Value, Input.UserTimezone);

        _logger.LogInformation("Updating scheduled message {MessageId} with Title={Title}, Frequency={Frequency}, NextExecution={NextExecution} UTC (from user input: {LocalTime} in {Timezone})",
            id, Input.Title, Input.Frequency, nextExecutionUtc, Input.NextExecutionAt, Input.UserTimezone ?? "UTC");

        var updateDto = new ScheduledMessageUpdateDto
        {
            ChannelId = Input.ChannelId!.Value,
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

            _logger.LogInformation("Successfully updated scheduled message {MessageId} for guild {GuildId}", id, guildId);

            TempData.SetSuccessToast("Scheduled message updated.");
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled message {MessageId} for guild {GuildId}", id, guildId);
            TempData.SetErrorToast("The message could not be saved. Check the details and try again.");
            LoadPage(guild, existing, postedBack: true);
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
            if (message == null || message.GuildId != guildId)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found in guild {GuildId} during delete", id, guildId);
                return NotFound();
            }

            var deleted = await _scheduledMessageService.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning("Failed to delete scheduled message {MessageId}", id);
                TempData.SetErrorToast("The message could not be deleted. It may already be gone.");
                return RedirectToPage("Index", new { guildId });
            }

            _logger.LogInformation("Successfully deleted scheduled message {MessageId} for guild {GuildId}", id, guildId);

            TempData.SetSuccessToast("Scheduled message deleted.");
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete scheduled message {MessageId} for guild {GuildId}", id, guildId);
            TempData.SetErrorToast("The message could not be deleted. Try again.");
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Everything the view needs besides <see cref="Input"/>. Shared by GET and every failed POST, so
    /// a re-rendered form keeps its header, navigation and breadcrumb.
    /// </summary>
    /// <param name="postedBack">
    /// True after a failed POST. <see cref="Input"/> then holds what the user typed, in their own
    /// time zone, and the date field must show it as typed; on GET it holds the stored UTC time,
    /// which a script converts into the viewer's zone.
    /// </param>
    private void LoadPage(GuildDto guild, ScheduledMessageDto message, bool postedBack)
    {
        var channels = _channelResolver.GetTextChannels(guild.Id)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

        Editor = new ScheduledMessageEditorViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            IsEdit = true,
            MessageId = message.Id,
            Title = Input.Title,
            Content = Input.Content,
            ChannelId = Input.ChannelId,
            Frequency = Input.Frequency,
            CronExpression = Input.CronExpression,
            IsEnabled = Input.IsEnabled,
            LastExecutedAt = message.LastExecutedAt,
            AvailableChannels = channels,
            DirtyOnLoad = postedBack
        };

        if (postedBack)
        {
            Editor.NextExecutionLocal = Input.NextExecutionAt?.ToString(ScheduledMessageEditorViewModel.LocalFormat);
            Editor.SummaryNextRunUtcIso = ScheduledMessageEditorViewModel.ToUtcIso(Input.NextExecutionAt, Input.UserTimezone);
        }
        else
        {
            var utc = Input.NextExecutionAt.HasValue ? DisplayFormat.Iso(Input.NextExecutionAt.Value) : null;
            Editor.NextExecutionUtcIso = utc;
            Editor.SummaryNextRunUtcIso = utc;
        }

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "Scheduled Messages", Url = $"/Guilds/ScheduledMessages/{guild.Id}" },
                new() { Label = "Edit", IsCurrent = true }
            }
        };
        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Edit Scheduled Message", $"Edit scheduled message for {guild.Name}");
        Navigation = BuildNavigation(guild.Id, "messages");
    }
}
