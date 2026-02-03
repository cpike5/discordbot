using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal VOX operations.
/// Provides VOX announcement playback functionality for authenticated guild members.
/// </summary>
[ApiController]
[Route("api/portal/vox/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalVoxController : ControllerBase
{
    private readonly IVoxService _voxService;
    private readonly IVoxClipLibrary _voxClipLibrary;
    private readonly IPlaybackService _playbackService;
    private readonly IAudioService _audioService;
    private readonly ISettingsService _settingsService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<PortalVoxController> _logger;

    private const int MaxMessageLength = 500;
    private const int MaxWordCount = 50;
    private const int MinWordGapMs = 20;
    private const int MaxWordGapMs = 200;
    private const int DefaultWordGapMs = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalVoxController"/> class.
    /// </summary>
    /// <param name="voxService">The VOX service for playback operations.</param>
    /// <param name="voxClipLibrary">The VOX clip library for clip retrieval.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="settingsService">The bot-level settings service.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="logger">The logger.</param>
    public PortalVoxController(
        IVoxService voxService,
        IVoxClipLibrary voxClipLibrary,
        IPlaybackService playbackService,
        IAudioService audioService,
        ISettingsService settingsService,
        DiscordSocketClient discordClient,
        ILogger<PortalVoxController> logger)
    {
        _voxService = voxService;
        _voxClipLibrary = voxClipLibrary;
        _playbackService = playbackService;
        _audioService = audioService;
        _settingsService = settingsService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// Checks if audio features are globally enabled at the bot level.
    /// </summary>
    /// <returns>True if audio is globally enabled, false otherwise.</returns>
    private async Task<bool> IsAudioGloballyEnabledAsync()
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
    }

    /// <summary>
    /// Gets all clips for a specific group with optional search filter.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="group">The VOX clip group (vox, fvox, or hgrunt).</param>
    /// <param name="search">Optional search query to filter clips.</param>
    /// <returns>List of clips matching the criteria.</returns>
    [HttpGet("clips")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public IActionResult GetClips(ulong guildId, [FromQuery] string group = "vox", [FromQuery] string? search = null)
    {
        _logger.LogInformation("Get VOX clips request for guild {GuildId}, group {Group}, search {Search}",
            guildId, group, search ?? "(none)");

        // Validate guild exists
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = "The requested guild was not found or the bot is not a member.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Parse group name
        if (!TryParseVoxClipGroup(group, out var clipGroup))
        {
            _logger.LogWarning("Invalid VOX group name: {Group}", group);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid group name",
                Detail = $"The group '{group}' is not valid. Valid groups are: vox, fvox, hgrunt.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get clips (with search filter if provided)
        IReadOnlyList<VoxClipInfo> clips;
        if (!string.IsNullOrWhiteSpace(search))
        {
            clips = _voxClipLibrary.SearchClips(clipGroup, search, maxResults: 100);
        }
        else
        {
            clips = _voxClipLibrary.GetClips(clipGroup);
        }

        // Map to response format
        var response = new
        {
            group = clipGroup.ToString().ToLowerInvariant(),
            clips = clips.Select(c => new
            {
                name = c.Name,
                durationSeconds = c.DurationSeconds,
                fileSizeBytes = c.FileSizeBytes
            }).ToList(),
            totalClips = clips.Count
        };

        _logger.LogInformation("Returning {Count} VOX clips for group {Group} in guild {GuildId}",
            clips.Count, clipGroup, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets a preview of how a message would be tokenized and played.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="message">The message to preview.</param>
    /// <param name="group">The VOX clip group (vox, fvox, or hgrunt).</param>
    /// <returns>Token preview showing matched/skipped words with durations.</returns>
    [HttpGet("preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public IActionResult GetPreview(ulong guildId, [FromQuery] string message, [FromQuery] string group = "vox")
    {
        _logger.LogInformation("Get VOX preview request for guild {GuildId}, message length {Length}, group {Group}",
            guildId, message?.Length ?? 0, group);

        // Validate guild exists
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = "The requested guild was not found or the bot is not a member.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate message
        var validationError = ValidateMessage(message);
        if (validationError != null)
        {
            return validationError;
        }

        // Parse group name
        if (!TryParseVoxClipGroup(group, out var clipGroup))
        {
            _logger.LogWarning("Invalid VOX group name: {Group}", group);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid group name",
                Detail = $"The group '{group}' is not valid. Valid groups are: vox, fvox, hgrunt.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get token preview
        var preview = _voxService.TokenizePreview(message!, clipGroup);

        // Map to response format
        var response = new
        {
            tokens = preview.Tokens.Select(t => new
            {
                word = t.Word,
                hasClip = t.HasClip,
                durationSeconds = t.DurationSeconds
            }).ToList(),
            matchedCount = preview.MatchedCount,
            skippedCount = preview.SkippedCount,
            estimatedDurationSeconds = preview.EstimatedDurationSeconds
        };

        _logger.LogInformation("VOX preview generated for guild {GuildId}: {MatchedCount} matched, {SkippedCount} skipped",
            guildId, preview.MatchedCount, preview.SkippedCount);

        return Ok(response);
    }

    /// <summary>
    /// Plays a VOX announcement in the bot's current voice channel.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The play request containing message, group, and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Playback result with matched/skipped words and duration.</returns>
    [HttpPost("play")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Play(
        ulong guildId,
        [FromBody] VoxPlayRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Play VOX request for guild {GuildId}, message length {Length}, group {Group}",
            guildId, request.Message?.Length ?? 0, request.Group);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting Play for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate guild exists
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = "The requested guild was not found or the bot is not a member.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if bot is connected to voice
        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice channel",
                Detail = "The bot must be connected to a voice channel before playing VOX announcements.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate message
        var validationError = ValidateMessage(request.Message);
        if (validationError != null)
        {
            return validationError;
        }

        // Parse group name
        if (!TryParseVoxClipGroup(request.Group, out var clipGroup))
        {
            _logger.LogWarning("Invalid VOX group name: {Group}", request.Group);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid group name",
                Detail = $"The group '{request.Group}' is not valid. Valid groups are: vox, fvox, hgrunt.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate word gap
        var wordGapMs = request.WordGapMs ?? DefaultWordGapMs;
        if (wordGapMs < MinWordGapMs || wordGapMs > MaxWordGapMs)
        {
            _logger.LogWarning("Invalid word gap: {WordGapMs} (valid range: {Min}-{Max})",
                wordGapMs, MinWordGapMs, MaxWordGapMs);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid word gap",
                Detail = $"Word gap must be between {MinWordGapMs} and {MaxWordGapMs} milliseconds.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if any clips will match (fail fast if no matches)
        var preview = _voxService.TokenizePreview(request.Message!, clipGroup);
        if (preview.MatchedCount == 0)
        {
            var unmatchedWords = preview.Tokens.Where(t => !t.HasClip).Select(t => t.Word).ToList();
            _logger.LogWarning("No matching clips for VOX message in guild {GuildId}. Unmatched words: {Words}",
                guildId, string.Join(", ", unmatchedWords));
            return BadRequest(new ApiErrorDto
            {
                Message = "No matching clips found",
                Detail = $"None of the words in the message have matching VOX clips. Unmatched words: {string.Join(", ", unmatchedWords)}",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Play the VOX message
        var options = new VoxPlaybackOptions { WordGapMs = wordGapMs };
        VoxPlaybackResult result;

        try
        {
            result = await _voxService.PlayAsync(guildId, request.Message!, clipGroup, options, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "VOX playback failed for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "VOX playback failed",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (!result.Success)
        {
            _logger.LogWarning("VOX playback failed for guild {GuildId}: {ErrorMessage}", guildId, result.ErrorMessage);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to play VOX message",
                Detail = result.ErrorMessage ?? "An error occurred during playback.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully played VOX message in guild {GuildId}: {MatchedCount} matched, {SkippedCount} skipped",
            guildId, result.MatchedWords.Count, result.SkippedWords.Count);

        // Return response matching the spec format
        var response = new
        {
            success = true,
            matchedWords = result.MatchedWords,
            skippedWords = result.SkippedWords,
            estimatedDurationSeconds = result.EstimatedDurationSeconds
        };

        return Ok(response);
    }

    /// <summary>
    /// Stops the currently playing audio in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Stop(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stop VOX playback request for guild {GuildId}", guildId);

        // Validate guild exists
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = "The requested guild was not found or the bot is not a member.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if bot is connected to voice
        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice channel",
                Detail = "The bot is not currently connected to a voice channel in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Stop playback
        await _playbackService.StopAsync(guildId, cancellationToken);

        _logger.LogInformation("Successfully stopped playback in guild {GuildId}", guildId);

        return Ok(new { success = true, message = "Playback stopped" });
    }

    /// <summary>
    /// Validates a VOX message against length and word count constraints.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <returns>An error response if validation fails, otherwise null.</returns>
    private IActionResult? ValidateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Empty VOX message provided");
            return BadRequest(new ApiErrorDto
            {
                Message = "Message cannot be empty",
                Detail = "Please provide a message to convert to VOX speech.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (message.Length > MaxMessageLength)
        {
            _logger.LogWarning("VOX message too long: {Length} (max: {Max})", message.Length, MaxMessageLength);
            return BadRequest(new ApiErrorDto
            {
                Message = "Message too long",
                Detail = $"Message length ({message.Length}) exceeds the maximum allowed ({MaxMessageLength}).",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var words = message.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxWordCount)
        {
            _logger.LogWarning("VOX message has too many words: {Count} (max: {Max})", words.Length, MaxWordCount);
            return BadRequest(new ApiErrorDto
            {
                Message = "Too many words",
                Detail = $"Message contains {words.Length} words, exceeding the maximum of {MaxWordCount}.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse a group name string to a VoxClipGroup enum.
    /// </summary>
    /// <param name="groupName">The group name string (case-insensitive).</param>
    /// <param name="group">The parsed VoxClipGroup if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    private static bool TryParseVoxClipGroup(string groupName, out VoxClipGroup group)
    {
        return Enum.TryParse(groupName, ignoreCase: true, out group);
    }

    /// <summary>
    /// Request model for VOX play endpoint.
    /// </summary>
    public class VoxPlayRequest
    {
        /// <summary>
        /// Gets or sets the message to convert to VOX speech.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the VOX clip group (vox, fvox, or hgrunt).
        /// </summary>
        public string Group { get; set; } = "vox";

        /// <summary>
        /// Gets or sets the word gap in milliseconds (20-200, default 50).
        /// </summary>
        public int? WordGapMs { get; set; }
    }
}
