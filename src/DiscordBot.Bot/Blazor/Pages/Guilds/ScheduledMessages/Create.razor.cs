using System.Security.Claims;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/ScheduledMessages/Create.cshtml</c> +
/// <c>CreateModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Timezone handling:
/// the viewer's IANA zone is detected once via <see cref="BrowserInterop.GetTimeZoneAsync"/> after
/// first render, used to default <see cref="Input"/>'s <c>NextExecutionAt</c> to "local now + 5
/// minutes, rounded up to the next 5-minute mark" and to convert the submitted local value back to
/// UTC with <see cref="TimezoneHelper.ConvertToUtc"/>.
/// </summary>
public partial class Create : GuildPageBase
{
    // AuthenticationStateTask is inherited (protected) from GuildPageBase - redeclaring it here
    // as a second [CascadingParameter] throws "declares more than one parameter matching the name
    // 'authenticationstatetask'" at render time (parameter names are case-insensitive).

    [Inject]
    private IScheduledMessageService ScheduledMessageService { get; set; } = default!;

    [Inject]
    private IDiscordChannelResolver ChannelResolver { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Create> Logger { get; set; } = default!;

    protected ScheduledMessageInputModel Input { get; set; } = new();
    protected IReadOnlyList<ViewModels.Pages.ChannelSelectItem> AvailableChannels { get; private set; } = [];
    protected string? ErrorMessage { get; set; }
    protected string? DetectedTimeZone { get; private set; }

    private bool _defaultApplied;

    protected override Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return Task.CompletedTask;
        }

        AvailableChannels = ChannelResolver.GetTextChannels((ulong)GuildId)
            .Select(ViewModels.Pages.ChannelSelectItem.FromChannelInfo)
            .ToList();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Own post-render work (defaulting <see cref="Input"/>'s next-execution time from the
    /// viewer's detected timezone) alongside <see cref="GuildPageBase"/>'s local-time-scan
    /// handling - calls <c>base.OnAfterRenderAsync(firstRender)</c> even though this page renders
    /// no <c>&lt;LocalTime&gt;</c> today, so a future one added here isn't silently skipped.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender && !_defaultApplied)
        {
            _defaultApplied = true;
            DetectedTimeZone = await BrowserInterop.GetTimeZoneAsync();

            var localNow = TimezoneHelper.ConvertFromUtc(DateTime.UtcNow, DetectedTimeZone);
            Input.NextExecutionAt = RoundUpToNextFiveMinutes(localNow.AddMinutes(5));
            StateHasChanged();
        }
    }

    protected async Task HandleValidSubmit()
    {
        ErrorMessage = null;

        if (!Input.ChannelId.HasValue)
        {
            ErrorMessage = "A channel must be selected.";
            return;
        }

        if (Input.Frequency == ScheduleFrequency.Custom)
        {
            if (string.IsNullOrWhiteSpace(Input.CronExpression))
            {
                ErrorMessage = "Cron expression is required for custom schedules.";
                return;
            }

            var (isValid, cronError) = await ScheduledMessageService.ValidateCronExpressionAsync(Input.CronExpression);
            if (!isValid)
            {
                ErrorMessage = cronError ?? "Invalid cron expression.";
                return;
            }
        }

        if (!Input.NextExecutionAt.HasValue)
        {
            ErrorMessage = "Next execution time is required.";
            return;
        }

        DateTime nextExecutionUtc;
        try
        {
            nextExecutionUtc = TimezoneHelper.ConvertToUtc(Input.NextExecutionAt.Value, DetectedTimeZone);
        }
        catch (ArgumentException)
        {
            // The chosen local time falls in a DST spring-forward gap (e.g. America/Toronto
            // 2026-03-08 02:30, which never occurs as clocks jump from 2:00 to 3:00) -
            // TimeZoneInfo.ConvertTimeToUtc throws for a time that doesn't exist rather than
            // guessing which side of the gap was meant. Surface it as a field validation message
            // instead of letting it escape this handler and crash the circuit.
            ErrorMessage = "That time doesn't exist in your timezone (it falls in a daylight saving time change). Please choose a different time.";
            return;
        }

        var userId = await GetCurrentUserIdAsync();

        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = (ulong)GuildId,
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
            var result = await ScheduledMessageService.CreateAsync(createDto);
            Logger.LogInformation("Created scheduled message {MessageId} for guild {GuildId}", result.Id, GuildId);
            Toast.Success("Scheduled message created successfully.");
            NavigationManager.NavigateTo($"/Guilds/ScheduledMessages/{GuildId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create scheduled message for guild {GuildId}", GuildId);
            ErrorMessage = "An error occurred while creating the scheduled message. Please try again.";
        }
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return "Unknown";
        }

        var user = (await AuthenticationStateTask).User;
        return user.Identity?.Name ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
    }

    /// <summary>
    /// Rounds <paramref name="value"/> up to the next 5-minute mark, leaving it unchanged if it is
    /// already on one. Extracted as a pure, publicly-testable method (rather than inline math in
    /// <see cref="OnAfterRenderAsync"/>) since the rest of that method depends on
    /// <see cref="BrowserInterop.GetTimeZoneAsync"/> and <see cref="DateTime.UtcNow"/>, neither of
    /// which a plain unit test can control.
    /// </summary>
    public static DateTime RoundUpToNextFiveMinutes(DateTime value)
    {
        // Ceiling division: (Minute + 4) / 5 leaves an exact multiple of 5 unchanged (e.g. :20 stays
        // :20) and still rounds any other minute up to the next mark (e.g. :21 -> :25) - unlike
        // (Minute / 5 + 1) * 5, which always added a full 5 minutes even when already on a mark.
        var roundedMinute = (value.Minute + 4) / 5 * 5;
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind).AddMinutes(roundedMinute);
    }
}
