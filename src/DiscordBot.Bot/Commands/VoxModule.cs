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
    /// The <c>voice</c> option value for the standard VOX (scientist) clip library.
    /// </summary>
    public const string VoiceVox = "vox";

    /// <summary>
    /// The <c>voice</c> option value for the FVOX (female scientist) clip library.
    /// </summary>
    public const string VoiceFvox = "fvox";

    /// <summary>
    /// The <c>voice</c> option value for the HGRUNT (military) clip library.
    /// </summary>
    public const string VoiceHgrunt = "hgrunt";

    /// <summary>
    /// Plays a Half-Life style announcement using the VOX, FVOX or HGRUNT clip library.
    /// </summary>
    /// <param name="message">The message to play.</param>
    /// <param name="voice">The clip library to use: <c>vox</c> (default), <c>fvox</c> or <c>hgrunt</c>.</param>
    /// <param name="gap">Word gap in milliseconds (20-200). Defaults to configured value.</param>
    [SlashCommand("vox", "Play a Half-Life style VOX announcement")]
    [RequireVoiceChannel]
    public async Task VoxAsync(
        [Summary("message", "The message to play")]
        [MaxLength(500)]
        [Autocomplete(typeof(VoxClipAutocompleteHandler))]
        string message,
        [Summary("voice", "Clip library to use (default: vox)")]
        [Choice("vox (scientist)", VoiceVox)]
        [Choice("fvox (female scientist)", VoiceFvox)]
        [Choice("hgrunt (military)", VoiceHgrunt)]
        string? voice = null,
        [Summary("gap", "Word gap in milliseconds (20-200)")]
        [MinValue(20)]
        [MaxValue(200)]
        int? gap = null)
    {
        await PlayVoxAsync(message, ParseVoice(voice), gap);
    }

    /// <summary>
    /// Maps the <c>voice</c> option value to a <see cref="VoxClipGroup"/>.
    /// Unknown or missing values fall back to <see cref="VoxClipGroup.Vox"/>.
    /// </summary>
    /// <param name="voice">The option value.</param>
    /// <returns>The clip group.</returns>
    public static VoxClipGroup ParseVoice(string? voice) =>
        voice?.Trim().ToLowerInvariant() switch
        {
            VoiceFvox => VoxClipGroup.Fvox,
            VoiceHgrunt => VoxClipGroup.Hgrunt,
            _ => VoxClipGroup.Vox
        };

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
