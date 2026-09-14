using System.Text.Json;
using System.Text.Json.Nodes;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.AuditLogs;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/AuditLogs/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Reproduces
/// <c>DetailsModel.OnGetAsync</c>'s load (entry by id, related entries by correlation id when
/// present, 404 when the entry doesn't exist) via <see cref="AuditLogDetailViewModel.FromDto"/>,
/// the same view model the legacy page used.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not found is HTTP 200 with an <c>EmptyState</c>,</b> matching
/// <c>GuildContextGate</c>/<c>Test_J_GuildProbe_UnknownGuild_ShowsNotFoundState</c>'s established
/// precedent for this codebase's interactive pages rather than
/// <c>NavigationManager.NotFound()</c> - see the class's own "not found" region below and the
/// cluster report for why.
/// </para>
/// <para>
/// <b>Raw-data expand/collapse</b> is component state (<see cref="_rawDataExpanded"/>) instead of
/// the legacy inline script's <c>classList.toggle</c> - no JS needed.
/// </para>
/// <para>
/// <b>Export JSON</b> is built server-side with <c>System.Text.Json</c> (the legacy inline script
/// string-interpolated it, including an <c>@Html.Raw</c> injection of the raw <c>Details</c>
/// JSON) and handed to the browser via <see cref="BrowserInterop.DownloadFileAsync"/>.
/// </para>
/// </remarks>
public partial class Details : ComponentBase, IDisposable
{
    [Parameter]
    public long Id { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    [Inject]
    private IAuditLogService AuditLogService { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected AuditLogDetailViewModel? ViewModel { get; private set; }
    protected bool NotFound { get; private set; }
    protected string ResolvedReturnUrl { get; private set; } = "/Admin/Logs?tab=audit";

    private bool _rawDataExpanded;
    private bool _needsLocalTimeScan;
    private long _resolvedId = -1;
    private PersistingComponentStateSubscription _persistingSubscription;

    protected bool RawDataExpanded => _rawDataExpanded;

    private string PersistenceKey => $"AuditLogDetails.{Id}";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<AuditLogPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            ViewModel = restored.ViewModel;
            NotFound = restored.ViewModel is null;
            _resolvedId = Id;
            _needsLocalTimeScan = true;
            return;
        }

        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        ResolvedReturnUrl = string.IsNullOrEmpty(ReturnUrl) ? "/Admin/Logs?tab=audit" : ReturnUrl;

        if (_resolvedId != Id)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _resolvedId = Id;
        Logger.LogDebug("Loading audit log details for entry ID {EntryId}", Id);

        var log = await AuditLogService.GetByIdAsync(Id);
        if (log is null)
        {
            Logger.LogWarning("Audit log entry {EntryId} not found", Id);
            ViewModel = null;
            NotFound = true;
            return;
        }

        IReadOnlyList<AuditLogDto>? relatedEntries = null;
        if (!string.IsNullOrEmpty(log.CorrelationId))
        {
            relatedEntries = await AuditLogService.GetByCorrelationIdAsync(log.CorrelationId);
        }

        ViewModel = AuditLogDetailViewModel.FromDto(log, relatedEntries);
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

    protected void ToggleRawData() => _rawDataExpanded = !_rawDataExpanded;

    protected async Task CopyEntryIdAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        var copied = await BrowserInterop.CopyToClipboardAsync(ViewModel.Id.ToString());
        if (copied)
        {
            Toast.Success("Copied to clipboard");
        }
        else
        {
            Toast.Error("Failed to copy to clipboard");
        }
    }

    protected async Task ExportJsonAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        var json = BuildExportJson(ViewModel);
        await BrowserInterop.DownloadFileAsync($"audit-entry-{ViewModel.Id}.json", json);
    }

    /// <summary>
    /// Reproduces the legacy inline script's <c>jsonData</c> object shape and key order
    /// (entryId, timestamp, category, action, actor, target, guild?, ipAddress?, correlationId?,
    /// details?) with <c>System.Text.Json</c> instead of string interpolation - <c>details</c>
    /// is parsed back into a <see cref="JsonNode"/> so it nests as a real object in the exported
    /// file the same way <c>@@Html.Raw(Model.ViewModel.Details)</c> did, falling back to the raw
    /// string if it somehow isn't valid JSON.
    /// </summary>
    private static string BuildExportJson(AuditLogDetailViewModel vm)
    {
        var root = new JsonObject
        {
            ["entryId"] = vm.Id,
            ["timestamp"] = DateTime.SpecifyKind(vm.Timestamp, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["category"] = vm.Category,
            ["action"] = vm.Action,
            ["actor"] = new JsonObject
            {
                ["id"] = vm.ActorId,
                ["name"] = vm.ActorName,
                ["type"] = vm.ActorTypeName
            },
            ["target"] = new JsonObject
            {
                ["type"] = vm.TargetType,
                ["id"] = vm.TargetId
            }
        };

        if (vm.HasGuild)
        {
            root["guild"] = new JsonObject
            {
                ["id"] = vm.GuildId!.Value.ToString(),
                ["name"] = vm.GuildName
            };
        }

        if (!string.IsNullOrEmpty(vm.IpAddress))
        {
            root["ipAddress"] = vm.IpAddress;
        }

        if (!string.IsNullOrEmpty(vm.CorrelationId))
        {
            root["correlationId"] = vm.CorrelationId;
        }

        if (vm.HasDetails)
        {
            try
            {
                root["details"] = JsonNode.Parse(vm.Details!);
            }
            catch (JsonException)
            {
                root["details"] = vm.Details;
            }
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new AuditLogPersistedState(ViewModel));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Persisted shape for the prerender-to-circuit round trip - <see langword="null"/> <see cref="ViewModel"/> means "not found", distinguishing that from "not yet loaded".</summary>
    private sealed record AuditLogPersistedState(AuditLogDetailViewModel? ViewModel);
}
