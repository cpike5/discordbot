using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Portal;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Portal.Soundboard;

/// <summary>
/// Page model for the Soundboard Guild Member Portal.
/// Displays sounds and voice channel controls for authenticated guild members.
/// </summary>
[Authorize(Policy = "PortalGuildMember")]
public class IndexModel : PageModel
{
    private readonly ISoundService _soundService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISoundService soundService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ILogger<IndexModel> logger)
    {
        _soundService = soundService;
        _audioSettingsRepository = audioSettingsRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _audioService = audioService;
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
    /// Handles GET requests to display the Soundboard Portal page.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserId} accessing Soundboard Portal for guild {GuildId}",
            User.Identity?.Name, guildId);

        try
        {
            // Get guild info from service
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Get all sounds for this guild
            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

            // Get audio settings (creates defaults if not found)
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
            var socketGuild = _discordClient.GetGuild(guildId);
            if (socketGuild != null)
            {
                foreach (var channel in socketGuild.VoiceChannels.Where(c => c != null).OrderBy(c => c.Position))
                {
                    voiceChannels.Add(new VoiceChannelInfo
                    {
                        Id = channel.Id,
                        Name = channel.Name,
                        MemberCount = channel.ConnectedUsers.Count
                    });
                }
            }

            // Set all view properties
            GuildId = guildId;
            GuildName = guild.Name;
            GuildIconUrl = guild.IconUrl;
            IsOnline = _discordClient.ConnectionState == Discord.ConnectionState.Connected;
            Sounds = soundViewModels;
            VoiceChannels = voiceChannels;
            CurrentChannelId = _audioService.GetConnectedChannelId(guildId);
            IsConnected = _audioService.IsConnected(guildId);
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
