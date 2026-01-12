using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Portal.TTS;

/// <summary>
/// Page model for the TTS (Text-to-Speech) Guild Member Portal.
/// Shows a landing page for unauthenticated users, or the full TTS interface
/// for authenticated guild members.
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly ITtsService _ttsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ITtsService ttsService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _guildService = guildService;
        _discordClient = discordClient;
        _audioService = audioService;
        _ttsService = ttsService;
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
    /// Gets the list of available TTS voices grouped by locale.
    /// </summary>
    public List<TtsVoiceInfo> AvailableVoices { get; set; } = new();

    /// <summary>
    /// Gets the maximum message length allowed for TTS.
    /// </summary>
    public int MaxMessageLength { get; set; } = 200;

    /// <summary>
    /// Gets whether the user is authenticated with Discord OAuth.
    /// When false, display the landing page instead of the TTS interface.
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
    /// Handles GET requests to display the TTS Portal page.
    /// Shows a landing page for unauthenticated users.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserId} accessing TTS Portal for guild {GuildId}",
            User.Identity?.Name ?? "anonymous", guildId);

        try
        {
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
                // Still populate available voices for potential future use
                PopulateAvailableVoices();
                return Page();
            }

            // User is authenticated - check guild membership
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.DiscordUserId.HasValue)
            {
                _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
                IsAuthenticated = false; // Treat as unauthenticated for UI purposes
                PopulateAvailableVoices();
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

            // User is authenticated and authorized - load full TTS interface
            IsAuthorized = true;

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
            VoiceChannels = voiceChannels;
            CurrentChannelId = _audioService.GetConnectedChannelId(guildId);
            IsConnected = _audioService.IsConnected(guildId);
            PopulateAvailableVoices();

            _logger.LogDebug("Loaded TTS Portal for guild {GuildId}", guildId);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load TTS Portal for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Populates the list of available TTS voices.
    /// This would typically come from Azure Cognitive Services or similar TTS provider.
    /// For now, providing a curated list of common voices.
    /// </summary>
    private void PopulateAvailableVoices()
    {
        // TODO: Replace with actual voice list from Azure Cognitive Services or TTS service
        // This is a placeholder with common English voices
        AvailableVoices = new List<TtsVoiceInfo>
        {
            // English (US)
            new() { Name = "en-US-AvaNeural", DisplayName = "Ava (Female)", Locale = "English (US)" },
            new() { Name = "en-US-AmberNeural", DisplayName = "Amber (Female)", Locale = "English (US)" },
            new() { Name = "en-US-AriaNeural", DisplayName = "Aria (Female)", Locale = "English (US)" },
            new() { Name = "en-US-AshleyNeural", DisplayName = "Ashley (Female)", Locale = "English (US)" },
            new() { Name = "en-US-CoraNeural", DisplayName = "Cora (Female)", Locale = "English (US)" },
            new() { Name = "en-US-ElizabethNeural", DisplayName = "Elizabeth (Female)", Locale = "English (US)" },
            new() { Name = "en-US-MichelleNeural", DisplayName = "Michelle (Female)", Locale = "English (US)" },
            new() { Name = "en-US-MonicaNeural", DisplayName = "Monica (Female)", Locale = "English (US)" },
            new() { Name = "en-US-AmandaNeural", DisplayName = "Amanda (Female)", Locale = "English (US)" },
            new() { Name = "en-US-BrandonNeural", DisplayName = "Brandon (Male)", Locale = "English (US)" },
            new() { Name = "en-US-ChristopherNeural", DisplayName = "Christopher (Male)", Locale = "English (US)" },
            new() { Name = "en-US-DavidNeural", DisplayName = "David (Male)", Locale = "English (US)" },
            new() { Name = "en-US-EricNeural", DisplayName = "Eric (Male)", Locale = "English (US)" },
            new() { Name = "en-US-GuyNeural", DisplayName = "Guy (Male)", Locale = "English (US)" },
            new() { Name = "en-US-JacobNeural", DisplayName = "Jacob (Male)", Locale = "English (US)" },
            new() { Name = "en-US-JasonNeural", DisplayName = "Jason (Male)", Locale = "English (US)" },
            new() { Name = "en-US-JennyNeural", DisplayName = "Jenny (Female, Friendly)", Locale = "English (US)" },
            new() { Name = "en-US-JesseNeural", DisplayName = "Jesse (Male)", Locale = "English (US)" },
            new() { Name = "en-US-JordanNeural", DisplayName = "Jordan (Female)", Locale = "English (US)" },
            new() { Name = "en-US-LouisaNeural", DisplayName = "Louisa (Female)", Locale = "English (US)" },
            new() { Name = "en-US-MichaelNeural", DisplayName = "Michael (Male)", Locale = "English (US)" },
            new() { Name = "en-US-RogerNeural", DisplayName = "Roger (Male)", Locale = "English (US)" },
            new() { Name = "en-US-SarahNeural", DisplayName = "Sarah (Female)", Locale = "English (US)" },
            new() { Name = "en-US-ThomasNeural", DisplayName = "Thomas (Male)", Locale = "English (US)" },

            // English (UK)
            new() { Name = "en-GB-AbbiNeural", DisplayName = "Abbi (Female)", Locale = "English (UK)" },
            new() { Name = "en-GB-BellaNeural", DisplayName = "Bella (Female)", Locale = "English (UK)" },
            new() { Name = "en-GB-ElliotNeural", DisplayName = "Elliot (Male)", Locale = "English (UK)" },
            new() { Name = "en-GB-EthanNeural", DisplayName = "Ethan (Male)", Locale = "English (UK)" },
            new() { Name = "en-GB-LibbyNeural", DisplayName = "Libby (Female)", Locale = "English (UK)" },
            new() { Name = "en-GB-MaisieNeural", DisplayName = "Maisie (Female, Child)", Locale = "English (UK)" },
            new() { Name = "en-GB-OliverNeural", DisplayName = "Oliver (Male, Child)", Locale = "English (UK)" },
            new() { Name = "en-GB-OliverNeural", DisplayName = "Oliver (Male)", Locale = "English (UK)" },
            new() { Name = "en-GB-RyanNeural", DisplayName = "Ryan (Male)", Locale = "English (UK)" },
            new() { Name = "en-GB-SoniaNeural", DisplayName = "Sonia (Female)", Locale = "English (UK)" },
            new() { Name = "en-GB-ThomasNeural", DisplayName = "Thomas (Male)", Locale = "English (UK)" },

            // Spanish
            new() { Name = "es-ES-AlvaroNeural", DisplayName = "Alvaro (Male)", Locale = "Spanish (Spain)" },
            new() { Name = "es-ES-ElviraNeural", DisplayName = "Elvira (Female)", Locale = "Spanish (Spain)" },
            new() { Name = "es-MX-DaliaNeural", DisplayName = "Dalia (Female)", Locale = "Spanish (Mexico)" },
            new() { Name = "es-MX-JorgeNeural", DisplayName = "Jorge (Male)", Locale = "Spanish (Mexico)" },

            // French
            new() { Name = "fr-FR-CelesteNeural", DisplayName = "Celeste (Female)", Locale = "French" },
            new() { Name = "fr-FR-CoralieNeural", DisplayName = "Coralie (Female)", Locale = "French" },
            new() { Name = "fr-FR-DeniseNeural", DisplayName = "Denise (Female)", Locale = "French" },
            new() { Name = "fr-FR-EloiseNeural", DisplayName = "Eloise (Female, Child)", Locale = "French" },
            new() { Name = "fr-FR-HenriNeural", DisplayName = "Henri (Male)", Locale = "French" },
            new() { Name = "fr-FR-JacquelineNeural", DisplayName = "Jacqueline (Female)", Locale = "French" },
            new() { Name = "fr-FR-JeromeNeural", DisplayName = "Jerome (Male)", Locale = "French" },
            new() { Name = "fr-FR-JosephineNeural", DisplayName = "Josephine (Female)", Locale = "French" },
            new() { Name = "fr-FR-MauriceNeural", DisplayName = "Maurice (Male)", Locale = "French" },
            new() { Name = "fr-FR-YvetteNeural", DisplayName = "Yvette (Female)", Locale = "French" },

            // German
            new() { Name = "de-DE-AmalaNeural", DisplayName = "Amala (Female)", Locale = "German" },
            new() { Name = "de-DE-BerndNeural", DisplayName = "Bernd (Male)", Locale = "German" },
            new() { Name = "de-DE-CoraNeural", DisplayName = "Cora (Female)", Locale = "German" },
            new() { Name = "de-DE-EddyNeural", DisplayName = "Eddy (Male)", Locale = "German" },
            new() { Name = "de-DE-GisberNeural", DisplayName = "Gisber (Male)", Locale = "German" },
            new() { Name = "de-DE-KasperNeural", DisplayName = "Kasper (Male)", Locale = "German" },
            new() { Name = "de-DE-KerstinNeural", DisplayName = "Kerstin (Female)", Locale = "German" },

            // Japanese
            new() { Name = "ja-JP-AzukaNeural", DisplayName = "Azuka (Female)", Locale = "Japanese" },
            new() { Name = "ja-JP-DaisukeNeural", DisplayName = "Daisuke (Male)", Locale = "Japanese" },
            new() { Name = "ja-JP-KeitaNeural", DisplayName = "Keita (Male)", Locale = "Japanese" },
            new() { Name = "ja-JP-MayuNeural", DisplayName = "Mayu (Female)", Locale = "Japanese" },
            new() { Name = "ja-JP-NaokiNeural", DisplayName = "Naoki (Male)", Locale = "Japanese" },
            new() { Name = "ja-JP-NatsukiNeural", DisplayName = "Natsuki (Female)", Locale = "Japanese" },
            new() { Name = "ja-JP-ShioriNeural", DisplayName = "Shiori (Female)", Locale = "Japanese" },

            // Chinese (Mandarin)
            new() { Name = "zh-CN-XiaoxiaoNeural", DisplayName = "Xiaoxiao (Female)", Locale = "Chinese (Mandarin)" },
            new() { Name = "zh-CN-XiaoyuNeural", DisplayName = "Xiaoyu (Female)", Locale = "Chinese (Mandarin)" },
            new() { Name = "zh-CN-XiaomoNeural", DisplayName = "Xiaomo (Female)", Locale = "Chinese (Mandarin)" },
            new() { Name = "zh-CN-YunyangNeural", DisplayName = "Yunyang (Male)", Locale = "Chinese (Mandarin)" },
            new() { Name = "zh-CN-YunxiaNeural", DisplayName = "Yunxia (Male)", Locale = "Chinese (Mandarin)" },
        };
    }
}

/// <summary>
/// DTO for voice channel information.
/// </summary>
public class VoiceChannelInfo
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID of the voice channel.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the voice channel.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of members currently in the channel.
    /// </summary>
    public int MemberCount { get; set; }
}

/// <summary>
/// DTO for TTS voice information.
/// </summary>
public class TtsVoiceInfo
{
    /// <summary>
    /// Gets or sets the voice identifier (e.g., "en-US-AriaNeural").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name (e.g., "Aria (Female)").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the locale group (e.g., "English (US)").
    /// </summary>
    public string Locale { get; set; } = string.Empty;
}
