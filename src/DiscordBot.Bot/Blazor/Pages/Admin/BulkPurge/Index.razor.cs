using System.Security.Claims;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Admin.BulkPurge;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/BulkPurge.cshtml</c> +
/// <c>BulkPurgeModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Reproduces
/// <c>BuildCriteria</c>'s validation. The progress bar subscribes to
/// <see cref="IDashboardEventBus"/>'s <see cref="BulkPurgeProgressEvent"/> before the purge starts
/// (not guild-scoped - a purge can span every guild) and unsubscribes once the operation completes
/// or the component is disposed.
/// </summary>
public partial class Index : ComponentBase, IAsyncDisposable
{
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = default!;
    [Inject] private IDashboardEventBus EventBus { get; set; } = default!;
    [Inject] private IToastService Toast { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    protected BulkPurgeEntityType? EntityType { get; set; }
    protected DateTime? StartDate { get; set; }
    protected DateTime? EndDate { get; set; }
    protected string? GuildIdInput { get; set; }

    protected string? EntityTypeError { get; set; }
    protected string? DateError { get; set; }
    protected string? GuildIdError { get; set; }

    protected BulkPurgePreviewDto? PreviewResult { get; set; }
    protected BulkPurgeResultDto? PurgeResult { get; set; }
    protected string? ResultMessage { get; set; }

    protected bool IsBusy { get; set; }
    protected bool IsPurging { get; set; }
    protected int ProgressPercent { get; set; }
    protected int ProgressProcessed { get; set; }
    protected int ProgressTotal { get; set; }
    protected string? ProgressMessage { get; set; }

    private ConfirmModal? _executeModal;
    private IDisposable? _progressSubscription;

    protected void SelectEntityType(BulkPurgeEntityType entityType)
    {
        EntityType = entityType;
        PreviewResult = null;
    }

    protected async Task PreviewAsync()
    {
        var criteria = BuildCriteria();
        if (criteria is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            PreviewResult = await ScopeFactory.RunAsync<IBulkPurgeService, BulkPurgePreviewDto>(s => s.PreviewPurgeAsync(criteria));
            if (!PreviewResult.Success)
            {
                Toast.Error(PreviewResult.ErrorMessage ?? "Failed to generate preview.");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task RequestExecute()
    {
        var confirmed = _executeModal is not null && await _executeModal.ShowAsync();
        if (confirmed)
        {
            await ExecuteAsync();
        }
    }

    private async Task ExecuteAsync()
    {
        var criteria = BuildCriteria();
        if (criteria is null)
        {
            return;
        }

        var user = AuthenticationStateTask is not null ? (await AuthenticationStateTask).User : new ClaimsPrincipal(new ClaimsIdentity());
        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        ResultMessage = null;
        PurgeResult = null;
        ProgressPercent = 0;
        ProgressProcessed = 0;
        ProgressTotal = 0;
        ProgressMessage = null;
        IsPurging = true;

        _progressSubscription = EventBus.Subscribe<BulkPurgeProgressEvent>((evt, _) =>
        {
            ProgressProcessed = evt.Progress.ProcessedCount;
            ProgressTotal = evt.Progress.TotalCount;
            ProgressPercent = evt.Progress.PercentComplete;
            ProgressMessage = evt.Progress.Message;
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        });

        try
        {
            PurgeResult = await ScopeFactory.RunAsync<IBulkPurgeService, BulkPurgeResultDto>(s => s.ExecutePurgeAsync(criteria, adminUserId));

            if (PurgeResult.Success)
            {
                ResultMessage = $"Successfully purged {PurgeResult.DeletedCount:N0} {PurgeResult.EntityType} records. Correlation ID: {PurgeResult.AuditLogCorrelationId}";
                Toast.Success(ResultMessage);
                Logger.LogInformation("Bulk purge completed by {AdminUserId}: {DeletedCount} {EntityType} records deleted", adminUserId, PurgeResult.DeletedCount, PurgeResult.EntityType);
                PreviewResult = null;
            }
            else
            {
                ResultMessage = PurgeResult.ErrorMessage ?? "An error occurred during purge.";
                Toast.Error(ResultMessage);
                Logger.LogError("Bulk purge failed for {EntityType}: {Error}", criteria.EntityType, PurgeResult.ErrorMessage);
            }
        }
        finally
        {
            IsPurging = false;
            _progressSubscription?.Dispose();
            _progressSubscription = null;
        }
    }

    private BulkPurgeCriteriaDto? BuildCriteria()
    {
        EntityTypeError = null;
        DateError = null;
        GuildIdError = null;

        if (EntityType is null || !Enum.IsDefined(EntityType.Value))
        {
            EntityTypeError = "Please select an entity type.";
            return null;
        }

        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            DateError = "Start date cannot be after end date.";
            return null;
        }

        ulong? guildId = null;
        if (!string.IsNullOrWhiteSpace(GuildIdInput))
        {
            if (!ulong.TryParse(GuildIdInput, out var parsedGuildId))
            {
                GuildIdError = "Invalid Guild ID format.";
                return null;
            }

            guildId = parsedGuildId;
        }

        return new BulkPurgeCriteriaDto
        {
            EntityType = EntityType.Value,
            StartDate = StartDate?.ToUniversalTime(),
            EndDate = EndDate?.ToUniversalTime(),
            GuildId = guildId
        };
    }

    public ValueTask DisposeAsync()
    {
        _progressSubscription?.Dispose();
        return ValueTask.CompletedTask;
    }
}
