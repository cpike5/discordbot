using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/ScheduledMessages/Edit.cshtml</c> +
/// <c>EditModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Timezone handling
/// mirrors <see cref="Create"/>: the stored UTC <c>NextExecutionAt</c> is converted to the viewer's
/// detected IANA zone for display once <see cref="BrowserInterop.GetTimeZoneAsync"/> resolves, and
/// converted back on submit.
/// </summary>
public partial class Edit : GuildPageBase
{
    [Parameter]
    public Guid Id { get; set; }

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
    private ILogger<Edit> Logger { get; set; } = default!;

    protected ScheduledMessageInputModel Input { get; set; } = new();
    protected IReadOnlyList<ViewModels.Pages.ChannelSelectItem> AvailableChannels { get; private set; } = [];
    protected string? ErrorMessage { get; set; }
    protected string? DetectedTimeZone { get; private set; }
    protected bool NotFoundState { get; private set; }
    protected DateTime? LastExecutedAt { get; private set; }
    protected DateTime CreatedAt { get; private set; }

    protected string StatusLabel => ScheduledMessageStatusDisplay.Label(Input.IsEnabled, Input.Frequency, LastExecutedAt, Input.NextExecutionAt);

    private ConfirmModal? _deleteModal;
    private Guid _resolvedId;
    private DateTime _nextExecutionUtc;
    private bool _timeZoneApplied;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync();
    }

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedId != Id)
        {
            _timeZoneApplied = false;
            _ = ReloadAndRerenderAsync();
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _resolvedId = Id;
        var message = await ScheduledMessageService.GetByIdAsync(Id);
        if (message is null || message.GuildId != (ulong)GuildId)
        {
            NotFoundState = true;
            return;
        }

        NotFoundState = false;
        CreatedAt = message.CreatedAt;
        LastExecutedAt = message.LastExecutedAt;
        _nextExecutionUtc = message.NextExecutionAt ?? DateTime.UtcNow;

        AvailableChannels = ChannelResolver.GetTextChannels((ulong)GuildId)
            .Select(ViewModels.Pages.ChannelSelectItem.FromChannelInfo)
            .ToList();

        Input = new ScheduledMessageInputModel
        {
            Title = message.Title,
            Content = message.Content,
            ChannelId = message.ChannelId,
            Frequency = message.Frequency,
            CronExpression = message.CronExpression,
            IsEnabled = message.IsEnabled,
            NextExecutionAt = message.NextExecutionAt
        };

        if (DetectedTimeZone is not null)
        {
            ApplyDetectedTimeZoneToInput();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_timeZoneApplied)
        {
            DetectedTimeZone = await BrowserInterop.GetTimeZoneAsync();
            ApplyDetectedTimeZoneToInput();
            StateHasChanged();
        }
    }

    private void ApplyDetectedTimeZoneToInput()
    {
        if (NotFoundState)
        {
            return;
        }

        _timeZoneApplied = true;
        Input.NextExecutionAt = TimezoneHelper.ConvertFromUtc(_nextExecutionUtc, DetectedTimeZone);
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

        var nextExecutionUtc = TimezoneHelper.ConvertToUtc(Input.NextExecutionAt.Value, DetectedTimeZone);

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
            var result = await ScheduledMessageService.UpdateAsync(Id, updateDto);
            if (result is null)
            {
                NotFoundState = true;
                return;
            }

            Logger.LogInformation("Updated scheduled message {MessageId} for guild {GuildId}", Id, GuildId);
            Toast.Success("Scheduled message updated successfully.");
            NavigationManager.NavigateTo($"/Guilds/ScheduledMessages/{GuildId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update scheduled message {MessageId} for guild {GuildId}", Id, GuildId);
            ErrorMessage = "An error occurred while updating the scheduled message. Please try again.";
        }
    }

    protected async Task RequestDelete()
    {
        var confirmed = _deleteModal is not null && await _deleteModal.ShowAsync();
        if (confirmed)
        {
            await ConfirmDeleteAsync();
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        try
        {
            var deleted = await ScheduledMessageService.DeleteAsync(Id);
            if (deleted)
            {
                Toast.Success("Scheduled message deleted successfully.");
                NavigationManager.NavigateTo($"/Guilds/ScheduledMessages/{GuildId}");
            }
            else
            {
                Toast.Error("Failed to delete the scheduled message.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete scheduled message {MessageId} for guild {GuildId}", Id, GuildId);
            Toast.Error("An error occurred while deleting the scheduled message. Please try again.");
        }
    }
}
