using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Logs.Tabs;

/// <summary>
/// Code-behind for <c>MessagesTab.razor</c> - filters, autocomplete search functions, paging and
/// data loading for the Logs page's "messages" tab. Reproduces
/// <c>Pages/Admin/Logs/Index.cshtml.cs</c>'s <c>LoadMessageLogsAsync</c>/
/// <c>PopulateMessageDisplayNamesAsync</c>.
/// </summary>
public partial class MessagesTab : ComponentBase
{
    private const int DefaultPageSize = 25;

    // Bound as string, not ulong? - QueryParameterValueSupplier only knows a fixed set of
    // primitive types for [SupplyParameterFromQuery] (string/bool/DateTime/decimal/double/float/
    // Guid/int/long and their nullable/array forms) and throws InvalidOperationException for
    // ulong (a Discord snowflake), the same reason every other guild-list filter in this codebase
    // binds an id as a raw string and parses it - see Guilds/AudioModerationLog/Index.razor.cs.
    [SupplyParameterFromQuery(Name = "AuthorId")]
    [Parameter]
    public string? AuthorIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "MessageGuildId")]
    [Parameter]
    public string? MessageGuildIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "ChannelId")]
    [Parameter]
    public string? ChannelIdQuery { get; set; }

    protected ulong? AuthorId => ulong.TryParse(AuthorIdQuery, out var authorId) ? authorId : null;
    protected ulong? MessageGuildId => ulong.TryParse(MessageGuildIdQuery, out var guildId) ? guildId : null;
    protected ulong? ChannelId => ulong.TryParse(ChannelIdQuery, out var channelId) ? channelId : null;

    [SupplyParameterFromQuery(Name = "MessageSource")]
    [Parameter]
    public string? MessageSourceFilter { get; set; }

    [SupplyParameterFromQuery(Name = "MessageStartDate")]
    [Parameter]
    public DateTime? MessageStartDate { get; set; }

    [SupplyParameterFromQuery(Name = "MessageEndDate")]
    [Parameter]
    public DateTime? MessageEndDate { get; set; }

    [SupplyParameterFromQuery(Name = "MessageSearchTerm")]
    [Parameter]
    public string? MessageSearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "messagePageNumber")]
    [Parameter]
    public int MessagePageNumber { get; set; } = 1;

    [SupplyParameterFromQuery(Name = "MessagePageSize")]
    [Parameter]
    public int? MessagePageSizeParam { get; set; }

    [Inject] private IMessageLogService MessageLogService { get; set; } = default!;
    [Inject] private IMessageLogRepository MessageLogRepository { get; set; } = default!;
    [Inject] private IGuildService GuildService { get; set; } = default!;
    [Inject] private DiscordSocketClient DiscordClient { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private BrowserInterop BrowserInterop { get; set; } = default!;
    [Inject] private ILogger<MessagesTab> Logger { get; set; } = default!;

    protected IReadOnlyList<MessageLogDto> Messages { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int PageNumber { get; private set; } = 1;
    protected int PageSize { get; private set; } = DefaultPageSize;
    protected int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    protected bool IsLoading { get; private set; } = true;
    protected bool LoadFailed { get; private set; }

    protected string? AuthorIdInput { get; set; }
    protected string? AuthorDisplayText { get; set; }
    protected string? GuildIdInput { get; set; }
    protected string? GuildDisplayText { get; set; }
    protected string? ChannelIdInput { get; set; }
    protected string? ChannelDisplayText { get; set; }
    protected string SourceInput { get; set; } = "";
    protected string? SearchTermInput { get; set; }
    protected DateOnly? StartInput { get; set; }
    protected DateOnly? EndInput { get; set; }

    private bool _needsLocalTimeScan;
    private (ulong?, ulong?, ulong?, string?, DateTime?, DateTime?, string?, int, int?) _resolvedQuery;

    protected static readonly List<SelectOption> SourceOptions =
    [
        new() { Value = "", Text = "All Sources" },
        new() { Value = nameof(MessageSource.DirectMessage), Text = "Direct Message" },
        new() { Value = nameof(MessageSource.ServerChannel), Text = "Server Channel" }
    ];

    protected string PageUrl => BuildUrl(1);

    protected override async Task OnInitializedAsync()
    {
        SeedInputsFromQuery();
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedQuery != CurrentQuery)
        {
            SeedInputsFromQuery();
            await LoadAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLocalTimeScan)
        {
            _needsLocalTimeScan = false;
            await BrowserInterop.ConvertLocalTimesAsync();
        }
    }

    private (ulong?, ulong?, ulong?, string?, DateTime?, DateTime?, string?, int, int?) CurrentQuery =>
        (AuthorId, MessageGuildId, ChannelId, MessageSourceFilter, MessageStartDate, MessageEndDate, MessageSearchTerm, MessagePageNumber, MessagePageSizeParam);

    private void SeedInputsFromQuery()
    {
        AuthorIdInput = AuthorId?.ToString();
        GuildIdInput = MessageGuildId?.ToString();
        ChannelIdInput = ChannelId?.ToString();
        SourceInput = MessageSourceFilter ?? "";
        SearchTermInput = MessageSearchTerm;
        StartInput = MessageStartDate.HasValue ? DateOnly.FromDateTime(MessageStartDate.Value) : null;
        EndInput = MessageEndDate.HasValue ? DateOnly.FromDateTime(MessageEndDate.Value) : null;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadFailed = false;
        _resolvedQuery = CurrentQuery;
        StateHasChanged();

        try
        {
            var startDate = MessageStartDate;
            var endDate = MessageEndDate;
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = DateTime.UtcNow.Date.AddDays(-7);
                endDate = DateTime.UtcNow.Date.AddDays(1);
            }

            MessageSource? sourceFilter = null;
            if (!string.IsNullOrEmpty(MessageSourceFilter) && Enum.TryParse<MessageSource>(MessageSourceFilter, true, out var parsed))
            {
                sourceFilter = parsed;
            }

            PageSize = MessagePageSizeParam is > 0 and <= 100 ? MessagePageSizeParam.Value : DefaultPageSize;

            var query = new MessageLogQueryDto
            {
                AuthorId = AuthorId,
                GuildId = MessageGuildId,
                ChannelId = ChannelId,
                Source = sourceFilter,
                StartDate = startDate,
                EndDate = endDate,
                SearchTerm = MessageSearchTerm,
                Page = Math.Max(1, MessagePageNumber),
                PageSize = PageSize
            };

            var result = await MessageLogService.GetLogsAsync(query);

            await PopulateDisplayNamesAsync();

            Messages = result.Items;
            TotalCount = result.TotalCount;
            PageNumber = result.Page;
            PageSize = result.PageSize;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading message logs");
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            _needsLocalTimeScan = true;
        }
    }

    private async Task PopulateDisplayNamesAsync()
    {
        if (AuthorId.HasValue)
        {
            var messages = await MessageLogRepository.GetUserMessagesAsync(AuthorId.Value, limit: 1);
            AuthorDisplayText = messages.FirstOrDefault()?.User?.Username;
        }

        if (MessageGuildId.HasValue)
        {
            var guild = await GuildService.GetGuildByIdAsync(MessageGuildId.Value);
            GuildDisplayText = guild?.Name;
        }

        if (ChannelId.HasValue && MessageGuildId.HasValue)
        {
            var socketGuild = DiscordClient.GetGuild(MessageGuildId.Value);
            ChannelDisplayText = socketGuild?.GetChannel(ChannelId.Value)?.Name;
        }
    }

    protected async Task<IReadOnlyList<AutocompleteItem>> SearchAuthorsAsync(string term, CancellationToken ct)
    {
        var authors = await MessageLogRepository.SearchAuthorsAsync(term, guildId: null, limit: 25, ct);
        return authors.Select(a => new AutocompleteItem(a.UserId.ToString(), a.Username)).ToList();
    }

    protected async Task<IReadOnlyList<AutocompleteItem>> SearchGuildsAsync(string term, CancellationToken ct)
    {
        var guilds = await GuildService.GetAllGuildsAsync(ct);
        var lower = term.ToLowerInvariant();
        return guilds.Where(g => g.Name.ToLowerInvariant().Contains(lower))
            .Take(25)
            .Select(g => new AutocompleteItem(g.Id.ToString(), g.Name))
            .ToList();
    }

    protected Task<IReadOnlyList<AutocompleteItem>> SearchChannelsAsync(string term, CancellationToken ct)
    {
        if (!ulong.TryParse(GuildIdInput, out var guildId))
        {
            return Task.FromResult<IReadOnlyList<AutocompleteItem>>([]);
        }

        var guild = DiscordClient.GetGuild(guildId);
        if (guild is null)
        {
            return Task.FromResult<IReadOnlyList<AutocompleteItem>>([]);
        }

        var lower = term.ToLowerInvariant();
        var results = guild.Channels
            .Where(c => c.Name.ToLowerInvariant().Contains(lower))
            .OrderBy(c => c.Name)
            .Take(25)
            .Select(c => new AutocompleteItem(c.Id.ToString(), c.Name))
            .ToList();

        return Task.FromResult<IReadOnlyList<AutocompleteItem>>(results);
    }

    protected void HandleFilterSubmit() => NavigationManager.NavigateTo(BuildUrl(1));

    protected void HandleClearFilters() => NavigationManager.NavigateTo("/Admin/Logs?tab=messages");

    private string BuildUrl(int page)
    {
        var parts = new List<string> { "tab=messages" };

        if (ulong.TryParse(AuthorIdInput, out var authorId))
        {
            parts.Add($"AuthorId={authorId}");
        }

        if (ulong.TryParse(GuildIdInput, out var guildId))
        {
            parts.Add($"MessageGuildId={guildId}");
        }

        if (ulong.TryParse(ChannelIdInput, out var channelId))
        {
            parts.Add($"ChannelId={channelId}");
        }

        if (!string.IsNullOrEmpty(SourceInput))
        {
            parts.Add($"MessageSource={Uri.EscapeDataString(SourceInput)}");
        }

        if (StartInput.HasValue)
        {
            parts.Add($"MessageStartDate={StartInput.Value:yyyy-MM-dd}");
        }

        if (EndInput.HasValue)
        {
            parts.Add($"MessageEndDate={EndInput.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrEmpty(SearchTermInput))
        {
            parts.Add($"MessageSearchTerm={Uri.EscapeDataString(SearchTermInput)}");
        }

        if (page > 1)
        {
            parts.Add($"messagePageNumber={page}");
        }

        return $"/Admin/Logs?{string.Join('&', parts)}";
    }
}
