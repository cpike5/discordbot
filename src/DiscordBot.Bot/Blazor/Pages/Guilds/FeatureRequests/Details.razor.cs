using System.Security.Claims;
using System.Text.Json;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.FeatureRequests;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/FeatureRequests/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Reproduces the
/// Approve/Reject handlers as component methods and the status-gated review-notes box.
/// </summary>
public partial class Details : GuildPageBase
{
    [Parameter]
    public Guid Id { get; set; }

    // AuthenticationStateTask is inherited (protected) from GuildPageBase - redeclaring it here
    // as a second [CascadingParameter] throws "declares more than one parameter matching the name
    // 'authenticationstatetask'" at render time (parameter names are case-insensitive).

    [Inject]
    private IFeatureRequestService Service { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected FeatureRequest? Item { get; private set; }
    protected bool NotFoundState { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected string ReviewNotes { get; set; } = string.Empty;

    private Guid _resolvedId;

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>'s field of the same name.</summary>
    private int _loadGeneration;

    protected GatheredRequirements? Gathered { get; private set; }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(Service);
    }

    /// <summary>Reloads when only <see cref="Id"/> changes (same guild) - see the identical note on
    /// <c>FeatureRequests/Index.razor.cs</c>'s <c>OnParametersSet</c> override.</summary>
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

    /// <summary>
    /// Loads the page's data through <paramref name="service"/> - the circuit-scoped
    /// <see cref="Service"/> for the initial and Id-change loads, a fresh scope's instance via
    /// <see cref="ScopeFactory"/> for the reload that follows <see cref="SetStatusAsync"/>'s
    /// approve/reject mutation (docs/architecture/patterns.md "Blazor Components" § Per-operation
    /// scopes).
    /// </summary>
    private async Task LoadAsync(IFeatureRequestService service)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedId = Id;

        try
        {
            var item = await service.GetByIdAsync(Id);
            if (generation != _loadGeneration)
            {
                // A newer load (a different Id navigated to, or a review action's reload) already
                // superseded this one - its result wins.
                return;
            }

            Item = item;
            NotFoundState = Item is null || Item.GuildId != (ulong)GuildId;
            if (NotFoundState)
            {
                Item = null;
                Gathered = null;
                return;
            }

            Gathered = TryParseGathered(Item!.GatheredRequirements);
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load feature request {RequestId} for guild {GuildId}", Id, GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load this feature request.");
        }
    }

    private static GatheredRequirements? TryParseGathered(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GatheredRequirements>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected bool ShowReviewNotesBox => Item is not null && Item.Status is
        FeatureRequestStatus.Submitted or FeatureRequestStatus.GeneratingDocs or FeatureRequestStatus.DocsGenerated;

    protected bool ShowApproveAnyway => Item is not null && Item.Status == FeatureRequestStatus.DocGenFailed;

    protected bool ShowReviewActions => ShowReviewNotesBox || ShowApproveAnyway;

    protected async Task ApproveAsync() => await SetStatusAsync(FeatureRequestStatus.Approved, "approved");

    protected async Task RejectAsync() => await SetStatusAsync(FeatureRequestStatus.Rejected, "rejected");

    private async Task SetStatusAsync(FeatureRequestStatus status, string verb)
    {
        if (Item is null)
        {
            return;
        }

        var reviewerId = await GetCurrentDiscordUserIdAsync();
        var notes = string.IsNullOrWhiteSpace(ReviewNotes) ? null : ReviewNotes;
        await ScopeFactory.RunAsync<IFeatureRequestService>(s => s.UpdateStatusAsync(Item.Id, status, reviewerId, notes));

        Logger.LogInformation("Admin {Verb} feature request {RequestId} in guild {GuildId}", verb, Item.Id, GuildId);
        Toast.Success($"Feature request {verb}.");
        await ScopeFactory.RunAsync<IFeatureRequestService>(LoadAsync);
        StateHasChanged();
    }

    private async Task<ulong?> GetCurrentDiscordUserIdAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return null;
        }

        var user = (await AuthenticationStateTask).User;
        var claim = user.FindFirst("discord:user_id");
        return claim is not null && ulong.TryParse(claim.Value, out var id) ? id : null;
    }
}
