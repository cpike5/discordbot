using Discord.WebSocket;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.AudioModerationLog;

/// <summary>
/// Page model for the Audio Moderation Log page.
/// Displays a paginated, filterable table of audio playback events for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PaginatedGuildPageModel
{
    private readonly IAudioPlaybackLogRepository _audioPlaybackLogRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IAudioPlaybackLogRepository audioPlaybackLogRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _audioPlaybackLogRepository = audioPlaybackLogRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;

        // Override base class defaults for audio log
        SortBy = "PlayedAt";
        SortDescending = true;
        PageSize = 25;
    }

    /// <summary>
    /// The Discord guild snowflake ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Optional filter by audio feature type.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public AudioFeatureType? FeatureFilter { get; set; }

    /// <summary>
    /// Optional filter by Discord user ID (entered as string to preserve precision).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? UserFilter { get; set; }

    /// <summary>
    /// Optional filter for entries on or after this date.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Optional filter for entries on or before this date.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// The audio playback log entries for the current page.
    /// </summary>
    public IReadOnlyList<AudioPlaybackLog> LogEntries { get; set; } = Array.Empty<AudioPlaybackLog>();

    /// <summary>
    /// The guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// The guild icon URL for the header.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Whether any filters are currently active.
    /// </summary>
    public bool HasActiveFilters =>
        FeatureFilter.HasValue ||
        !string.IsNullOrWhiteSpace(UserFilter) ||
        DateFrom.HasValue ||
        DateTo.HasValue;

    /// <summary>
    /// Resolves a Discord user ID to a display name using the Discord client.
    /// Falls back to the raw ID if the user cannot be resolved.
    /// </summary>
    public string ResolveUserName(ulong userId)
    {
        try
        {
            var guild = _discordClient.GetGuild(GuildId);
            var guildUser = guild?.GetUser(userId);
            if (guildUser != null)
                return guildUser.DisplayName;

            var user = _discordClient.GetUser(userId);
            if (user != null)
                return user.Username;
        }
        catch
        {
            // Ignore resolution failures
        }

        return userId.ToString();
    }

    /// <summary>
    /// Resolves a Discord channel ID to a channel name.
    /// Falls back to the raw ID if the channel cannot be resolved.
    /// </summary>
    public string ResolveChannelName(ulong channelId)
    {
        try
        {
            var guild = _discordClient.GetGuild(GuildId);
            var channel = guild?.GetVoiceChannel(channelId);
            if (channel != null)
                return channel.Name;
        }
        catch
        {
            // Ignore resolution failures
        }

        return channelId.ToString();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User accessing Audio Moderation Log for guild {GuildId}. FeatureFilter={FeatureFilter}, UserFilter={UserFilter}, Page={Page}",
            GuildId, FeatureFilter, UserFilter, CurrentPage);

        // Validate pagination
        if (CurrentPage < 1) CurrentPage = 1;
        if (PageSize < 1 || PageSize > 100) PageSize = 25;

        // Get guild info
        var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        GuildName = guild.Name;
        GuildIconUrl = guild.IconUrl;

        // Parse user filter to ulong if provided
        ulong? userIdFilter = null;
        if (!string.IsNullOrWhiteSpace(UserFilter) && ulong.TryParse(UserFilter.Trim(), out var parsedUserId))
        {
            userIdFilter = parsedUserId;
        }

        // Adjust DateTo to include the entire day
        var adjustedDateTo = DateTo?.Date.AddDays(1).AddTicks(-1);

        // Query the repository
        var (items, totalCount) = await _audioPlaybackLogRepository.GetPagedAsync(
            GuildId,
            CurrentPage,
            PageSize,
            FeatureFilter,
            userIdFilter,
            DateFrom,
            adjustedDateTo,
            cancellationToken);

        LogEntries = items;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);

        _logger.LogDebug(
            "Retrieved {Count} audio log entries for guild {GuildId} (page {Page} of {TotalPages})",
            LogEntries.Count, GuildId, CurrentPage, TotalPages);

        // Populate guild layout
        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "audio", "Audio Log",
            $"Audio playback history for {guild.Name}");

        return Page();
    }
}
