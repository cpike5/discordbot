using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Portal.Soundboard;

/// <summary>
/// Page model for the Soundboard Guild Member Portal.
/// Shows a landing page for unauthenticated users, or the full soundboard
/// for authenticated guild members.
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly ISoundService _soundService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISoundService soundService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _soundService = soundService;
        _audioSettingsRepository = audioSettingsRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _audioService = audioService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets whether the bot is online (connected to Discord gateway).
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets the list of sounds available in this guild.
    /// </summary>
    public IReadOnlyList<PortalSoundViewModel> Sounds { get; set; } = Array.Empty<PortalSoundViewModel>();

    /// <summary>
    /// Gets the list of voice channels in the guild.
    /// </summary>
    public List<VoiceChannelInfo> VoiceChannels { get; set; } = new();

    /// <summary>
    /// Gets the ID of the voice channel the bot is currently connected to.
    /// </summary>
    public ulong? CurrentChannelId { get; set; }

    /// <summary>
    /// Gets whether the bot is connected to a voice channel in this guild.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets the number of members in the currently connected voice channel (excluding bots).
    /// </summary>
    public int? CurrentChannelMemberCount { get; set; }

    /// <summary>
    /// Gets the maximum number of sounds allowed per guild.
    /// </summary>
    public int MaxSounds { get; set; }

    /// <summary>
    /// Gets the current sound count for this guild.
    /// </summary>
    public int CurrentSoundCount { get; set; }

    /// <summary>
    /// Gets the supported audio formats.
    /// </summary>
    public string SupportedFormats { get; set; } = "MP3, WAV, OGG";

    /// <summary>
    /// Gets the maximum file size in MB.
    /// </summary>
    public int MaxFileSizeMB { get; set; }

    /// <summary>
    /// Gets the maximum duration in seconds.
    /// </summary>
    public int MaxDurationSeconds { get; set; }

    /// <summary>
    /// Gets whether the user is authenticated with Discord OAuth.
    /// When false, display the landing page instead of the soundboard.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets whether the authenticated user is authorized to view this portal.
    /// True when user is a member of the guild.
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets the login URL with return URL for Discord OAuth.
    /// </summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>
    /// Handles GET requests to display the Soundboard Portal page.
    /// Shows a landing page for unauthenticated users.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserId} accessing Soundboard Portal for guild {GuildId}",
            User.Identity?.Name ?? "anonymous", guildId);

        try
        {
            // Check if portal is enabled for this guild first (before auth check)
            var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId);

            // TODO: Issue #947 will add EnableMemberPortal property
            // For now, we check AudioEnabled as a proxy
            if (audioSettings == null || !audioSettings.AudioEnabled)
            {
                _logger.LogDebug("Portal not enabled for guild {GuildId}", guildId);
                return NotFound();
            }

            // Get guild info - return 404 if not found (don't reveal guild doesn't exist)
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Check if Discord guild is available
            var socketGuild = _discordClient.GetGuild(guildId);
            if (socketGuild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
                return NotFound();
            }

            // Set basic guild info for landing page (needed for both auth states)
            GuildId = guildId;
            GuildName = guild.Name;
            GuildIconUrl = guild.IconUrl;
            IsOnline = _discordClient.ConnectionState == Discord.ConnectionState.Connected;

            // Build login URL with return URL
            var returnUrl = HttpContext.Request.Path.ToString();
            LoginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

            // Check authentication state
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false;

            if (!IsAuthenticated)
            {
                _logger.LogDebug("Unauthenticated user viewing landing page for guild {GuildId}", guildId);
                return Page();
            }

            // User is authenticated - check guild membership
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.DiscordUserId.HasValue)
            {
                _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
                IsAuthenticated = false; // Treat as unauthenticated for UI purposes
                return Page();
            }

            // Check if user is a member of the guild
            var guildUser = socketGuild.GetUser(user.DiscordUserId.Value);
            if (guildUser == null)
            {
                _logger.LogDebug("User {DiscordUserId} is not a member of guild {GuildId}",
                    user.DiscordUserId.Value, guildId);
                // Return 403 - authenticated but not authorized
                return Forbid();
            }

            // User is authenticated and authorized - load full soundboard
            IsAuthorized = true;

            // Get all sounds for this guild
            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

            // Get audio settings for limits
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

            // Map sounds to portal view models
            var soundViewModels = sounds
                .Select(s => new PortalSoundViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    PlayCount = s.PlayCount
                })
                .ToList();

            // Build voice channels list
            var voiceChannels = new List<VoiceChannelInfo>();
            foreach (var channel in socketGuild.VoiceChannels.Where(c => c != null).OrderBy(c => c.Position))
            {
                voiceChannels.Add(new VoiceChannelInfo
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    MemberCount = channel.ConnectedUsers.Count
                });
            }

            // Set remaining view properties
            Sounds = soundViewModels;
            VoiceChannels = voiceChannels;
            CurrentChannelId = _audioService.GetConnectedChannelId(guildId);
            IsConnected = _audioService.IsConnected(guildId);

            // Get member count if connected
            if (IsConnected && CurrentChannelId.HasValue)
            {
                var connectedChannel = socketGuild.GetVoiceChannel(CurrentChannelId.Value);
                if (connectedChannel != null)
                {
                    CurrentChannelMemberCount = connectedChannel.ConnectedUsers.Count(u => !u.IsBot);
                }
            }
            MaxSounds = settings.MaxSoundsPerGuild;
            CurrentSoundCount = sounds.Count;
            SupportedFormats = "MP3, WAV, OGG";
            MaxFileSizeMB = (int)(settings.MaxFileSizeBytes / (1024.0 * 1024.0));
            MaxDurationSeconds = settings.MaxDurationSeconds;

            _logger.LogDebug("Loaded {Count} sounds for guild {GuildId} in portal view",
                sounds.Count, guildId);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Soundboard Portal for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }
}
