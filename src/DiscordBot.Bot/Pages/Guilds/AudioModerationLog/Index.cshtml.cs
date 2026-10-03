using Discord.WebSocket;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using DiscordBot.Bot.ViewModels.Components;
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
    /// What is wrong with the user filter, when it is not a Discord user ID. The log then shows
    /// no rows rather than quietly ignoring the filter and listing everybody.
    /// </summary>
    public string? UserFilterError { get; private set; }

    /// <summary>
    /// What is wrong with the date range, when the end is before the start.
    /// </summary>
    public string? DateRangeError { get; private set; }

    /// <summary>
    /// Whether the filters can be applied. When not, no query runs and the page says why.
    /// </summary>
    public bool FiltersValid => UserFilterError == null && DateRangeError == null;

    /// <summary>
    /// Whether any filters are currently active.
    /// </summary>
    public bool HasActiveFilters =>
        FeatureFilter.HasValue ||
        !string.IsNullOrWhiteSpace(UserFilter) ||
        DateFrom.HasValue ||
        DateTo.HasValue;

    /// <summary>
    /// Builds the design-system badge for an audio feature type so the desktop table and the
    /// mobile cards stay in step.
    /// </summary>
    public static BadgeViewModel BuildFeatureBadge(AudioFeatureType featureType) => featureType switch
    {
        AudioFeatureType.Soundboard => new BadgeViewModel { Text = "Soundboard", Variant = BadgeVariant.Blue, IsPill = true },
        AudioFeatureType.Tts => new BadgeViewModel { Text = "TTS", Variant = BadgeVariant.Success, IsPill = true },
        AudioFeatureType.Vox => new BadgeViewModel { Text = "VOX", Variant = BadgeVariant.Orange, IsPill = true },
        _ => new BadgeViewModel { Text = featureType.ToString(), Variant = BadgeVariant.Default, IsPill = true }
    };

    /// <summary>
    /// Resolves a Discord user ID to a display name using the Discord client.
    /// Falls back to the raw ID if the user cannot be resolved.
    /// </summary>
    public string ResolveUserName(ulong userId)
    {
        // Entries written before the portal claim fix carry no user; show that plainly
        // rather than a bare "0".
        if (userId == 0)
            return "Unknown";

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

        ulong? userIdFilter = null;
        if (!string.IsNullOrWhiteSpace(UserFilter))
        {
            if (TryParseUserId(UserFilter, out var parsedUserId))
            {
                userIdFilter = parsedUserId;
            }
            else
            {
                UserFilterError = "Enter a Discord user ID: digits only, like 123456789012345678. You can paste a mention too.";
                ModelState.AddModelError(nameof(UserFilter), UserFilterError);
            }
        }

        if (DateFrom.HasValue && DateTo.HasValue && DateFrom.Value.Date > DateTo.Value.Date)
        {
            DateRangeError = "The end date is before the start date. Swap them or pick a later end date.";
            ModelState.AddModelError(nameof(DateTo), DateRangeError);
        }

        if (!FiltersValid)
        {
            // Show nothing, not everything: an unreadable filter is not "no filter"
            PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "audio", "Audio Log",
                $"Audio playback history for {guild.Name}");
            return Page();
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

    /// <summary>
    /// Reads a Discord user ID from filter text: plain digits, or a pasted mention (&lt;@123&gt;).
    /// </summary>
    internal static bool TryParseUserId(string? text, out ulong userId)
    {
        userId = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("<@", StringComparison.Ordinal) && trimmed.EndsWith('>'))
        {
            trimmed = trimmed[2..^1].TrimStart('!');
        }

        return trimmed.All(char.IsAsciiDigit) && ulong.TryParse(trimmed, out userId) && userId != 0;
    }
}
