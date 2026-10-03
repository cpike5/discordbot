using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for editing guild settings.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class EditModel : GuildPageModelBase
{
    private readonly IGuildService _guildService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IGuildService guildService,
        IGuildAudioSettingsService audioSettingsService,
        ILogger<EditModel> logger)
    {
        _guildService = guildService;
        _audioSettingsService = audioSettingsService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (name, icon).
    /// </summary>
    public GuildEditViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Whether the audio settings were read when the form was drawn. The form posts this back: when
    /// they could not be read, the audio fields hold defaults, not the saved values, and saving
    /// them would overwrite the real settings with those defaults.
    /// </summary>
    public bool AudioSettingsLoaded { get; set; }

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        [Display(Name = "Bot Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>True when the audio fields on the form hold the server's real settings.</summary>
        public bool AudioSettingsLoaded { get; set; }

        [Display(Name = "Audio Enabled")]
        public bool AudioEnabled { get; set; } = true;

        [Display(Name = "Auto-leave Timeout")]
        [Range(0, GuildAudioSettings.MaxAutoLeaveTimeoutMinutes, ErrorMessage = "Enter a whole number of minutes from 0 to 60.")]
        public int AutoLeaveTimeoutMinutes { get; set; } = 5;

        [Display(Name = "Queue Enabled")]
        public bool QueueEnabled { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild edit page for guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        Input = new InputModel { IsActive = guild.IsActive };

        // Load audio settings (don't fail the page if this fails)
        try
        {
            var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
            Input.AudioEnabled = audioSettings.AudioEnabled;
            Input.AutoLeaveTimeoutMinutes = audioSettings.AutoLeaveTimeoutMinutes;
            Input.QueueEnabled = audioSettings.QueueEnabled;
            Input.AudioSettingsLoaded = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load audio settings for guild {GuildId}", guildId);
        }

        LoadPage(guild, dirtyOnLoad: false);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User submitting guild edit for guild {GuildId}, IsActive={IsActive}, AudioEnabled={AudioEnabled}",
            guildId, Input.IsActive, Input.AudioEnabled);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                guildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            LoadPage(guild, dirtyOnLoad: true);
            return Page();
        }

        _logger.LogInformation("Updating guild {GuildId} with IsActive={IsActive}", guildId, Input.IsActive);

        var result = await _guildService.UpdateGuildAsync(guildId, new GuildUpdateRequestDto { IsActive = Input.IsActive }, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Failed to update guild {GuildId} - guild not found", guildId);
            TempData.SetErrorToast("The server was not found. It may have been removed.");
            LoadPage(guild, dirtyOnLoad: true);
            return Page();
        }

        // Only write the audio settings when the form showed the real ones
        if (Input.AudioSettingsLoaded)
        {
            try
            {
                await _audioSettingsService.UpdateSettingsAsync(guildId, settings =>
                {
                    settings.AudioEnabled = Input.AudioEnabled;
                    settings.AutoLeaveTimeoutMinutes = Input.AutoLeaveTimeoutMinutes;
                    settings.QueueEnabled = Input.QueueEnabled;
                }, cancellationToken);

                _logger.LogInformation("Successfully updated audio settings for guild {GuildId}", guildId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to update audio settings for guild {GuildId}", guildId);
                TempData.SetErrorToast("The server settings were saved, but the audio settings could not be. Try saving again.");
                LoadPage(guild, dirtyOnLoad: true);
                return Page();
            }
        }

        _logger.LogInformation("Successfully updated guild {GuildId}", guildId);
        TempData.SetSuccessToast("Server settings saved.");

        return RedirectToPage("Details", new { guildId });
    }

    /// <summary>
    /// Everything the view needs besides <see cref="Input"/>: the display view model and the layout
    /// chrome. Shared by GET and every failed POST.
    /// </summary>
    private void LoadPage(GuildDto guild, bool dirtyOnLoad)
    {
        ViewModel = GuildEditViewModel.FromDto(guild);
        AudioSettingsLoaded = Input.AudioSettingsLoaded;
        DirtyOnLoad = dirtyOnLoad;

        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "overview",
            "Edit Settings", $"Configure bot settings for {guild.Name}");
    }

    /// <summary>True after a failed POST: the form shows input that is not saved yet.</summary>
    public bool DirtyOnLoad { get; private set; }
}
