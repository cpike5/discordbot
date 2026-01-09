using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Soundboard;

/// <summary>
/// Page model for the Soundboard management page.
/// Displays sounds, statistics, and settings for a guild's soundboard.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly ISoundService _soundService;
    private readonly ISoundFileService _soundFileService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISoundService soundService,
        ISoundFileService soundFileService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ILogger<IndexModel> logger)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _audioSettingsRepository = audioSettingsRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _audioService = audioService;
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
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message from TempData.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

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

            _logger.LogDebug("Retrieved {Count} sounds for guild {GuildId}", sounds.Count, guildId);

            // Build view model
            ViewModel = SoundboardIndexViewModel.Create(
                guildId,
                guild.Name,
                guild.IconUrl,
                sounds,
                settings);

            // Build voice channel panel view model
            VoiceChannelPanel = BuildVoiceChannelPanelViewModel(guildId);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Soundboard page for guild {GuildId}", guildId);
            ErrorMessage = "Failed to load soundboard. Please try again.";

            // Set fallback voice channel panel
            VoiceChannelPanel = new VoiceChannelPanelViewModel { GuildId = guildId };

            return Page();
        }
    }

    /// <summary>
    /// Handles POST requests to delete a sound.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="soundId">The sound ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to delete sound {SoundId} for guild {GuildId}",
            soundId, guildId);

        try
        {
            // Get sound to verify it exists and get filename
            var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
            if (sound == null)
            {
                _logger.LogWarning("Sound {SoundId} not found for guild {GuildId}", soundId, guildId);
                ErrorMessage = "Sound not found.";
                return RedirectToPage("Index", new { guildId });
            }

            // Delete the physical file first
            var fileDeleted = await _soundFileService.DeleteSoundFileAsync(
                guildId,
                sound.FileName,
                cancellationToken);

            if (!fileDeleted)
            {
                _logger.LogWarning("File {FileName} not found on disk for sound {SoundId}",
                    sound.FileName, soundId);
            }

            // Delete the database record
            var dbDeleted = await _soundService.DeleteSoundAsync(soundId, guildId, cancellationToken);

            if (dbDeleted)
            {
                _logger.LogInformation("Successfully deleted sound {SoundId} ({Name})",
                    soundId, sound.Name);
                SuccessMessage = "Sound deleted successfully.";
            }
            else
            {
                _logger.LogWarning("Failed to delete sound {SoundId} from database", soundId);
                ErrorMessage = "Failed to delete sound from database.";
            }

            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sound {SoundId} for guild {GuildId}",
                soundId, guildId);
            ErrorMessage = "An error occurred while deleting the sound. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Handles POST requests to upload a new sound file.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="file">The uploaded file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostUploadAsync(
        ulong guildId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to upload sound file for guild {GuildId}", guildId);

        try
        {
            // Validate file exists
            if (file == null || file.Length == 0)
            {
                ErrorMessage = "Please select a file to upload.";
                return RedirectToPage("Index", new { guildId });
            }

            // Validate file extension
            if (!_soundFileService.IsValidAudioFormat(file.FileName))
            {
                ErrorMessage = "Invalid file format. Supported formats: MP3, WAV, OGG, M4A.";
                return RedirectToPage("Index", new { guildId });
            }

            // Get settings for validation
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

            // Validate file size
            if (file.Length > settings.MaxFileSizeBytes)
            {
                var maxSizeMB = settings.MaxFileSizeBytes / (1024.0 * 1024.0);
                ErrorMessage = $"File size exceeds the maximum allowed size of {maxSizeMB:F1} MB.";
                return RedirectToPage("Index", new { guildId });
            }

            // Validate storage limit
            var storageValid = await _soundService.ValidateStorageLimitAsync(
                guildId,
                file.Length,
                cancellationToken);

            if (!storageValid)
            {
                ErrorMessage = "Adding this file would exceed the guild's storage limit.";
                return RedirectToPage("Index", new { guildId });
            }

            // Validate sound count limit
            var countValid = await _soundService.ValidateSoundCountLimitAsync(
                guildId,
                cancellationToken);

            if (!countValid)
            {
                ErrorMessage = $"Guild has reached the maximum limit of {settings.MaxSoundsPerGuild} sounds.";
                return RedirectToPage("Index", new { guildId });
            }

            // Generate unique filename to avoid conflicts
            var fileExtension = Path.GetExtension(file.FileName);
            var generatedFileName = $"{Guid.NewGuid()}{fileExtension}";

            // Save file to disk
            await using (var stream = file.OpenReadStream())
            {
                await _soundFileService.SaveSoundFileAsync(
                    guildId,
                    generatedFileName,
                    stream,
                    cancellationToken);
            }

            _logger.LogDebug("Saved sound file {FileName} to disk for guild {GuildId}",
                generatedFileName, guildId);

            // Create Sound entity
            var sound = new Sound
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                Name = Path.GetFileNameWithoutExtension(file.FileName),
                FileName = generatedFileName,
                FileSizeBytes = file.Length,
                DurationSeconds = 0, // Placeholder - duration detection is future enhancement
                UploadedAt = DateTime.UtcNow
            };

            // Save to database
            await _soundService.CreateSoundAsync(sound, cancellationToken);

            _logger.LogInformation("Successfully uploaded sound {Name} for guild {GuildId}",
                sound.Name, guildId);
            SuccessMessage = $"Sound '{sound.Name}' uploaded successfully.";

            return RedirectToPage("Index", new { guildId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            _logger.LogWarning(ex, "Duplicate sound name for guild {GuildId}", guildId);
            ErrorMessage = "A sound with this name already exists.";
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading sound for guild {GuildId}", guildId);
            ErrorMessage = "An error occurred while uploading the sound. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
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
                ErrorMessage = "Audio features are not enabled for this guild.";
                return RedirectToPage("Index", new { guildId });
            }

            // Discover sound files from the guild's directory
            var discoveredFiles = await _soundFileService.DiscoverSoundFilesAsync(
                guildId,
                cancellationToken);

            if (discoveredFiles.Count == 0)
            {
                ErrorMessage = "No sound files found in the guild's directory.";
                return RedirectToPage("Index", new { guildId });
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

                var sound = new Sound
                {
                    Id = Guid.NewGuid(),
                    GuildId = guildId,
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    FileName = fileName,
                    FileSizeBytes = fileInfo.Length,
                    DurationSeconds = 0, // Placeholder - duration detection is future enhancement
                    UploadedAt = DateTime.UtcNow
                };

                try
                {
                    await _soundService.CreateSoundAsync(sound, cancellationToken);
                    newSoundsCount++;
                    _logger.LogDebug("Discovered and added sound {Name} from file {FileName}",
                        sound.Name, fileName);
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
                SuccessMessage = $"Discovered {newSoundsCount} new sound(s).";
            }
            else
            {
                ErrorMessage = "No new sounds found. All files in the directory are already registered.";
            }

            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering sounds for guild {GuildId}", guildId);
            ErrorMessage = "An error occurred while discovering sounds. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Handles POST requests to rename a sound.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="soundId">The sound ID to rename.</param>
    /// <param name="newName">The new name for the sound.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostRenameAsync(
        ulong guildId,
        Guid soundId,
        [FromForm] string newName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to rename sound {SoundId} to {NewName} for guild {GuildId}",
            soundId, newName, guildId);

        try
        {
            // Validate new name
            if (string.IsNullOrWhiteSpace(newName))
            {
                ErrorMessage = "Sound name cannot be empty.";
                return RedirectToPage("Index", new { guildId });
            }

            // Get sound
            var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
            if (sound == null)
            {
                _logger.LogWarning("Sound {SoundId} not found for guild {GuildId}", soundId, guildId);
                ErrorMessage = "Sound not found.";
                return RedirectToPage("Index", new { guildId });
            }

            var oldName = sound.Name;
            sound.Name = newName.Trim();

            // Note: The repository pattern doesn't expose an UpdateSoundAsync method in ISoundService.
            // For now, we'll just reload the page. In a future enhancement, we could add an UpdateSoundAsync method.
            // Since this is a limitation, we'll set an error message.

            ErrorMessage = "Rename functionality is not yet implemented. Please delete and re-upload the sound with the new name.";
            _logger.LogWarning("Rename attempted but UpdateSoundAsync not available in ISoundService");

            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming sound {SoundId} for guild {GuildId}",
                soundId, guildId);
            ErrorMessage = "An error occurred while renaming the sound. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
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
            AvailableChannels = availableChannels
            // NowPlaying and Queue will be populated via SignalR in real-time
        };
    }
}
