using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Pages.Guilds.AudioSettings;

/// <summary>
/// Page model for the Guild Audio Settings page.
/// Allows administrators to configure audio and soundboard settings for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly IGuildService _guildService;
    private readonly ISoundService _soundService;
    private readonly DiscordSocketClient _discordClient;
    private readonly SoundboardOptions _soundboardOptions;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildAudioSettingsService audioSettingsService,
        IGuildService guildService,
        ISoundService soundService,
        DiscordSocketClient discordClient,
        IOptions<SoundboardOptions> soundboardOptions,
        ISettingsService settingsService,
        ILogger<IndexModel> logger)
    {
        _audioSettingsService = audioSettingsService;
        _guildService = guildService;
        _soundService = soundService;
        _discordClient = discordClient;
        _soundboardOptions = soundboardOptions.Value;
        _settingsService = settingsService;
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
    /// The soundboard commands that can have role restrictions.
    /// </summary>
    public static readonly string[] SoundboardCommands = { "join", "leave", "play", "sounds", "stop" };

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

        // Load audio settings
        Settings = await _audioSettingsService.GetSettingsAsync(GuildId, cancellationToken);

        // Load guild roles from Discord
        var discordGuild = _discordClient.GetGuild(GuildId);
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
    /// Handles POST requests to save general settings.
    /// </summary>
    public async Task<IActionResult> OnPostSaveGeneralAsync(
        [FromBody] GeneralSettingsDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving general audio settings for guild {GuildId}", GuildId);

        try
        {
            await _audioSettingsService.UpdateSettingsAsync(GuildId, settings =>
            {
                settings.AudioEnabled = request.AudioEnabled;
                settings.AutoLeaveTimeoutMinutes = request.AutoLeaveTimeoutMinutes;
                settings.QueueEnabled = request.QueueEnabled;
                settings.EnableMemberPortal = request.EnableMemberPortal;
            }, cancellationToken);

            _logger.LogInformation("General audio settings saved for guild {GuildId}", GuildId);
            return new JsonResult(new { success = true, message = "General settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save general audio settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to save limit settings.
    /// </summary>
    public async Task<IActionResult> OnPostSaveLimitsAsync(
        [FromBody] LimitSettingsDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving limit settings for guild {GuildId}", GuildId);

        try
        {
            // Validate inputs
            if (request.MaxDurationSeconds < 1 || request.MaxDurationSeconds > 300)
            {
                return new JsonResult(new { success = false, message = "Max duration must be between 1 and 300 seconds." }) { StatusCode = 400 };
            }
            if (request.MaxFileSizeMB < 1 || request.MaxFileSizeMB > 50)
            {
                return new JsonResult(new { success = false, message = "Max file size must be between 1 and 50 MB." }) { StatusCode = 400 };
            }
            if (request.MaxSoundsPerGuild < 1 || request.MaxSoundsPerGuild > 500)
            {
                return new JsonResult(new { success = false, message = "Max sounds must be between 1 and 500." }) { StatusCode = 400 };
            }

            await _audioSettingsService.UpdateSettingsAsync(GuildId, settings =>
            {
                settings.MaxDurationSeconds = request.MaxDurationSeconds;
                settings.MaxFileSizeBytes = request.MaxFileSizeMB * 1024 * 1024; // Convert MB to bytes
                settings.MaxSoundsPerGuild = request.MaxSoundsPerGuild;
            }, cancellationToken);

            _logger.LogInformation("Limit settings saved for guild {GuildId}", GuildId);
            return new JsonResult(new { success = true, message = "Limit settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save limit settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to update command role restrictions.
    /// </summary>
    public async Task<IActionResult> OnPostUpdateCommandRolesAsync(
        [FromBody] CommandRolesDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating command roles for '{CommandName}' in guild {GuildId}",
            request.CommandName, GuildId);

        try
        {
            // Validate command name
            if (!SoundboardCommands.Contains(request.CommandName, StringComparer.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "Invalid command name." }) { StatusCode = 400 };
            }

            // Get current settings to update restrictions
            var settings = await _audioSettingsService.GetSettingsAsync(GuildId, cancellationToken);

            // Find existing restriction or create new one
            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(request.CommandName, StringComparison.OrdinalIgnoreCase));

            if (restriction != null)
            {
                // Clear existing roles first by removing all
                foreach (var roleId in restriction.AllowedRoleIds.ToList())
                {
                    await _audioSettingsService.RemoveCommandRestrictionAsync(GuildId, request.CommandName, roleId, cancellationToken);
                }
            }

            // Add new roles
            foreach (var roleId in request.RoleIds)
            {
                await _audioSettingsService.AddCommandRestrictionAsync(GuildId, request.CommandName, roleId, cancellationToken);
            }

            _logger.LogInformation("Command roles updated for '{CommandName}' in guild {GuildId}: {RoleCount} roles",
                request.CommandName, GuildId, request.RoleIds.Count);

            return new JsonResult(new { success = true, message = "Command permissions updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update command roles for '{CommandName}' in guild {GuildId}",
                request.CommandName, GuildId);
            return new JsonResult(new { success = false, message = "Failed to update command permissions." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to reset all settings to defaults.
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
                settings.MaxDurationSeconds = 30;
                settings.MaxFileSizeBytes = 5_242_880; // 5 MB
                settings.MaxSoundsPerGuild = 50;
                settings.CommandRoleRestrictions.Clear();
            }, cancellationToken);

            _logger.LogInformation("Audio settings reset to defaults for guild {GuildId}", GuildId);
            return new JsonResult(new { success = true, message = "Settings reset to defaults." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset audio settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to reset settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// DTO for general settings update.
    /// </summary>
    public class GeneralSettingsDto
    {
        public bool AudioEnabled { get; set; }
        public int AutoLeaveTimeoutMinutes { get; set; }
        public bool QueueEnabled { get; set; }
        public bool EnableMemberPortal { get; set; }
    }

    /// <summary>
    /// DTO for limit settings update.
    /// </summary>
    public class LimitSettingsDto
    {
        public int MaxDurationSeconds { get; set; }
        public int MaxFileSizeMB { get; set; }
        public int MaxSoundsPerGuild { get; set; }
    }

    /// <summary>
    /// DTO for command role restrictions update.
    /// </summary>
    public class CommandRolesDto
    {
        public string CommandName { get; set; } = string.Empty;
        public List<ulong> RoleIds { get; set; } = new();
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
