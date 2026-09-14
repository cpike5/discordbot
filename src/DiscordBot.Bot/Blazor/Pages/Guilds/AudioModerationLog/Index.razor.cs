using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Guilds;
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
/// Filters and paging live in component state seeded once from the initial
/// <c>[SupplyParameterFromQuery]</c> values (so a bookmarked/shared URL with
/// <c>pageNumber</c>/<c>FeatureFilter</c>/etc. loads correctly), then updated imperatively by
/// <see cref="HandleFilterSubmit"/>/<see cref="HandlePageChanged"/> rather than through
/// <c>OnParametersSetAsync</c>: <see cref="GuildPageBase"/> seals that lifecycle method to only
/// re-resolve when <c>GuildId</c> itself changes, so a page under it that also wants
/// query-string-driven filtering reloads its own data directly from the handler that changes the
/// URL, instead of relying on a parameter-change notification that will never come for a
/// same-guild query change.
/// </remarks>
public partial class Index : GuildPageBase
{
    private const int DefaultPageSize = 25;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int PageNumber { get; set; } = 1;

    [SupplyParameterFromQuery(Name = "pageSize")]
    [Parameter]
    public int PageSize { get; set; } = DefaultPageSize;

    [SupplyParameterFromQuery(Name = "FeatureFilter")]
    [Parameter]
    public AudioFeatureType? FeatureFilter { get; set; }

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
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<AudioPlaybackLog> LogEntries { get; private set; } = [];
    protected int CurrentPage { get; private set; } = 1;
    protected int CurrentPageSize { get; private set; } = DefaultPageSize;
    protected int TotalCount { get; private set; }
    protected int TotalPages { get; private set; }

    /// <summary>Live filter-form values, seeded from the query and only applied to the query on submit.</summary>
    protected AudioFeatureType? FeatureFilterInput { get; set; }
    protected string? UserFilterInput { get; set; }
    protected DateTime? DateFromInput { get; set; }
    protected DateTime? DateToInput { get; set; }

    protected bool HasActiveFilters =>
        FeatureFilter.HasValue || !string.IsNullOrWhiteSpace(UserFilter) || DateFrom.HasValue || DateTo.HasValue;

    private bool _seeded;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        if (!_seeded)
        {
            _seeded = true;
            SeedInputsFromQuery();
        }

        await LoadAsync();
    }

    private void SeedInputsFromQuery()
    {
        CurrentPage = Math.Max(1, PageNumber);
        CurrentPageSize = PageSize is < 1 or > 100 ? DefaultPageSize : PageSize;
        FeatureFilterInput = FeatureFilter;
        UserFilterInput = UserFilter;
        DateFromInput = DateFrom;
        DateToInput = DateTo;
    }

    private async Task LoadAsync()
    {
        var guildId = (ulong)GuildId;

        ulong? userIdFilter = null;
        if (!string.IsNullOrWhiteSpace(UserFilter) && ulong.TryParse(UserFilter.Trim(), out var parsedUserId))
        {
            userIdFilter = parsedUserId;
        }

        var adjustedDateTo = DateTo?.Date.AddDays(1).AddTicks(-1);

        var (items, totalCount) = await AudioPlaybackLogRepository.GetPagedAsync(
            guildId, CurrentPage, CurrentPageSize, FeatureFilter, userIdFilter, DateFrom, adjustedDateTo);

        LogEntries = items;
        TotalCount = totalCount;
        TotalPages = CurrentPageSize > 0 ? (int)Math.Ceiling((double)totalCount / CurrentPageSize) : 0;

        Logger.LogDebug(
            "Retrieved {Count} audio log entries for guild {GuildId} (page {Page} of {TotalPages})",
            LogEntries.Count, guildId, CurrentPage, TotalPages);
    }

    protected async Task HandleFilterSubmit()
    {
        FeatureFilter = FeatureFilterInput;
        UserFilter = UserFilterInput;
        DateFrom = DateFromInput;
        DateTo = DateToInput;
        CurrentPage = 1;
        await ApplyAsync();
    }

    protected async Task HandleClearFilters()
    {
        FeatureFilter = null;
        UserFilter = null;
        DateFrom = null;
        DateTo = null;
        FeatureFilterInput = null;
        UserFilterInput = null;
        DateFromInput = null;
        DateToInput = null;
        CurrentPage = 1;
        CurrentPageSize = DefaultPageSize;
        await ApplyAsync();
    }

    protected async Task HandlePageChanged(int page)
    {
        CurrentPage = page;
        await ApplyAsync();
    }

    protected async Task HandlePageSizeChanged(int pageSize)
    {
        CurrentPageSize = pageSize;
        CurrentPage = 1;
        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        Nav.NavigateTo(BuildUrl(), replace: true);
        await LoadAsync();
        StateHasChanged();
    }

    private string BuildUrl()
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
        if (CurrentPage > 1)
        {
            parts.Add($"pageNumber={CurrentPage}");
        }
        if (CurrentPageSize != DefaultPageSize)
        {
            parts.Add($"pageSize={CurrentPageSize}");
        }

        var basePath = $"/Guilds/AudioModerationLog/{GuildId}";
        return parts.Count == 0 ? basePath : $"{basePath}?{string.Join('&', parts)}";
    }

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
