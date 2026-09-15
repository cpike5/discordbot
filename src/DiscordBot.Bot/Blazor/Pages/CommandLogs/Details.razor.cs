using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.CommandLogs;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/CommandLogs/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Reproduces
/// <c>DetailsModel.OnGetAsync</c>'s <see cref="ICommandLogService.GetByIdAsync"/> load and 404
/// handling via <see cref="CommandLogDetailViewModel.FromDto"/>, the same view model the legacy
/// page used - see <c>CommandLogDetailViewModelTests</c> (kept, unchanged by this cluster) for its
/// own coverage.
/// </summary>
/// <remarks>
/// <b>Not found is HTTP 200 with an <c>EmptyState</c>,</b> matching the same established
/// precedent as <c>Blazor/Pages/Admin/AuditLogs/Details.razor.cs</c> - see that class's remarks.
/// <b>Kept at <c>RequireModerator</c></b> per the legacy page's own gate (broader than the
/// <c>RequireAdmin</c> the other two cluster 4a Details pages use).
/// </remarks>
public partial class Details : ComponentBase, IDisposable
{
    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    private ICommandLogService CommandLogService { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected CommandLogDetailViewModel? ViewModel { get; private set; }
    protected bool NotFound { get; private set; }

    private bool _needsLocalTimeScan;
    private Guid _resolvedId = Guid.Empty;
    private bool _hasResolved;
    private PersistingComponentStateSubscription _persistingSubscription;

    private string PersistenceKey => $"CommandLogDetails.{Id}";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<CommandLogPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            ViewModel = restored.ViewModel;
            NotFound = restored.ViewModel is null;
            _resolvedId = Id;
            _hasResolved = true;
            _needsLocalTimeScan = true;
            return;
        }

        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_hasResolved || _resolvedId != Id)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _resolvedId = Id;
        _hasResolved = true;
        Logger.LogInformation("User accessing command log details for ID {Id}", Id);

        var log = await CommandLogService.GetByIdAsync(Id);
        if (log is null)
        {
            Logger.LogWarning("Command log with ID {Id} not found", Id);
            ViewModel = null;
            NotFound = true;
            return;
        }

        ViewModel = CommandLogDetailViewModel.FromDto(log);
        NotFound = false;
        _needsLocalTimeScan = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLocalTimeScan)
        {
            _needsLocalTimeScan = false;
            await BrowserInterop.ConvertLocalTimesAsync();
        }
    }

    protected async Task CopyToClipboardAsync(string value)
    {
        var copied = await BrowserInterop.CopyToClipboardAsync(value);
        if (copied)
        {
            Toast.Success("Copied to clipboard");
        }
        else
        {
            Toast.Error("Failed to copy to clipboard");
        }
    }

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new CommandLogPersistedState(ViewModel));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Persisted shape for the prerender-to-circuit round trip - <see langword="null"/> <see cref="ViewModel"/> means "not found".</summary>
    private sealed record CommandLogPersistedState(CommandLogDetailViewModel? ViewModel);
}
