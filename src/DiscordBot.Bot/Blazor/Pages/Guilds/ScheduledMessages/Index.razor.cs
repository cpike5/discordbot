using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/ScheduledMessages/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// </summary>
public partial class Index : GuildPageBase
{
    private const int PageSize = 20;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback query name, used only when <see cref="PageNumber"/> is absent.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [Inject]
    private IScheduledMessageService ScheduledMessageService { get; set; } = default!;

    [Inject]
    private IDiscordChannelResolver ChannelResolver { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    protected IReadOnlyList<ScheduledMessageListItem> Messages { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;

    private ConfirmModal? _deleteModal;
    private ScheduledMessageListItem? _pendingDelete;
    private int? _resolvedPageNumber;

    protected string DeleteModalMessage => _pendingDelete is null
        ? string.Empty
        : $"Are you sure you want to delete \"{_pendingDelete.MessagePreview}\"? This action cannot be undone.";

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

        if (Guild is not null && _resolvedPageNumber != (PageNumber ?? LegacyPage))
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
        _resolvedPageNumber = PageNumber ?? LegacyPage;
        Query = PagedQuery.FromQuery(PageNumber, null, defaultPageSize: PageSize, legacyPage: LegacyPage);

        var guildId = (ulong)GuildId;
        var (messages, total) = await ScheduledMessageService.GetByGuildIdAsync(guildId, Query.PageNumber, Query.PageSize);

        var listViewModel = ScheduledMessageListViewModel.Create(
            guildId,
            Guild?.Guild.Name ?? string.Empty,
            Guild?.Guild.IconUrl,
            messages,
            channelId => ChannelResolver.ResolveChannelName(guildId, channelId),
            Query.PageNumber,
            Query.PageSize,
            total);

        Messages = listViewModel.Messages;
        TotalCount = total;
        RequestLocalTimeScan();
    }

    protected string PageUrl => $"/Guilds/ScheduledMessages/{GuildId}";

    protected async Task ToggleAsync(ScheduledMessageListItem item)
    {
        var result = await ScheduledMessageService.UpdateAsync(item.Id, new ScheduledMessageUpdateDto { IsEnabled = !item.IsEnabled });
        if (result is not null)
        {
            Toast.Success($"Scheduled message {(result.IsEnabled ? "resumed" : "paused")} successfully.");
        }
        else
        {
            Toast.Error("Failed to update scheduled message.");
        }

        await LoadAsync();
    }

    protected async Task RequestDelete(ScheduledMessageListItem item)
    {
        _pendingDelete = item;
        var confirmed = _deleteModal is not null && await _deleteModal.ShowAsync();
        if (confirmed)
        {
            await ConfirmDeleteAsync();
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDelete is null)
        {
            return;
        }

        var success = await ScheduledMessageService.DeleteAsync(_pendingDelete.Id);
        Toast.Success(success ? "Scheduled message deleted successfully." : "Scheduled message not found. It may have already been deleted.");

        _pendingDelete = null;
        await LoadAsync();
        StateHasChanged();
    }
}
