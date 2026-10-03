using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Soundboard;

/// <summary>
/// Page model for the Soundboard management page.
/// Displays sounds, statistics, and settings for a guild's soundboard.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly ISoundService _soundService;
    private readonly ISoundRepository _soundRepository;
    private readonly ISoundCategoryRepository _categoryRepository;
    private readonly ISoundFileService _soundFileService;
    private readonly ISoundboardOrchestrationService _orchestrationService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly ISoundPlayLogRepository _soundPlayLogRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISoundService soundService,
        ISoundRepository soundRepository,
        ISoundCategoryRepository categoryRepository,
        ISoundFileService soundFileService,
        ISoundboardOrchestrationService orchestrationService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        ISoundPlayLogRepository soundPlayLogRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ISettingsService settingsService,
        ILogger<IndexModel> logger)
    {
        _soundService = soundService;
        _soundRepository = soundRepository;
        _categoryRepository = categoryRepository;
        _soundFileService = soundFileService;
        _orchestrationService = orchestrationService;
        _audioSettingsRepository = audioSettingsRepository;
        _soundPlayLogRepository = soundPlayLogRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _audioService = audioService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public SoundboardIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// View model for the voice channel control panel.
    /// </summary>
    public VoiceChannelPanelViewModel VoiceChannelPanel { get; set; } = null!;

    /// <summary>
    /// Current sort order for the sounds list.
    /// Valid values: name-asc, name-desc, newest, oldest.
    /// Defaults to name-asc (alphabetical A-Z).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "name-asc";

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Gets whether the member portal is enabled for this guild.
    /// </summary>
    public bool IsMemberPortalEnabled { get; set; }

    /// <summary>
    /// Handles GET requests to display the Soundboard management page.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Soundboard management for guild {GuildId}", guildId);

        try
        {
            // Check if audio is globally disabled
            var isGloballyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
            IsAudioGloballyDisabled = !isGloballyEnabled;

            // Get guild info from service
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Get all sounds for this guild
            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

            // Apply sorting
            var sortedSounds = ApplySorting(sounds);

            // Get audio settings (creates defaults if not found)
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

            // Set member portal enabled flag for UI
            IsMemberPortalEnabled = settings.EnableMemberPortal;

            // Query play statistics
            var todayUtc = DateTime.UtcNow.Date;
            var yesterdayUtc = todayUtc.AddDays(-1);

            // Get play counts since start of today and yesterday
            var playsSinceTodayStart = await _soundPlayLogRepository.GetPlayCountAsync(guildId, todayUtc, cancellationToken);
            var playsSinceYesterdayStart = await _soundPlayLogRepository.GetPlayCountAsync(guildId, yesterdayUtc, cancellationToken);

            // Calculate actual counts for today and yesterday
            var playsToday = playsSinceTodayStart;
            var playsYesterday = playsSinceYesterdayStart - playsSinceTodayStart;

            _logger.LogDebug("Retrieved {Count} sounds for guild {GuildId}, sorted by {Sort}. Plays today: {PlaysToday}, yesterday: {PlaysYesterday}",
                sounds.Count, guildId, Sort, playsToday, playsYesterday);

            // Build view model
            ViewModel = SoundboardIndexViewModel.Create(
                guildId,
                guild.Name,
                guild.IconUrl,
                sortedSounds,
                settings,
                playsToday,
                playsYesterday,
                Sort) with
            {
                Categories = await LoadCategoryOptionsAsync(guildId, cancellationToken)
            };

            // Build voice channel panel view model
            VoiceChannelPanel = BuildVoiceChannelPanelViewModel(guildId);

            // Populate guild layout ViewModels
            Breadcrumb = BuildPageBreadcrumb(guild.Id, guild.Name, "Audio");

            Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
                "Audio", $"Manage audio settings and soundboard for {guild.Name}");
            Header.Actions = IsMemberPortalEnabled ? new List<HeaderAction>
            {
                new()
                {
                    Label = "Open Member Portal",
                    Url = $"/Portal/Soundboard/{guildId}",
                    Icon = "M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14",
                    Style = HeaderActionStyle.Secondary,
                    OpenInNewTab = true
                }
            } : null;

            Navigation = BuildNavigation(guild.Id, "audio");

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Soundboard page for guild {GuildId}", guildId);
            ErrorMessage = "The soundboard could not be loaded. Try again in a moment.";
            ViewModel = new SoundboardIndexViewModel { GuildId = guildId };

            // Set fallback voice channel panel
            VoiceChannelPanel = new VoiceChannelPanelViewModel { GuildId = guildId };

            return Page();
        }
    }

    /// <summary>
    /// Handles AJAX requests for sorted sound list partial.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view with sorted sounds list; an error status when the list cannot be loaded,
    /// so the script can keep what is on screen and offer Retry.</returns>
    public async Task<IActionResult> OnGetPartialAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AJAX request for Soundboard partial view, guild {GuildId}, sort {Sort}", guildId, Sort);

        try
        {
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                return NotFound();
            }

            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);
            var sortedSounds = ApplySorting(sounds);

            // Same mapping as the full page, so the duration and size columns are filled in
            ViewModel = new SoundboardIndexViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                Sounds = sortedSounds.Select(SoundViewModel.FromEntity).ToList(),
                Categories = await LoadCategoryOptionsAsync(guildId, cancellationToken),
                CurrentSort = Sort
            };

            return Partial("_SoundsList", this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Soundboard partial for guild {GuildId}", guildId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handles POST requests to delete a sound. Answers JSON to a script request so the row can be
    /// removed in place, and redirects (with a toast) otherwise.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="soundId">The sound ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnPostDeleteAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default)
    {
        var result = await _orchestrationService.DeleteSoundAsync(guildId, soundId, cancellationToken);

        var message = result.Success
            ? (result.FileDeleted
                ? "Sound deleted."
                : "Sound deleted (its file was already missing).")
            : result.ErrorMessage ?? "Could not delete the sound.";

        if (IsScriptRequest)
        {
            if (!result.Success)
            {
                return new JsonResult(new { success = false, message }) { StatusCode = StatusCodes.Status400BadRequest };
            }

            return new JsonResult(new { success = true, message, soundId, stats = await BuildStatsAsync(guildId, cancellationToken) });
        }

        if (result.Success)
        {
            TempData.SetSuccessToast(message);
        }
        else
        {
            TempData.SetErrorToast(message);
        }

        return RedirectToPage("Index", new { guildId, sort = Sort });
    }

    /// <summary>
    /// Handles POST requests to upload a new sound file. The page script uploads one file per
    /// request (so each file has its own progress and result) and gets JSON back; a plain form
    /// post still works and redirects with a toast.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="file">The uploaded file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnPostUploadAsync(
        ulong guildId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to upload sound file for guild {GuildId}", guildId);

        IActionResult Fail(string message)
        {
            if (IsScriptRequest)
            {
                return new JsonResult(new { success = false, message }) { StatusCode = StatusCodes.Status400BadRequest };
            }

            TempData.SetErrorToast(message);
            return RedirectToPage("Index", new { guildId, sort = Sort });
        }

        if (file == null || file.Length == 0)
        {
            return Fail("Choose a file to upload.");
        }

        if (!_soundFileService.IsValidAudioFormat(file.FileName))
        {
            return Fail("That file type is not supported. Use MP3, WAV, OGG or M4A.");
        }

        var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

        if (file.Length > settings.MaxFileSizeBytes)
        {
            var maxSizeMB = settings.MaxFileSizeBytes / (1024.0 * 1024.0);
            return Fail($"That file is larger than the {maxSizeMB:F1} MB limit.");
        }

        var soundName = Path.GetFileNameWithoutExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var result = await _orchestrationService.UploadSoundAsync(
            guildId,
            file.FileName,
            soundName,
            stream,
            file.Length,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return Fail(result.ErrorMessage ?? "Could not upload the sound.");
        }

        var message = $"Uploaded '{result.Sound!.Name}'.";
        if (IsScriptRequest)
        {
            return new JsonResult(new
            {
                success = true,
                message,
                soundId = result.Sound.Id,
                stats = await BuildStatsAsync(guildId, cancellationToken)
            });
        }

        TempData.SetSuccessToast(message);
        return RedirectToPage("Index", new { guildId, sort = Sort });
    }

    /// <summary>
    /// Handles POST requests to discover sounds from the guild's folder.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostDiscoverAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to discover sounds for guild {GuildId}", guildId);

        try
        {
            // Ensure audio is enabled
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);
            if (!settings.AudioEnabled)
            {
                TempData.SetErrorToast("Audio features are not enabled for this guild.");
                return RedirectToPage("Index", new { guildId, sort = Sort });
            }

            // Discover sound files from the guild's directory
            var discoveredFiles = await _soundFileService.DiscoverSoundFilesAsync(
                guildId,
                cancellationToken);

            if (discoveredFiles.Count == 0)
            {
                TempData.SetWarningToast("No sound files found in the guild's directory.");
                return RedirectToPage("Index", new { guildId, sort = Sort });
            }

            // Get existing sounds to avoid duplicates
            var existingSounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);
            var existingFileNames = new HashSet<string>(
                existingSounds.Select(s => s.FileName),
                StringComparer.OrdinalIgnoreCase);

            var newSoundsCount = 0;

            // Create Sound entities for new files
            foreach (var fileName in discoveredFiles)
            {
                // Skip if already in database
                if (existingFileNames.Contains(fileName))
                {
                    _logger.LogDebug("Skipping existing sound file {FileName}", fileName);
                    continue;
                }

                // Get file info
                var filePath = _soundFileService.GetSoundFilePath(guildId, fileName);
                var fileInfo = new FileInfo(filePath);

                if (!fileInfo.Exists)
                {
                    _logger.LogWarning("File {FileName} no longer exists at {Path}", fileName, filePath);
                    continue;
                }

                // Extract audio duration using FFprobe
                var duration = await _soundFileService.GetAudioDurationAsync(filePath, cancellationToken);

                var sound = new Sound
                {
                    Id = Guid.NewGuid(),
                    GuildId = guildId,
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    FileName = fileName,
                    FileSizeBytes = fileInfo.Length,
                    DurationSeconds = duration,
                    UploadedAt = DateTime.UtcNow
                };

                try
                {
                    await _soundService.CreateSoundAsync(sound, cancellationToken);
                    newSoundsCount++;
                    _logger.LogDebug("Discovered and added sound {Name} (duration: {Duration}s) from file {FileName}",
                        sound.Name, duration, fileName);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                {
                    _logger.LogWarning("Sound with name {Name} already exists, skipping", sound.Name);
                }
            }

            if (newSoundsCount > 0)
            {
                _logger.LogInformation("Discovered {Count} new sounds for guild {GuildId}",
                    newSoundsCount, guildId);
                TempData.SetSuccessToast($"Discovered {newSoundsCount} new sound(s).");
            }
            else
            {
                TempData.SetInfoToast("No new sounds found. All files in the directory are already registered.");
            }

            return RedirectToPage("Index", new { guildId, sort = Sort });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering sounds for guild {GuildId}", guildId);
            TempData.SetErrorToast("An error occurred while discovering sounds. Please try again.");
            return RedirectToPage("Index", new { guildId, sort = Sort });
        }
    }

    // ---- Categories --------------------------------------------------------------------------
    // These are page handlers, not the member portal's endpoints: those answer 403 for everyone,
    // administrators included, while the guild's member portal is switched off, and an admin page
    // must not depend on a member-facing switch.

    /// <summary>Creates a category.</summary>
    public async Task<IActionResult> OnPostCreateCategoryAsync(
        ulong guildId,
        [FromBody] CategoryNameDto request,
        CancellationToken cancellationToken = default)
    {
        var name = request?.Name?.Trim() ?? string.Empty;
        var invalid = ValidateCategoryName(name);
        if (invalid != null)
        {
            return CategoryError(invalid);
        }

        var existing = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);
        if (existing.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return CategoryError($"A category named '{name}' already exists on this server.");
        }

        var category = await _categoryRepository.AddAsync(new SoundCategory
        {
            GuildId = guildId,
            Name = name,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Created sound category {CategoryId} in guild {GuildId}", category.Id, guildId);
        return new JsonResult(new { success = true, message = $"Created category '{category.Name}'.", category = new { id = category.Id, name = category.Name } });
    }

    /// <summary>Renames a category.</summary>
    public async Task<IActionResult> OnPostRenameCategoryAsync(
        ulong guildId,
        [FromBody] CategoryNameDto request,
        CancellationToken cancellationToken = default)
    {
        var name = request?.Name?.Trim() ?? string.Empty;
        var invalid = ValidateCategoryName(name);
        if (invalid != null)
        {
            return CategoryError(invalid);
        }

        var category = await _categoryRepository.GetByIdAsync(request!.Id, cancellationToken);
        if (category == null || category.GuildId != guildId)
        {
            return CategoryError("That category no longer exists. Reload the page to see the latest.", StatusCodes.Status404NotFound);
        }

        var existing = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);
        if (existing.Any(c => c.Id != category.Id && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return CategoryError($"A category named '{name}' already exists on this server.");
        }

        category.Name = name;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        return new JsonResult(new { success = true, message = $"Renamed the category to '{category.Name}'.", category = new { id = category.Id, name = category.Name } });
    }

    /// <summary>Deletes a category; its sounds become uncategorized.</summary>
    public async Task<IActionResult> OnPostDeleteCategoryAsync(
        ulong guildId,
        [FromBody] CategoryNameDto request,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(request?.Id ?? 0, cancellationToken);
        if (category == null || category.GuildId != guildId)
        {
            return CategoryError("That category no longer exists. Reload the page to see the latest.", StatusCodes.Status404NotFound);
        }

        var name = category.Name;
        await _categoryRepository.DeleteAsync(category, cancellationToken);

        _logger.LogInformation("Deleted sound category {CategoryId} in guild {GuildId}", request!.Id, guildId);
        return new JsonResult(new { success = true, message = $"Deleted the category '{name}'. Its sounds are now uncategorized." });
    }

    /// <summary>Puts a sound in a category, or takes it out of one (null).</summary>
    public async Task<IActionResult> OnPostAssignCategoryAsync(
        ulong guildId,
        [FromBody] AssignCategoryDto request,
        CancellationToken cancellationToken = default)
    {
        var sound = await _soundService.GetByIdAsync(request.SoundId, guildId, cancellationToken);
        if (sound == null)
        {
            return CategoryError("That sound no longer exists. Reload the page to see the latest.", StatusCodes.Status404NotFound);
        }

        string? categoryName = null;
        if (request.CategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId.Value, cancellationToken);
            if (category == null || category.GuildId != guildId)
            {
                return CategoryError("That category no longer exists. Reload the page to see the latest.");
            }

            categoryName = category.Name;
        }

        // The sound was read with its Category loaded and untracked; Update would copy that old
        // category's key back over the new one, so drop the navigation along with changing the key.
        sound.Category = null;
        sound.CategoryId = request.CategoryId;
        await _soundRepository.UpdateAsync(sound, cancellationToken);

        return new JsonResult(new
        {
            success = true,
            message = categoryName == null
                ? $"'{sound.Name}' is no longer in a category."
                : $"'{sound.Name}' is now in '{categoryName}'."
        });
    }

    private static string? ValidateCategoryName(string name)
    {
        if (name.Length == 0)
        {
            return "Enter a category name.";
        }

        return name.Length > 50 ? "A category name can be at most 50 characters." : null;
    }

    private static JsonResult CategoryError(string message, int status = StatusCodes.Status400BadRequest)
        => new(new { success = false, message }) { StatusCode = status };

    private async Task<List<SoundCategoryOption>> LoadCategoryOptionsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);
        return categories.Select(c => new SoundCategoryOption(c.Id, c.Name)).ToList();
    }

    /// <summary>The numbers on the stat cards, returned with an upload or delete so they update in place.</summary>
    private async Task<object> BuildStatsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);
        var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);
        var stats = SoundboardIndexViewModel.Create(guildId, string.Empty, null, sounds, settings, 0, 0).Stats;
        return new
        {
            totalSounds = stats.TotalSounds,
            storageUsedFormatted = stats.StorageUsedFormatted,
            storageLimitFormatted = stats.StorageLimitFormatted,
            storagePercentage = stats.StoragePercentage,
            topSoundName = stats.TopSoundName,
            topSoundPlays = stats.TopSoundPlays
        };
    }

    private bool IsScriptRequest => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);

    /// <summary>Body of a category create, rename or delete request.</summary>
    public class CategoryNameDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>Body of a sound's category assignment.</summary>
    public class AssignCategoryDto
    {
        public Guid SoundId { get; set; }
        public int? CategoryId { get; set; }
    }

    /// <summary>
    /// Builds the voice channel panel view model with current connection status and available channels.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>The voice channel panel view model.</returns>
    private VoiceChannelPanelViewModel BuildVoiceChannelPanelViewModel(ulong guildId)
    {
        var socketGuild = _discordClient.GetGuild(guildId);
        var isConnected = _audioService.IsConnected(guildId);
        var connectedChannelId = _audioService.GetConnectedChannelId(guildId);

        // Build available channels list
        var availableChannels = new List<VoiceChannelInfo>();
        if (socketGuild != null)
        {
            foreach (var channel in socketGuild.VoiceChannels.Where(c => c != null).OrderBy(c => c.Position))
            {
                availableChannels.Add(new VoiceChannelInfo
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    MemberCount = channel.ConnectedUsers.Count
                });
            }
        }

        // Get connected channel info if connected
        string? connectedChannelName = null;
        int? channelMemberCount = null;
        if (isConnected && connectedChannelId.HasValue && socketGuild != null)
        {
            var connectedChannel = socketGuild.GetVoiceChannel(connectedChannelId.Value);
            if (connectedChannel != null)
            {
                connectedChannelName = connectedChannel.Name;
                channelMemberCount = connectedChannel.ConnectedUsers.Count;
            }
        }

        return new VoiceChannelPanelViewModel
        {
            GuildId = guildId,
            IsConnected = isConnected,
            ConnectedChannelId = connectedChannelId,
            ConnectedChannelName = connectedChannelName,
            ChannelMemberCount = channelMemberCount,
            AvailableChannels = availableChannels,
            // Now playing, Stop and the queue are filled in over SignalR as the bot plays
        };
    }

    /// <summary>
    /// Applies the current sort order to the sounds collection.
    /// </summary>
    /// <param name="sounds">The sounds to sort.</param>
    /// <returns>A sorted list of sounds.</returns>
    private IReadOnlyList<Sound> ApplySorting(IReadOnlyList<Sound> sounds)
    {
        return Sort?.ToLowerInvariant() switch
        {
            "name-desc" => sounds.OrderByDescending(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "newest" => sounds.OrderByDescending(s => s.UploadedAt).ToList(),
            "oldest" => sounds.OrderBy(s => s.UploadedAt).ToList(),
            _ => sounds.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList() // Default: name-asc
        };
    }
}
