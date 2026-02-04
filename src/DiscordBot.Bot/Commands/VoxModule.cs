using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for VOX clip playback commands.
/// Allows users to play synthesized voice announcements using VOX, FVOX, and HGRUNT clip libraries.
/// </summary>
[RequireGuildActive]
[RequireAudioEnabled]
[RateLimit(5, 10)]
public class VoxModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IVoxService _voxService;
    private readonly IOptions<VoxOptions> _voxOptions;
    private readonly ILogger<VoxModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoxModule"/> class.
    /// </summary>
    public VoxModule(
        IVoxService voxService,
        IOptions<VoxOptions> voxOptions,
        ILogger<VoxModule> logger)
    {
        _voxService = voxService;
        _voxOptions = voxOptions;
        _logger = logger;
    }

    /// <summary>
    /// Plays a VOX (scientist voice) group announcement.
    /// </summary>
    /// <param name="message">The message to play using VOX clips.</param>
    /// <param name="gap">Word gap in milliseconds (20-200). Defaults to configured value.</param>
    [SlashCommand("vox", "Play a VOX group announcement")]
    [RequireVoiceChannel]
    public async Task VoxAsync(
        [Summary("message", "The message to play")]
        [MaxLength(500)]
        [Autocomplete(typeof(VoxClipAutocompleteHandler))]
        string message,
        [Summary("gap", "Word gap in milliseconds (20-200)")]
        [MinValue(20)]
        [MaxValue(200)]
        int? gap = null)
    {
        await PlayVoxAsync(message, VoxClipGroup.Vox, gap);
    }

    /// <summary>
    /// Plays an FVOX (female scientist voice) group announcement.
    /// </summary>
    /// <param name="message">The message to play using FVOX clips.</param>
    /// <param name="gap">Word gap in milliseconds (20-200). Defaults to configured value.</param>
    [SlashCommand("fvox", "Play an FVOX group announcement")]
    [RequireVoiceChannel]
    public async Task FvoxAsync(
        [Summary("message", "The message to play")]
        [MaxLength(500)]
        [Autocomplete(typeof(FvoxClipAutocompleteHandler))]
        string message,
        [Summary("gap", "Word gap in milliseconds (20-200)")]
        [MinValue(20)]
        [MaxValue(200)]
        int? gap = null)
    {
        await PlayVoxAsync(message, VoxClipGroup.Fvox, gap);
    }

    /// <summary>
    /// Plays an HGRUNT (military voice) group announcement.
    /// </summary>
    /// <param name="message">The message to play using HGRUNT clips.</param>
    /// <param name="gap">Word gap in milliseconds (20-200). Defaults to configured value.</param>
    [SlashCommand("hgrunt", "Play an HGRUNT group announcement")]
    [RequireVoiceChannel]
    public async Task HgruntAsync(
        [Summary("message", "The message to play")]
        [MaxLength(500)]
        [Autocomplete(typeof(HgruntClipAutocompleteHandler))]
        string message,
        [Summary("gap", "Word gap in milliseconds (20-200)")]
        [MinValue(20)]
        [MaxValue(200)]
        int? gap = null)
    {
        await PlayVoxAsync(message, VoxClipGroup.Hgrunt, gap);
    }

    /// <summary>
    /// Shared helper method for playing VOX messages.
    /// </summary>
    /// <param name="message">The message to play.</param>
    /// <param name="group">The VOX clip group to use.</param>
    /// <param name="gap">Optional word gap in milliseconds.</param>
    private async Task PlayVoxAsync(string message, VoxClipGroup group, int? gap)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;
        var groupName = group.ToString().ToUpperInvariant();
        var wordGapMs = gap ?? _voxOptions.Value.DefaultWordGapMs;

        _logger.LogInformation(
            "VOX_COMMAND_STARTED: {Group} command by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message: {Message}, WordGapMs: {WordGapMs}, Source: {Source}",
            groupName,
            Context.User.Username,
            userId,
            Context.Guild.Name,
            guildId,
            message,
            wordGapMs,
            "SlashCommand");

        await DeferAsync(ephemeral: true);

        try
        {
            var options = new VoxPlaybackOptions { WordGapMs = wordGapMs };

            var result = await _voxService.PlayAsync(guildId, message, group, options);

            if (!result.Success)
            {
                // Log using standardized VOX_COMMAND_FAILED format
                // ErrorType is determined by the service and included in the result
                _logger.LogWarning(
                    "VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorMessage}",
                    groupName,
                    guildId,
                    result.ErrorMessage);

                await FollowupAsync(
                    text: result.ErrorMessage ?? "An unknown error occurred.",
                    ephemeral: true);
                return;
            }

            // Build success message
            var responseLines = new List<string>();

            if (result.MatchedWords.Count > 0)
            {
                responseLines.Add($"Playing: {string.Join(" ", result.MatchedWords)}");
            }

            if (result.SkippedWords.Count > 0)
            {
                responseLines.Add($"Skipped (no clip): {string.Join(", ", result.SkippedWords)}");
            }

            var responseText = responseLines.Count > 0
                ? string.Join("\n", responseLines)
                : "Playback initiated.";

            await FollowupAsync(text: responseText, ephemeral: true);
        }
        catch (Exception ex)
        {
            // Log unhandled exceptions using standardized format
            _logger.LogError(
                ex,
                "VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorType} - {ErrorMessage}",
                groupName,
                guildId,
                "UnknownError",
                ex.Message);

            await FollowupAsync(
                text: "An error occurred while trying to play the message. Please try again later.",
                ephemeral: true);
        }
    }
}
