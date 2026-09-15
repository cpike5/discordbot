using System.Security.Claims;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Admin.UserPurge;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/UserPurge.cshtml</c> +
/// <c>UserPurgeModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Reproduces the
/// GET-driven preview (<c>DiscordUserId</c> in the query string), the cannot-purge gate, and the
/// typed confirm whose required text is the looked-up user's own Discord ID (dynamic per lookup,
/// unlike BulkPurge's fixed "CONFIRM").
/// </summary>
public partial class Index : ComponentBase
{
    [SupplyParameterFromQuery(Name = "DiscordUserId")]
    [Parameter]
    public string? DiscordUserId { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject] private IUserPurgeService PurgeService { get; set; } = default!;
    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IToastService Toast { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    protected string? DiscordUserIdInput { get; set; }
    protected string? LookupError { get; set; }
    protected string? CannotPurgeReason { get; set; }
    protected bool ShowPreview { get; set; }
    protected UserPurgeResultDto? PreviewResult { get; set; }
    protected UserPurgeResultDto? PurgeResult { get; set; }
    protected string? ResultMessage { get; set; }

    private ConfirmModal? _purgeModal;
    private string? _resolvedDiscordUserId;

    protected override async Task OnInitializedAsync()
    {
        DiscordUserIdInput = DiscordUserId;
        await LookupAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DiscordUserId != _resolvedDiscordUserId)
        {
            DiscordUserIdInput = DiscordUserId;
            await LookupAsync();
        }
    }

    private async Task LookupAsync()
    {
        _resolvedDiscordUserId = DiscordUserId;
        CannotPurgeReason = null;
        ShowPreview = false;
        PreviewResult = null;
        LookupError = null;

        if (string.IsNullOrEmpty(DiscordUserId))
        {
            return;
        }

        if (!ulong.TryParse(DiscordUserId, out var userId))
        {
            LookupError = "Invalid Discord User ID format.";
            return;
        }

        var (canPurge, reason) = await PurgeService.CanPurgeUserAsync(userId);
        if (!canPurge)
        {
            CannotPurgeReason = reason;
            return;
        }

        PreviewResult = await PurgeService.PreviewPurgeAsync(userId);
        if (PreviewResult.Success)
        {
            ShowPreview = true;
        }
        else
        {
            Toast.Error(PreviewResult.ErrorMessage ?? "Failed to generate preview");
        }
    }

    protected void HandleLookup()
    {
        ResultMessage = null;
        PurgeResult = null;
        var target = string.IsNullOrWhiteSpace(DiscordUserIdInput) ? "" : DiscordUserIdInput.Trim();
        NavigationManager.NavigateTo(string.IsNullOrEmpty(target)
            ? "/Admin/UserPurge"
            : $"/Admin/UserPurge?DiscordUserId={Uri.EscapeDataString(target)}");
    }

    protected async Task RequestPurge()
    {
        var confirmed = _purgeModal is not null && await _purgeModal.ShowAsync();
        if (confirmed)
        {
            await ExecutePurgeAsync();
        }
    }

    private async Task ExecutePurgeAsync()
    {
        if (string.IsNullOrEmpty(DiscordUserId) || !ulong.TryParse(DiscordUserId, out var userId))
        {
            LookupError = "Invalid Discord User ID format.";
            return;
        }

        var (canPurge, reason) = await PurgeService.CanPurgeUserAsync(userId);
        if (!canPurge)
        {
            CannotPurgeReason = reason;
            ResultMessage = reason;
            return;
        }

        var user = AuthenticationStateTask is not null ? (await AuthenticationStateTask).User : new ClaimsPrincipal(new ClaimsIdentity());
        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        Logger.LogInformation("Admin {AdminId} initiating purge for Discord user {DiscordUserId}", adminUserId, userId);

        PurgeResult = await ScopeFactory.RunAsync<IUserPurgeService, UserPurgeResultDto>(
            s => s.PurgeUserDataAsync(userId, PurgeInitiator.Admin, adminUserId));

        if (PurgeResult.Success)
        {
            var totalDeleted = PurgeResult.DeletedCounts.Values.Sum();
            ResultMessage = $"User data purged successfully. {totalDeleted} records deleted. Correlation ID: {PurgeResult.AuditLogCorrelationId}";
            Toast.Success(ResultMessage);
            ShowPreview = false;
            Logger.LogInformation("Successfully purged data for Discord user {DiscordUserId}. Total records: {TotalDeleted}", userId, totalDeleted);
        }
        else
        {
            ResultMessage = PurgeResult.ErrorMessage ?? "An error occurred during purge.";
            Toast.Error(ResultMessage);
            Logger.LogError("Failed to purge data for Discord user {DiscordUserId}: {Error}", userId, PurgeResult.ErrorMessage);
        }
    }

    protected static string DisplayCategory(string category)
        => category.Replace("_Anonymized", "", StringComparison.OrdinalIgnoreCase);
}
