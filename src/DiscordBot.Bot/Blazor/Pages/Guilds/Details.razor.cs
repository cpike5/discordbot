using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). <see cref="Aggregate"/> holds the raw
/// <see cref="GuildDetailsAggregateDto"/> straight from <see cref="IGuildDetailsAggregator"/> - the
/// six dashboard widgets and the activity table read it directly in Details.razor's markup instead
/// of through the legacy page's raw-HTML-string <c>DashboardWidgetViewModel.BodyContent</c>, per
/// the cluster brief ("render real markup with the Phase 2 Card/EmptyState/Badge").
/// </summary>
public partial class Details : GuildPageBase
{
    private const int RecentCommandsLimit = 10;

    [Inject]
    private IGuildDetailsAggregator Aggregator { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected GuildDetailsAggregateDto? Aggregate { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected bool IsSyncing { get; private set; }
    protected bool MoreActionsOpen { get; private set; }

    private ElementReference _moreActionsRef;
    private DotNetObjectReference<Details>? _selfRef;
    private int? _clickOutsideHandle;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var guild = Guild;
        if (guild is null)
        {
            return;
        }

        LoadFailed = false;

        try
        {
            Aggregate = await Aggregator.BuildAsync(guild.GuildId, RecentCommandsLimit, default);
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load guild details for guild {GuildId}", GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load guild details.");
        }
    }

    protected async Task HandleSyncAsync()
    {
        var guild = Guild;
        if (guild is null || IsSyncing)
        {
            return;
        }

        IsSyncing = true;
        MoreActionsOpen = false;

        try
        {
            var success = await ScopeFactory.RunAsync<IGuildService, bool>(s => s.SyncGuildAsync(guild.GuildId));
            if (success)
            {
                Toast.Success("Guild synced successfully.");
                Aggregate = await ScopeFactory.RunAsync<IGuildDetailsAggregator, GuildDetailsAggregateDto?>(
                    a => a.BuildAsync(guild.GuildId, RecentCommandsLimit, default));
                RequestLocalTimeScan();
            }
            else
            {
                Toast.Error("Guild not found in Discord client.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error syncing guild {GuildId}", GuildId);
            Toast.Error("An error occurred while syncing the guild.");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    protected void ToggleMoreActions() => MoreActionsOpen = !MoreActionsOpen;

    protected async Task HandleCopyGuildIdAsync()
    {
        MoreActionsOpen = false;
        if (Guild is null)
        {
            return;
        }

        var copied = await BrowserInterop.CopyToClipboardAsync(Guild.GuildIdString);
        if (copied)
        {
            Toast.Success("Guild ID copied to clipboard.");
        }
        else
        {
            Toast.Error("Couldn't copy the guild ID.");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _selfRef = DotNetObjectReference.Create(this);
            _clickOutsideHandle = await BrowserInterop.OnClickOutsideAsync(_moreActionsRef, _selfRef);
        }
    }

    [JSInvokable]
    public async Task OnClickOutsideMoreActions()
    {
        if (MoreActionsOpen)
        {
            MoreActionsOpen = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected static string GetRelativeTime(DateTime utc)
    {
        var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    public override void Dispose()
    {
        if (_clickOutsideHandle is { } handle)
        {
            _ = BrowserInterop.OffClickOutsideAsync(handle);
        }

        _selfRef?.Dispose();
        base.Dispose();
    }
}
