using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.MessageLogs;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/MessageLogs/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Reproduces
/// <c>DetailsModel.OnGetAsync</c>'s <see cref="IMessageLogService.GetByIdAsync"/> load and 404
/// handling.
/// </summary>
/// <remarks>
/// <b>Not found is HTTP 200 with an <c>EmptyState</c>,</b> matching the same established
/// precedent as <c>Blazor/Pages/Admin/AuditLogs/Details.razor.cs</c> - see that class's remarks.
/// <b>Preview triggers.</b> The legacy page's Author/Guild <c>preview-trigger</c> spans are now
/// <c>Blazor/Shared/Overlays/UserPreview.razor</c>/<c>GuildPreview.razor</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4d), calling the same lookup logic
/// <c>PreviewController</c> uses via <c>IPreviewService</c> directly, in-circuit.
/// </remarks>
public partial class Details : ComponentBase, IDisposable
{
    [Parameter]
    public long Id { get; set; }

    [Inject]
    private IMessageLogService MessageLogService { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected MessageLogDto? Message { get; private set; }
    protected bool NotFound { get; private set; }

    private bool _needsLocalTimeScan;
    private long _resolvedId = -1;
    private PersistingComponentStateSubscription _persistingSubscription;

    private string PersistenceKey => $"MessageLogDetails.{Id}";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<MessageLogPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            Message = restored.Message;
            NotFound = restored.Message is null;
            _resolvedId = Id;
            _needsLocalTimeScan = true;
            return;
        }

        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedId != Id)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _resolvedId = Id;
        Logger.LogDebug("Loading message log details for ID: {MessageLogId}", Id);

        var message = await MessageLogService.GetByIdAsync(Id);
        if (message is null)
        {
            Logger.LogWarning("Message log not found: {MessageLogId}", Id);
            Message = null;
            NotFound = true;
            return;
        }

        Message = message;
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

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new MessageLogPersistedState(Message));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Persisted shape for the prerender-to-circuit round trip - <see langword="null"/> <see cref="Message"/> means "not found".</summary>
    private sealed record MessageLogPersistedState(MessageLogDto? Message);
}
