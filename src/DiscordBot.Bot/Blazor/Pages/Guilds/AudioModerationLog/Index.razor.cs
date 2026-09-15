using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.AudioModerationLog;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/AudioModerationLog/Index.cshtml</c>
/// + <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). A paginated,
/// filterable table of audio playback events, sorted <c>PlayedAt</c> descending, page size 25 -
/// same defaults as the legacy <c>PaginatedGuildPageModel</c> override.
/// </summary>
/// <remarks>
/// Filters and paging are query-string state: <c>[SupplyParameterFromQuery]</c> values seed a form-
/// staging set of "Input" properties (only applied to the query on submit, like every other filtered
/// guild list), and a separate <see cref="PagedQuery"/> built via <see cref="PagedQuery.FromQuery"/>
/// - matching <c>FeatureRequests/Index.razor.cs</c>/<c>Reminders/Index.razor.cs</c>
/// (docs/architecture/patterns.md, "Paged list pages") rather than the hand-rolled component-state
/// paging (a one-time <c>_seeded</c> flag, callback-mode <c>Pagination</c>) this page shipped with
/// initially, which changed the URL without the URL itself ever being the source of truth - so
/// browser back/forward across a page or filter change now reloads like every other paged guild
/// list. <see cref="GuildPageBase"/> seals <c>OnParametersSetAsync</c> to only re-resolve when
/// <c>GuildId</c> itself changes, so the unsealed synchronous <see cref="OnParametersSet"/> is what
/// notices a query-string-only change and kicks off the reload - see the identical note on
/// <c>FeatureRequests/Index.razor.cs</c>.
/// </remarks>
public partial class Index : GuildPageBase
{
    private const int PageSize = 25;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    [Parameter]
    public int? PageSizeQuery { get; set; }

    /// <summary>
    /// Query-bound as <c>int?</c>, not <see cref="AudioFeatureType"/>? directly -
    /// <c>QueryParameterValueSupplier</c> only knows a fixed set of primitive types for
    /// <c>[SupplyParameterFromQuery]</c> (string/bool/DateTime/decimal/double/float/Guid/int/long
    /// and their nullable/array forms) and throws <c>InvalidOperationException</c> for an arbitrary
    /// enum; <see cref="FeatureFilter"/> is the typed value derived from this on every (re)seed.
    /// </summary>
    [SupplyParameterFromQuery(Name = "FeatureFilter")]
    [Parameter]
    public int? FeatureFilterQuery { get; set; }

    protected AudioFeatureType? FeatureFilter => FeatureFilterQuery.HasValue ? (AudioFeatureType)FeatureFilterQuery.Value : null;

    [SupplyParameterFromQuery(Name = "UserFilter")]
    [Parameter]
    public string? UserFilter { get; set; }

    [SupplyParameterFromQuery(Name = "DateFrom")]
    [Parameter]
    public DateTime? DateFrom { get; set; }

    [SupplyParameterFromQuery(Name = "DateTo")]
    [Parameter]
    public DateTime? DateTo { get; set; }

    [Inject]
    private IAudioPlaybackLogRepository AudioPlaybackLogRepository { get; set; } = default!;

    [Inject]
    private DiscordSocketClient DiscordClient { get; set; } = default!;

    [Inject]
    private NavigationManager Nav { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<AudioPlaybackLog> LogEntries { get; private set; } = [];
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalCount { get; private set; }
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;

    /// <summary>Base URL (current filters, no page/size) for link-mode <c>Pagination</c> -
    /// its own <c>BuildUrl</c> strips and re-adds <c>pageNumber</c>/<c>pageSize</c>.</summary>
    protected string PageUrl
    {
        get
        {
            var parts = new List<string>();
            if (FeatureFilter.HasValue)
            {
                parts.Add($"FeatureFilter={(int)FeatureFilter.Value}");
            }
            if (!string.IsNullOrEmpty(UserFilter))
            {
                parts.Add($"UserFilter={Uri.EscapeDataString(UserFilter)}");
            }
            if (DateFrom.HasValue)
            {
                parts.Add($"DateFrom={DateFrom.Value:yyyy-MM-dd}");
            }
            if (DateTo.HasValue)
            {
                parts.Add($"DateTo={DateTo.Value:yyyy-MM-dd}");
            }

            var basePath = $"/Guilds/AudioModerationLog/{GuildId}";
            return parts.Count == 0 ? basePath : $"{basePath}?{string.Join('&', parts)}";
        }
    }

    /// <summary>Live filter-form values, seeded from the query and only applied to the query on submit.</summary>
    protected AudioFeatureType? FeatureFilterInput { get; set; }
    protected string? UserFilterInput { get; set; }
    protected DateTime? DateFromInput { get; set; }
    protected DateTime? DateToInput { get; set; }

    protected bool HasActiveFilters =>
        FeatureFilter.HasValue || !string.IsNullOrWhiteSpace(UserFilter) || DateFrom.HasValue || DateTo.HasValue;

    private (int?, int?, int?, string?, DateTime?, DateTime?) _resolvedQuery;

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>'s field of the same name.</summary>
    private int _loadGeneration;

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

        if (Guild is not null && _resolvedQuery != CurrentQueryKey)
        {
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync();
        StateHasChanged();
    }

    private (int?, int?, int?, string?, DateTime?, DateTime?) CurrentQueryKey =>
        (PageNumber, PageSizeQuery, FeatureFilterQuery, UserFilter, DateFrom, DateTo);

    private void SeedInputsFromQuery()
    {
        FeatureFilterInput = FeatureFilter;
        UserFilterInput = UserFilter;
        DateFromInput = DateFrom;
        DateToInput = DateTo;
    }

    private async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey;
        SeedInputsFromQuery();
        Query = PagedQuery.FromQuery(PageNumber, PageSizeQuery, defaultPageSize: PageSize);

        var guildId = (ulong)GuildId;

        ulong? userIdFilter = null;
        if (!string.IsNullOrWhiteSpace(UserFilter) && ulong.TryParse(UserFilter.Trim(), out var parsedUserId))
        {
            userIdFilter = parsedUserId;
        }

        var adjustedDateTo = DateTo?.Date.AddDays(1).AddTicks(-1);

        try
        {
            var (items, totalCount) = await AudioPlaybackLogRepository.GetPagedAsync(
                guildId, Query.PageNumber, Query.PageSize, FeatureFilter, userIdFilter, DateFrom, adjustedDateTo);

            if (generation != _loadGeneration)
            {
                // A newer load (another filter/page change) already superseded this one.
                return;
            }

            LogEntries = items;
            TotalCount = totalCount;
            RequestLocalTimeScan();

            Logger.LogDebug(
                "Retrieved {Count} audio log entries for guild {GuildId} (page {Page} of {TotalPages})",
                LogEntries.Count, guildId, Query.PageNumber, TotalPages);
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load audio moderation log for guild {GuildId}", guildId);
            LoadFailed = true;
            Toast.Error("Failed to load the audio log.");
        }
    }

    protected void HandleFilterSubmit()
    {
        var parts = new List<string>();
        if (FeatureFilterInput.HasValue)
        {
            parts.Add($"FeatureFilter={(int)FeatureFilterInput.Value}");
        }
        if (!string.IsNullOrEmpty(UserFilterInput))
        {
            parts.Add($"UserFilter={Uri.EscapeDataString(UserFilterInput)}");
        }
        if (DateFromInput.HasValue)
        {
            parts.Add($"DateFrom={DateFromInput.Value:yyyy-MM-dd}");
        }
        if (DateToInput.HasValue)
        {
            parts.Add($"DateTo={DateToInput.Value:yyyy-MM-dd}");
        }

        var basePath = $"/Guilds/AudioModerationLog/{GuildId}";
        Nav.NavigateTo(parts.Count == 0 ? basePath : $"{basePath}?{string.Join('&', parts)}");
    }

    protected void HandleClearFilters() => Nav.NavigateTo($"/Guilds/AudioModerationLog/{GuildId}");

    /// <summary>
    /// Resolves a Discord user ID to a display name via the gateway cache. Falls back to the raw
    /// ID string if the user cannot be resolved. Entries written before the portal claim fix carry
    /// no user (<paramref name="userId"/> is <c>0</c>) - callers show "Unknown" for that case
    /// rather than calling this.
    /// </summary>
    protected string ResolveUserName(ulong userId)
    {
        try
        {
            var guild = DiscordClient.GetGuild((ulong)GuildId);
            var guildUser = guild?.GetUser(userId);
            if (guildUser is not null)
            {
                return guildUser.DisplayName;
            }

            var user = DiscordClient.GetUser(userId);
            if (user is not null)
            {
                return user.Username;
            }
        }
        catch
        {
            // Ignore resolution failures.
        }

        return userId.ToString();
    }

    /// <summary>Resolves a Discord voice channel ID to its name, falling back to the raw ID.</summary>
    protected string ResolveChannelName(ulong channelId)
    {
        try
        {
            var guild = DiscordClient.GetGuild((ulong)GuildId);
            var channel = guild?.GetVoiceChannel(channelId);
            if (channel is not null)
            {
                return channel.Name;
            }
        }
        catch
        {
            // Ignore resolution failures.
        }

        return channelId.ToString();
    }

    /// <summary>
    /// Maps an audio feature type to its badge text/variant, mirroring the legacy
    /// <c>IndexModel.BuildFeatureBadge</c> (which built a Razor Pages <c>BadgeViewModel</c> for the
    /// <c>_Badge</c> partial; here the tuple feeds the Blazor <c>Badge</c> component's own
    /// parameters directly). Kept as a static per-page mapping method - no other ported page in
    /// this cluster shares an audio-feature badge vocabulary.
    /// </summary>
    public static (string Text, BadgeVariant Variant) FeatureBadge(AudioFeatureType featureType) => featureType switch
    {
        AudioFeatureType.Soundboard => ("Soundboard", BadgeVariant.Blue),
        AudioFeatureType.Tts => ("TTS", BadgeVariant.Success),
        AudioFeatureType.Vox => ("VOX", BadgeVariant.Orange),
        _ => (featureType.ToString(), BadgeVariant.Default)
    };
}
