using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for text-to-speech playback commands.
/// Allows users to convert text to speech and play it in voice channels.
/// </summary>
[RequireGuildActive]
[RequireTtsEnabled]
[RateLimit(5, 10)]
public class TtsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly ITtsService _ttsService;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ITtsPlaybackService _ttsPlaybackService;
    private readonly ISsmlBuilder _ssmlBuilder;
    private readonly IStylePresetProvider _stylePresetProvider;
    private readonly ILogger<TtsModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TtsModule"/> class.
    /// </summary>
    public TtsModule(
        IAudioService audioService,
        ITtsService ttsService,
        ITtsSettingsService ttsSettingsService,
        ITtsPlaybackService ttsPlaybackService,
        ISsmlBuilder ssmlBuilder,
        IStylePresetProvider stylePresetProvider,
        ILogger<TtsModule> logger)
    {
        _audioService = audioService;
        _ttsService = ttsService;
        _ttsSettingsService = ttsSettingsService;
        _ttsPlaybackService = ttsPlaybackService;
        _ssmlBuilder = ssmlBuilder;
        _stylePresetProvider = stylePresetProvider;
        _logger = logger;
    }

    /// <summary>
    /// Converts text to speech and plays it in the user's voice channel.
    /// When a style preset is given, the speech is built as SSML from the preset (which requires
    /// SSML to be enabled for the guild) and the preset's voice overrides <paramref name="voice"/>.
    /// </summary>
    /// <param name="message">The text to speak.</param>
    /// <param name="voice">The voice to use for synthesis. Ignored when <paramref name="preset"/> is set.</param>
    /// <param name="preset">Optional style preset ID. Overrides <paramref name="voice"/>.</param>
    [SlashCommand("tts", "Convert text to speech and play in voice channel")]
    [RequireVoiceChannel]
    public async Task TtsAsync(
        [Summary("message", "The text to speak (max 500 characters)")]
        [MaxLength(500)]
        string message,
        [Summary("voice", "Voice to use")]
        [Autocomplete(typeof(VoiceAutocompleteHandler))]
        string? voice = null,
        [Summary("preset", "Style preset to use (overrides voice; requires SSML)")]
        [Autocomplete(typeof(StylePresetAutocompleteHandler))]
        string? preset = null)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;
        var username = Context.User.Username;
        var usePreset = !string.IsNullOrWhiteSpace(preset);

        _logger.LogInformation(
            "TTS command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message length: {MessageLength}, Voice: {Voice}, Preset: {Preset}",
            username,
            userId,
            Context.Guild.Name,
            guildId,
            message.Length,
            voice ?? "default",
            preset ?? "none");

        try
        {
            // Check if TTS service is configured
            if (!_ttsService.IsConfigured)
            {
                _logger.LogWarning(
                    "TTS command used but service not configured in guild {GuildId}",
                    guildId);

                await RespondAsync(embed: EmbedHelper.Error("TTS Not Available", "Text-to-speech is not configured on this server. Please contact an administrator."), ephemeral: true);
                return;
            }

            // Get guild TTS settings
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId);

            StylePreset? selectedPreset = null;
            if (usePreset)
            {
                // Styled TTS requires SSML to be enabled for the guild
                if (!settings.SsmlEnabled)
                {
                    _logger.LogDebug(
                        "TTS preset used but SSML not enabled in guild {GuildId}",
                        guildId);

                    await RespondAsync(embed: EmbedHelper.Confirmation("SSML Not Enabled", "Style presets require SSML to be enabled for this server. Please contact an administrator to enable it in TTS settings."), ephemeral: true);
                    return;
                }

                selectedPreset = _stylePresetProvider.GetPresetById(preset!);
                if (selectedPreset == null)
                {
                    _logger.LogWarning(
                        "Invalid preset ID {PresetId} requested in guild {GuildId}",
                        preset,
                        guildId);

                    await RespondAsync(embed: EmbedHelper.Error("Invalid Preset", "The requested style preset was not found. Please select a valid preset."), ephemeral: true);
                    return;
                }
            }

            // Check message length against guild settings
            if (message.Length > settings.MaxMessageLength)
            {
                _logger.LogDebug(
                    "TTS message too long: {Length} > {MaxLength} in guild {GuildId}",
                    message.Length,
                    settings.MaxMessageLength,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Error("Message Too Long", $"Message is too long. Maximum length is {settings.MaxMessageLength} characters."), ephemeral: true);
                return;
            }

            // Check rate limit
            var isRateLimited = await _ttsSettingsService.IsUserRateLimitedAsync(guildId, userId);
            if (isRateLimited)
            {
                _logger.LogDebug(
                    "User {UserId} rate limited for TTS in guild {GuildId}",
                    userId,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Confirmation("Rate Limited", "You're sending TTS messages too quickly. Please wait a moment before trying again."), ephemeral: true);
                return;
            }

            // Get user's voice channel
            var (isValid, voiceChannel, voiceErrorEmbed) = VoiceChannelHelper.ValidateUserInVoiceChannel(Context);
            if (!isValid)
            {
                _logger.LogDebug(
                    "User {UserId} not in voice channel for TTS command in guild {GuildId}",
                    userId,
                    guildId);

                await RespondAsync(embed: voiceErrorEmbed, ephemeral: true);
                return;
            }

            // Defer the response since TTS synthesis may take a moment
            await DeferAsync(ephemeral: true);

            // Auto-join or switch to user's voice channel
            var isConnected = _audioService.IsConnected(guildId);
            if (!isConnected)
            {
                _logger.LogDebug(
                    "Bot not connected to voice in guild {GuildId}, connecting to channel {ChannelId}",
                    guildId,
                    voiceChannel.Id);

                await _audioService.JoinChannelAsync(guildId, voiceChannel.Id);
            }
            else
            {
                // Check if connected to a different channel
                var currentChannelId = _audioService.GetConnectedChannelId(guildId);
                if (currentChannelId.HasValue && currentChannelId.Value != voiceChannel.Id)
                {
                    _logger.LogDebug(
                        "Bot connected to different channel in guild {GuildId}, switching from {CurrentChannelId} to {NewChannelId}",
                        guildId,
                        currentChannelId.Value,
                        voiceChannel.Id);

                    await _audioService.LeaveChannelAsync(guildId);
                    await _audioService.JoinChannelAsync(guildId, voiceChannel.Id);
                }
            }

            string effectiveVoice;
            Stream audioStream;
            try
            {
                if (selectedPreset != null)
                {
                    effectiveVoice = selectedPreset.VoiceName;
                    var ssmlText = BuildPresetSsml(selectedPreset, message);

                    _logger.LogDebug(
                        "Synthesizing TTS with SSML for guild {GuildId} using preset {PresetId} (Voice: {Voice}, Style: {Style})",
                        guildId,
                        selectedPreset.PresetId,
                        selectedPreset.VoiceName,
                        selectedPreset.Style);

                    audioStream = await _ttsService.SynthesizeSpeechAsync(ssmlText, options: null, SynthesisMode.Ssml);
                }
                else
                {
                    effectiveVoice = voice ?? settings.DefaultVoice;
                    if (string.IsNullOrWhiteSpace(effectiveVoice))
                    {
                        effectiveVoice = "en-US-JennyNeural"; // Fallback default
                    }

                    var ttsOptions = new TtsOptions
                    {
                        Voice = effectiveVoice,
                        Speed = settings.DefaultSpeed,
                        Pitch = settings.DefaultPitch,
                        Volume = settings.DefaultVolume
                    };

                    _logger.LogDebug(
                        "Synthesizing TTS for guild {GuildId} with voice {Voice}",
                        guildId,
                        effectiveVoice);

                    audioStream = await _ttsService.SynthesizeSpeechAsync(message, ttsOptions);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "TTS synthesis failed for guild {GuildId}", guildId);

                await FollowupAsync(embed: EmbedHelper.Error("Synthesis Failed", "Failed to generate speech. Please try again."), ephemeral: true);
                return;
            }

            // Play the audio using the TTS playback service
            _logger.LogInformation(
                "Playing TTS audio in guild {GuildId} for user {UserId}",
                guildId,
                userId);

            Core.DTOs.Tts.TtsPlaybackResult playbackResult;
            await using (audioStream)
            {
                playbackResult = await _ttsPlaybackService.PlayAsync(
                    guildId,
                    userId,
                    username,
                    message,
                    effectiveVoice,
                    audioStream);
            }

            if (!playbackResult.Success)
            {
                _logger.LogError("TTS playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);

                await FollowupAsync(embed: EmbedHelper.Error("Playback Error", playbackResult.ErrorMessage ?? "Failed to play audio. Please try again."), ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "TTS playback completed for user {UserId} in guild {GuildId}, preset: {PresetId}, duration: {Duration:F2}s",
                userId,
                guildId,
                selectedPreset?.PresetId ?? "none",
                playbackResult.DurationSeconds);

            // Success response
            var successEmbedBuilder = new EmbedBuilder()
                .WithTitle("TTS Playing")
                .WithDescription($"Now speaking: \"{TruncateMessage(message, 100)}\"")
                .WithColor(Color.Green);

            if (selectedPreset != null)
            {
                successEmbedBuilder
                    .AddField("Preset", selectedPreset.DisplayName, inline: true)
                    .AddField("Voice", selectedPreset.VoiceName, inline: true)
                    .AddField("Style", $"{selectedPreset.Style} ({selectedPreset.StyleDegree:F2})", inline: true);
            }
            else
            {
                successEmbedBuilder.AddField("Voice", effectiveVoice, inline: true);
            }

            await FollowupAsync(embed: successEmbedBuilder.WithCurrentTimestamp().Build(), ephemeral: true);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError(
                "Bot lacks permissions to join voice channel in guild {GuildId}",
                guildId);

            var permissionEmbed = EmbedHelper.Error("Permission Denied", "I don't have permission to join that voice channel.");

            if (Context.Interaction.HasResponded)
            {
                await FollowupAsync(embed: permissionEmbed, ephemeral: true);
            }
            else
            {
                await RespondAsync(embed: permissionEmbed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "TTS command failed for user {UserId} in guild {GuildId}",
                userId,
                guildId);

            var errorEmbed = EmbedHelper.Error("TTS Error", "An error occurred while processing your TTS request. Please try again later.");

            if (Context.Interaction.HasResponded)
            {
                await FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
            else
            {
                await RespondAsync(embed: errorEmbed, ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Builds the SSML document for a message spoken with a style preset.
    /// </summary>
    /// <param name="preset">The style preset.</param>
    /// <param name="message">The text to speak.</param>
    /// <returns>The SSML markup.</returns>
    private string BuildPresetSsml(StylePreset preset, string message)
    {
        _ssmlBuilder.Reset();
        _ssmlBuilder
            .BeginDocument("en-US")
            .WithVoice(preset.VoiceName)
            .WithStyle(preset.Style, preset.StyleDegree);

        if (preset.ProsodyOptions != null)
        {
            _ssmlBuilder.WithProsody(
                preset.ProsodyOptions.Speed,
                preset.ProsodyOptions.Pitch,
                preset.ProsodyOptions.Volume);
        }

        _ssmlBuilder
            .AddText(message)
            .EndStyle()
            .EndVoice();

        return _ssmlBuilder.Build();
    }

    /// <summary>
    /// Truncates a message for display, adding ellipsis if needed.
    /// </summary>
    /// <param name="message">The message to truncate.</param>
    /// <param name="maxLength">Maximum length before truncation.</param>
    /// <returns>The truncated message.</returns>
    private static string TruncateMessage(string message, int maxLength)
    {
        if (message.Length <= maxLength)
        {
            return message;
        }

        return message[..(maxLength - 3)] + "...";
    }
}
