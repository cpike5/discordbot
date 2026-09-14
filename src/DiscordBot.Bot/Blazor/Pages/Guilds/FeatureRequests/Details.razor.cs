using System.Security.Claims;
using System.Text.Json;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

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
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected FeatureRequest? Item { get; private set; }
    protected bool NotFoundState { get; private set; }
    protected string ReviewNotes { get; set; } = string.Empty;

    private Guid _resolvedId;

    protected GatheredRequirements? Gathered { get; private set; }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync();
    }

    /// <summary>Reloads when only <see cref="Id"/> changes (same guild) - see the identical note on
    /// <c>FeatureRequests/Index.razor.cs</c>'s <c>OnParametersSet</c> override.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedId != Id)
        {
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
        Item = await Service.GetByIdAsync(Id);
        NotFoundState = Item is null || Item.GuildId != (ulong)GuildId;
        if (NotFoundState)
        {
            Item = null;
            Gathered = null;
            return;
        }

        Gathered = TryParseGathered(Item!.GatheredRequirements);
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
        await Service.UpdateStatusAsync(Item.Id, status, reviewerId, string.IsNullOrWhiteSpace(ReviewNotes) ? null : ReviewNotes);

        Logger.LogInformation("Admin {Verb} feature request {RequestId} in guild {GuildId}", verb, Item.Id, GuildId);
        Toast.Success($"Feature request {verb}.");
        await LoadAsync();
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
