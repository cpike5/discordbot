using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Pages.Guilds.AudioSettings;

/// <summary>
/// Page model for the Guild Audio Settings page.
/// Allows administrators to configure audio and soundboard settings for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly IGuildService _guildService;
    private readonly ISoundService _soundService;
    private readonly DiscordSocketClient _discordClient;
    private readonly SoundboardOptions _soundboardOptions;
    private readonly ISettingsService _settingsService;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildAudioSettingsService audioSettingsService,
        IGuildService guildService,
        ISoundService soundService,
        DiscordSocketClient discordClient,
        IOptions<SoundboardOptions> soundboardOptions,
        ISettingsService settingsService,
        ITtsSettingsService ttsSettingsService,
        ILogger<IndexModel> logger)
    {
        _audioSettingsService = audioSettingsService;
        _guildService = guildService;
        _soundService = soundService;
        _discordClient = discordClient;
        _soundboardOptions = soundboardOptions.Value;
        _settingsService = settingsService;
        _ttsSettingsService = ttsSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the guild ID from the route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current audio settings.
    /// </summary>
    public GuildAudioSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the current TTS settings.
    /// </summary>
    public GuildTtsSettings TtsSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the available roles in the guild.
    /// </summary>
    public List<RoleInfo> AvailableRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the sound folder path for display.
    /// </summary>
    public string SoundFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total sound count for display in the AudioTabs component.
    /// </summary>
    public int SoundCount { get; set; }

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Whether the bot could read this server's roles. When it cannot (not connected, or not in the
    /// server) the role pickers are empty and the page says why.
    /// </summary>
    public bool RolesAvailable { get; set; }

    /// <summary>
    /// The soundboard commands that can have role restrictions.
    /// </summary>
    public static readonly string[] SoundboardCommands = { "join", "leave", "play", "sounds", "stop" };

    /// <summary>
    /// The speaking styles the default-style select offers; anything else is refused on save.
    /// </summary>
    public static readonly string[] DefaultStyles = { "cheerful", "excited", "friendly", "sad", "angry", "whispering", "shouting", "newscast" };

    /// <summary>
    /// Handles GET requests for the Audio Settings page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Audio settings page accessed for guild {GuildId} by user {UserId}",
            GuildId, User.Identity?.Name);

        // Check if audio is globally disabled
        var isGloballyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
        IsAudioGloballyDisabled = !isGloballyEnabled;

        // Load guild information
        var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        GuildName = guild.Name;

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "Audio", Url = $"/Guilds/Soundboard/{guild.Id}" },
                new() { Label = "Settings", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Audio", $"Configure audio settings for {guild.Name}");

        Navigation = BuildNavigation(guild.Id, "audio");

        // Load audio settings
        Settings = await _audioSettingsService.GetSettingsAsync(GuildId, cancellationToken);

        // Load TTS settings
        TtsSettings = await _ttsSettingsService.GetOrCreateSettingsAsync(GuildId, cancellationToken);

        // Load guild roles from Discord
        var discordGuild = _discordClient.GetGuild(GuildId);
        RolesAvailable = discordGuild != null;
        if (discordGuild != null)
        {
            AvailableRoles = discordGuild.Roles
                .Where(r => !r.IsEveryone && !r.IsManaged)
                .OrderByDescending(r => r.Position)
                .Select(r => new RoleInfo
                {
                    Id = r.Id,
                    Name = r.Name,
                    Color = r.Color.ToString()
                })
                .ToList();
        }

        // Set the sound folder path for display (from configuration)
        SoundFolderPath = Path.Combine(_soundboardOptions.BasePath, GuildId.ToString());

        // Load sound count for the AudioTabs badge
        SoundCount = await _soundService.GetSoundCountAsync(GuildId, cancellationToken);

        return Page();
    }

    /// <summary>
    /// Handles POST requests to save every setting on the page at once. All values are checked
    /// first; a refusal names each bad field (<c>errors</c>, keyed by the control's id) and saves nothing.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAllAsync(
        [FromBody] SaveAllDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving audio settings for guild {GuildId}", GuildId);

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new JsonResult(new
            {
                success = false,
                message = errors.Count == 1 ? errors.Values.First() : "Some settings are not valid. Fix the fields marked below and save again.",
                errors
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }

        var audioSaved = false;
        try
        {
            await _audioSettingsService.UpdateSettingsAsync(GuildId, settings =>
            {
                settings.AudioEnabled = request.AudioEnabled;
                settings.AutoLeaveTimeoutMinutes = request.AutoLeaveTimeoutMinutes!.Value;
                settings.QueueEnabled = request.QueueEnabled;
                settings.EnableMemberPortal = request.EnableMemberPortal;
                settings.SilentPlayback = request.SilentPlayback;
                settings.MaxDurationSeconds = request.MaxDurationSeconds!.Value;
                settings.MaxFileSizeBytes = request.MaxFileSizeMB!.Value * 1024L * 1024L;
                settings.MaxSoundsPerGuild = request.MaxSoundsPerGuild!.Value;
                ApplyCommandRoles(settings, request.CommandRoles);
            }, cancellationToken);
            audioSaved = true;

            var tts = await _ttsSettingsService.GetOrCreateSettingsAsync(GuildId, cancellationToken);
            tts.SsmlEnabled = request.SsmlEnabled;
            tts.StrictSsmlValidation = request.StrictSsmlValidation;
            tts.MaxSsmlComplexity = request.MaxSsmlComplexity!.Value;
            tts.DefaultStyle = string.IsNullOrWhiteSpace(request.DefaultStyle) ? null : request.DefaultStyle;
            tts.UpdatedAt = DateTime.UtcNow;
            await _ttsSettingsService.UpdateSettingsAsync(tts, cancellationToken);

            _logger.LogInformation("Audio settings saved for guild {GuildId}", GuildId);
            return new JsonResult(new { success = true, message = "Audio settings saved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio settings for guild {GuildId} (audio part saved: {AudioSaved})", GuildId, audioSaved);
            return new JsonResult(new
            {
                success = false,
                message = audioSaved
                    ? "The audio settings were saved, but the text-to-speech settings were not. Save again to retry."
                    : "The settings could not be saved. Nothing was changed. Try again."
            })
            { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }

    /// <summary>
    /// Checks a save request. Returns the problems keyed by control id; empty means it can be saved.
    /// </summary>
    internal static Dictionary<string, string> Validate(SaveAllDto? request)
    {
        var errors = new Dictionary<string, string>();
        if (request == null)
        {
            errors["form"] = "The settings could not be read. Reload the page and try again.";
            return errors;
        }

        void Range(int? value, string id, int min, int max, string what, string unit)
        {
            if (value == null)
            {
                errors[id] = $"Enter {what} as a whole number.";
            }
            else if (value < min || value > max)
            {
                var suffix = unit.Length == 0 ? string.Empty : " " + unit;
                errors[id] = $"{char.ToUpperInvariant(what[0])}{what[1..]} must be from {min} to {max}{suffix}.";
            }
        }

        Range(request.AutoLeaveTimeoutMinutes, "autoLeaveTimeout", 0, GuildAudioSettings.MaxAutoLeaveTimeoutMinutes, "the auto-leave timeout", "minutes");
        Range(request.MaxDurationSeconds, "maxDuration", 1, 300, "the maximum duration", "seconds");
        Range(request.MaxFileSizeMB, "maxFileSize", 1, 50, "the maximum file size", "MB");
        Range(request.MaxSoundsPerGuild, "maxSounds", 1, 500, "the maximum number of sounds", "sounds");
        Range(request.MaxSsmlComplexity, "maxSsmlComplexity", 10, 200, "the SSML complexity limit", "");

        if (!string.IsNullOrWhiteSpace(request.DefaultStyle)
            && !DefaultStyles.Contains(request.DefaultStyle, StringComparer.OrdinalIgnoreCase))
        {
            errors["defaultStyle"] = "Choose one of the listed styles.";
        }

        foreach (var (command, roleIds) in request.CommandRoles ?? new Dictionary<string, List<ulong>>())
        {
            if (!SoundboardCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            {
                errors["form"] = "A command in the permissions is not recognised. Reload the page and try again.";
            }
            else if (roleIds == null)
            {
                // {"commandRoles": {"play": null}}: a missing list is a bad request, not a crash
                errors["form"] = "The permissions for a command could not be read. Reload the page and try again.";
            }
        }

        return errors;
    }

    private static void ApplyCommandRoles(GuildAudioSettings settings, Dictionary<string, List<ulong>>? commandRoles)
    {
        if (commandRoles == null)
        {
            return;
        }

        foreach (var (command, roleIds) in commandRoles)
        {
            var name = SoundboardCommands.First(c => c.Equals(command, StringComparison.OrdinalIgnoreCase));
            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var ids = roleIds.Distinct().ToList();

            if (restriction != null)
            {
                restriction.AllowedRoleIds = ids;
            }
            else if (ids.Count > 0)
            {
                settings.CommandRoleRestrictions.Add(new CommandRoleRestriction
                {
                    GuildId = settings.GuildId,
                    CommandName = name,
                    AllowedRoleIds = ids
                });
            }
        }
    }

    /// <summary>
    /// Handles POST requests to reset general settings, limits and command permissions to their
    /// defaults. The reply carries the new values so the page can show them without reloading.
    /// TTS settings are left alone.
    /// </summary>
    public async Task<IActionResult> OnPostResetToDefaultsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Resetting audio settings to defaults for guild {GuildId}", GuildId);

        try
        {
            await _audioSettingsService.UpdateSettingsAsync(GuildId, settings =>
            {
                settings.AudioEnabled = true;
                settings.AutoLeaveTimeoutMinutes = 5;
                settings.QueueEnabled = true;
                settings.EnableMemberPortal = false;
                settings.SilentPlayback = false;
                settings.MaxDurationSeconds = 30;
                settings.MaxFileSizeBytes = 5_242_880; // 5 MB
                settings.MaxSoundsPerGuild = 50;
                settings.CommandRoleRestrictions.Clear();
            }, cancellationToken);

            _logger.LogInformation("Audio settings reset to defaults for guild {GuildId}", GuildId);
            return new JsonResult(new
            {
                success = true,
                message = "General settings, limits and command permissions are back to their defaults.",
                settings = new
                {
                    audioEnabled = true,
                    autoLeaveTimeoutMinutes = 5,
                    queueEnabled = true,
                    enableMemberPortal = false,
                    silentPlayback = false,
                    maxDurationSeconds = 30,
                    maxFileSizeMB = 5,
                    maxSoundsPerGuild = 50
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset audio settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "The settings could not be reset. Try again." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Everything the Save button sends. The numbers are nullable so an empty or garbled field
    /// arrives as a refusal, never as a quiet 0.
    /// </summary>
    public class SaveAllDto
    {
        public bool AudioEnabled { get; set; }
        public int? AutoLeaveTimeoutMinutes { get; set; }
        public bool QueueEnabled { get; set; }
        public bool EnableMemberPortal { get; set; }
        public bool SilentPlayback { get; set; }
        public int? MaxDurationSeconds { get; set; }
        public int? MaxFileSizeMB { get; set; }
        public int? MaxSoundsPerGuild { get; set; }
        public bool SsmlEnabled { get; set; }
        public bool StrictSsmlValidation { get; set; }
        public int? MaxSsmlComplexity { get; set; }
        public string? DefaultStyle { get; set; }

        /// <summary>The allowed role ids per command; an empty list means everyone.</summary>
        public Dictionary<string, List<ulong>>? CommandRoles { get; set; }
    }

    /// <summary>
    /// Represents a Discord role for display.
    /// </summary>
    public class RoleInfo
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
}
