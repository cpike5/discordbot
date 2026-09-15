using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/FlaggedEvents/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Dismiss/
/// Acknowledge/Take Action all call <see cref="IFlaggedEventService"/> directly through one shared
/// confirm modal (<see cref="_confirmModal"/>) instead of the legacy inline-JS
/// <c>quickActions.confirm</c> + fetch pair.
/// </summary>
public partial class Details : GuildPageBase
{
    [Parameter]
    public Guid Id { get; set; }

    // AuthenticationStateTask is inherited (protected) from GuildPageBase - see the identical note
    // on FeatureRequests/Details.razor.cs.

    [Inject]
    private IFlaggedEventService Service { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected FlaggedEventDto? Event { get; private set; }
    protected IReadOnlyList<FlaggedEventDto> UserHistory { get; private set; } = [];
    protected bool NotFoundState { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected bool IsBusy { get; private set; }
    protected bool EvidenceExpanded { get; set; }
    protected string CustomActionText { get; set; } = string.Empty;
    protected bool CustomActionModalOpen { get; set; }

    private Guid _resolvedId;
    private int _loadGeneration;
    private ConfirmModal? _confirmModal;
    private string _pendingActionTitle = string.Empty;
    private string _pendingActionMessage = string.Empty;
    private Func<Task>? _pendingAction;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(Service);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedId != Id)
        {
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync(Service);
        StateHasChanged();
    }

    private async Task LoadAsync(IFlaggedEventService service)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedId = Id;

        try
        {
            var evt = await service.GetEventAsync(Id);
            if (generation != _loadGeneration)
            {
                return;
            }

            NotFoundState = evt is null || evt.GuildId != (ulong)GuildId;
            if (NotFoundState)
            {
                Event = null;
                UserHistory = [];
                return;
            }

            Event = evt;
            var history = await service.GetUserEventsAsync((ulong)GuildId, evt!.UserId);
            if (generation != _loadGeneration)
            {
                return;
            }

            UserHistory = history.Where(e => e.Id != Id).OrderByDescending(e => e.CreatedAt).Take(10).ToList();
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load flagged event {EventId} for guild {GuildId}", Id, GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load this flagged event.");
        }
    }

    private async Task<ulong?> GetReviewerIdAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return null;
        }

        var user = (await AuthenticationStateTask).User;
        var id = user.GetDiscordUserId();
        return id == 0 ? null : id;
    }

    protected void ToggleEvidence() => EvidenceExpanded = !EvidenceExpanded;

    protected async Task RequestDismissAsync()
    {
        _pendingActionTitle = "Dismiss Flagged Event";
        _pendingActionMessage = "Are you sure you want to dismiss this flagged event?";
        _pendingAction = DismissAsync;
        await ShowConfirmAsync();
    }

    protected async Task RequestAcknowledgeAsync()
    {
        _pendingActionTitle = "Acknowledge Flagged Event";
        _pendingActionMessage = "Are you sure you want to acknowledge this flagged event?";
        _pendingAction = AcknowledgeAsync;
        await ShowConfirmAsync();
    }

    protected async Task RequestCannedActionAsync(string action)
    {
        _pendingActionTitle = "Confirm Action";
        _pendingActionMessage = $"Are you sure you want to take this action: {action}?";
        _pendingAction = () => TakeActionAsync(action);
        await ShowConfirmAsync();
    }

    protected void ShowCustomActionModal()
    {
        CustomActionText = string.Empty;
        CustomActionModalOpen = true;
    }

    protected async Task SubmitCustomActionAsync()
    {
        var action = CustomActionText.Trim();
        if (string.IsNullOrEmpty(action))
        {
            Toast.Warning("Please enter an action description.");
            return;
        }

        CustomActionModalOpen = false;
        await TakeActionAsync(action);
    }

    private async Task ShowConfirmAsync()
    {
        if (_confirmModal is not null && await _confirmModal.ShowAsync())
        {
            await (_pendingAction?.Invoke() ?? Task.CompletedTask);
        }
    }

    private async Task DismissAsync()
    {
        var reviewerId = await GetReviewerIdAsync();
        if (reviewerId is null || Event is null)
        {
            Toast.Error("Your account isn't linked to a Discord user, so this action can't be recorded.");
            return;
        }

        IsBusy = true;
        try
        {
            await ScopeFactory.RunAsync<IFlaggedEventService>(s => s.DismissEventAsync(Event.Id, reviewerId.Value));
            Toast.Success("Event dismissed successfully.");
            // The legacy inline redirect targeted "/Guilds/FlaggedEvents/Index/{guildId}", a route
            // that never existed (the real list route is "/Guilds/FlaggedEvents/{guildId}") -
            // fixed here rather than reproduced.
            NavigationManager.NavigateTo($"/Guilds/FlaggedEvents/{GuildId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error dismissing flagged event {EventId}", Event.Id);
            Toast.Error("An error occurred while dismissing the event.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AcknowledgeAsync()
    {
        var reviewerId = await GetReviewerIdAsync();
        if (reviewerId is null || Event is null)
        {
            Toast.Error("Your account isn't linked to a Discord user, so this action can't be recorded.");
            return;
        }

        await RunMutationAsync(
            s => s.AcknowledgeEventAsync(Event.Id, reviewerId.Value),
            "Event acknowledged successfully.",
            "Error acknowledging flagged event {EventId}");
    }

    private async Task TakeActionAsync(string action)
    {
        var reviewerId = await GetReviewerIdAsync();
        if (reviewerId is null || Event is null)
        {
            Toast.Error("Your account isn't linked to a Discord user, so this action can't be recorded.");
            return;
        }

        await RunMutationAsync(
            s => s.TakeActionAsync(Event.Id, action, reviewerId.Value),
            "Action recorded successfully.",
            "Error taking action on flagged event {EventId}");
    }

    private async Task RunMutationAsync(Func<IFlaggedEventService, Task<FlaggedEventDto?>> mutation, string successMessage, string errorLogTemplate)
    {
        if (Event is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await ScopeFactory.RunAsync<IFlaggedEventService>(mutation);
            Toast.Success(successMessage);
            await ScopeFactory.RunAsync<IFlaggedEventService>(LoadAsync);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, errorLogTemplate, Event.Id);
            Toast.Error("An error occurred while updating the event.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
