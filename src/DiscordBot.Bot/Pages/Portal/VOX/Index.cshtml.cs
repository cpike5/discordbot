using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Portal.VOX;

/// <summary>
/// Page model for the VOX Guild Member Portal.
/// Shows a landing page for unauthenticated users, or the full VOX interface
/// for authenticated guild members.
/// </summary>
[AllowAnonymous]
public class IndexModel : PortalPageModelBase
{
    private readonly IVoxClipLibrary _voxClipLibrary;
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IVoxClipLibrary voxClipLibrary,
        IAudioService audioService,
        IPlaybackService playbackService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
        : base(guildService, discordClient, userManager, logger)
    {
        _voxClipLibrary = voxClipLibrary;
        _audioService = audioService;
        _playbackService = playbackService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the clip count for the VOX group.
    /// </summary>
    public int VoxClipCount { get; set; }

    /// <summary>
    /// Gets the clip count for the FVOX group.
    /// </summary>
    public int FvoxClipCount { get; set; }

    /// <summary>
    /// Gets the clip count for the HGrunt group.
    /// </summary>
    public int HgruntClipCount { get; set; }

    /// <summary>
    /// Gets the voice channel panel view model.
    /// </summary>
    public VoiceChannelPanelViewModel? VoicePanel { get; set; }

    /// <summary>
    /// Gets or sets the active clip group tab.
    /// Used by JavaScript for maintaining state during tab switches when AJAX-based tab content loading is implemented.
    /// </summary>
    public string ActiveGroup { get; set; } = "vox";

    /// <summary>
    /// Gets the now playing message text.
    /// </summary>
    public string? NowPlayingMessage { get; set; }

    /// <summary>
    /// Gets the navigation tabs view model for group selection.
    /// </summary>
    public NavTabsViewModel? GroupTabs { get; set; }

    /// <summary>
    /// Handles GET requests to display the VOX Portal page.
    /// Shows a landing page for unauthenticated users.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform common portal authorization check
            var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "VOX", cancellationToken);

            // Handle auth failures
            var actionResult = GetAuthResultAction(authResult);
            if (actionResult != null)
            {
                return actionResult;
            }

            // For landing page, we're done
            if (authResult == PortalAuthResult.ShowLandingPage)
            {
                return Page();
            }

            // User is authorized - load full VOX portal data
            _logger.LogDebug("Loading VOX Portal data for guild {GuildId}", guildId);

            // Get clip counts per group
            VoxClipCount = _voxClipLibrary.GetClipCount(VoxClipGroup.Vox);
            FvoxClipCount = _voxClipLibrary.GetClipCount(VoxClipGroup.Fvox);
            HgruntClipCount = _voxClipLibrary.GetClipCount(VoxClipGroup.Hgrunt);

            // Build voice channel panel data
            var connectedChannelId = _audioService.GetConnectedChannelId(guildId);
            var isConnected = _audioService.IsConnected(guildId);
            string? connectedChannelName = null;
            int? channelMemberCount = null;

            if (isConnected && connectedChannelId.HasValue)
            {
                var connectedChannel = context!.SocketGuild.GetVoiceChannel(connectedChannelId.Value);
                if (connectedChannel != null)
                {
                    connectedChannelName = connectedChannel.Name;
                    channelMemberCount = connectedChannel.ConnectedUsers.Count(u => !u.IsBot);
                }
            }

            // Get now playing info if available
            if (_playbackService.IsPlaying(guildId))
            {
                NowPlayingMessage = "VOX Message"; // Will be enhanced in future issues
            }

            VoicePanel = new VoiceChannelPanelViewModel
            {
                GuildId = guildId,
                IsCompact = true,
                ShowNowPlaying = true,
                ShowProgress = false,
                IsConnected = isConnected,
                ConnectedChannelId = connectedChannelId,
                ConnectedChannelName = connectedChannelName,
                ChannelMemberCount = channelMemberCount,
                AvailableChannels = BuildVoiceChannelList(context!.SocketGuild)
                    .Select(c => new DiscordBot.Bot.ViewModels.Components.VoiceChannelInfo
                    {
                        Id = c.Id,
                        Name = c.Name,
                        MemberCount = c.MemberCount
                    }).ToList(),
                NowPlaying = string.IsNullOrEmpty(NowPlayingMessage)
                    ? null
                    : new NowPlayingInfo { Name = NowPlayingMessage },
                Queue = []
            };

            // Build group tabs
            GroupTabs = new NavTabsViewModel
            {
                ContainerId = "voxGroupTabs",
                StyleVariant = NavTabStyle.Pills,
                NavigationMode = NavMode.InPage,
                PersistenceMode = NavPersistence.Hash,
                Tabs = new List<NavTabItem>
                {
                    new() { Id = "vox", Label = "VOX" },
                    new() { Id = "fvox", Label = "FVOX" },
                    new() { Id = "hgrunt", Label = "HGrunt" }
                },
                ActiveTabId = "vox",
                AriaLabel = "VOX clip groups"
            };

            _logger.LogDebug("Loaded VOX Portal for guild {GuildId}: VOX={VoxCount}, FVOX={FvoxCount}, HGrunt={HgruntCount}",
                guildId, VoxClipCount, FvoxClipCount, HgruntClipCount);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load VOX Portal for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }
}
