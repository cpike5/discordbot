using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Entities;
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
    private readonly ITtsHistoryService _ttsHistoryService;
    private readonly ILogger<TtsModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TtsModule"/> class.
    /// </summary>
    public TtsModule(
        IAudioService audioService,
        ITtsService ttsService,
        ITtsSettingsService ttsSettingsService,
        ITtsHistoryService ttsHistoryService,
        ILogger<TtsModule> logger)
    {
        _audioService = audioService;
        _ttsService = ttsService;
        _ttsSettingsService = ttsSettingsService;
        _ttsHistoryService = ttsHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// Converts text to speech and plays it in the user's voice channel.
    /// </summary>
    /// <param name="message">The text to speak.</param>
    /// <param name="voice">The voice to use for synthesis.</param>
    [SlashCommand("tts", "Convert text to speech and play in voice channel")]
    [RequireVoiceChannel]
    public async Task TtsAsync(
        [Summary("message", "The text to speak (max 500 characters)")]
        [MaxLength(500)]
        string message,
        [Summary("voice", "Voice to use")]
        [Autocomplete(typeof(VoiceAutocompleteHandler))]
        string? voice = null)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;
        var username = Context.User.Username;

        _logger.LogInformation(
            "TTS command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message length: {MessageLength}, Voice: {Voice}",
            username,
            userId,
            Context.Guild.Name,
            guildId,
            message.Length,
            voice ?? "default");

        try
        {
            // Check if TTS service is configured
            if (!_ttsService.IsConfigured)
            {
                _logger.LogWarning(
                    "TTS command used but service not configured in guild {GuildId}",
                    guildId);

                var notConfiguredEmbed = new EmbedBuilder()
                    .WithTitle("TTS Not Available")
                    .WithDescription("Text-to-speech is not configured on this server. Please contact an administrator.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notConfiguredEmbed, ephemeral: true);
                return;
            }

            // Get guild TTS settings
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId);

            // Check message length against guild settings
            if (message.Length > settings.MaxMessageLength)
            {
                _logger.LogDebug(
                    "TTS message too long: {Length} > {MaxLength} in guild {GuildId}",
                    message.Length,
                    settings.MaxMessageLength,
                    guildId);

                var tooLongEmbed = new EmbedBuilder()
                    .WithTitle("Message Too Long")
                    .WithDescription($"Message is too long. Maximum length is {settings.MaxMessageLength} characters.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: tooLongEmbed, ephemeral: true);
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

                var rateLimitEmbed = new EmbedBuilder()
                    .WithTitle("Rate Limited")
                    .WithDescription("You're sending TTS messages too quickly. Please wait a moment before trying again.")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: rateLimitEmbed, ephemeral: true);
                return;
            }

            // Get user's voice channel
            var guildUser = Context.User as SocketGuildUser;
            var voiceChannel = guildUser?.VoiceChannel;

            if (voiceChannel == null)
            {
                _logger.LogDebug(
                    "User {UserId} not in voice channel for TTS command in guild {GuildId}",
                    userId,
                    guildId);

                var noVoiceEmbed = new EmbedBuilder()
                    .WithTitle("Not in Voice Channel")
                    .WithDescription("You need to be in a voice channel to use this command.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: noVoiceEmbed, ephemeral: true);
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

            // Prepare TTS options
            var effectiveVoice = voice ?? settings.DefaultVoice;
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

            // Synthesize speech
            _logger.LogDebug(
                "Synthesizing TTS for guild {GuildId} with voice {Voice}",
                guildId,
                effectiveVoice);

            var startTime = DateTime.UtcNow;
            Stream audioStream;
            try
            {
                audioStream = await _ttsService.SynthesizeSpeechAsync(message, ttsOptions);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "TTS synthesis failed for guild {GuildId}", guildId);

                var synthesisErrorEmbed = new EmbedBuilder()
                    .WithTitle("Synthesis Failed")
                    .WithDescription("Failed to generate speech. Please try again.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: synthesisErrorEmbed, ephemeral: true);
                return;
            }

            // Play the audio
            var pcmStream = _audioService.GetOrCreatePcmStream(guildId);
            if (pcmStream == null)
            {
                _logger.LogError("Failed to get PCM stream for guild {GuildId}", guildId);

                var streamErrorEmbed = new EmbedBuilder()
                    .WithTitle("Playback Error")
                    .WithDescription("Failed to connect to voice channel. Please try again.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: streamErrorEmbed, ephemeral: true);
                return;
            }

            // Stream the audio to Discord
            _logger.LogInformation(
                "Playing TTS audio in guild {GuildId} for user {UserId}",
                guildId,
                userId);

            _audioService.UpdateLastActivity(guildId);

            await using (audioStream)
            {
                await audioStream.CopyToAsync(pcmStream);
                await pcmStream.FlushAsync();
            }

            var endTime = DateTime.UtcNow;
            var durationSeconds = (endTime - startTime).TotalSeconds;

            // Log the TTS message to history
            var ttsMessage = new TtsMessage
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = userId,
                Username = username,
                Message = message,
                Voice = effectiveVoice,
                DurationSeconds = durationSeconds,
                CreatedAt = DateTime.UtcNow
            };

            await _ttsHistoryService.LogMessageAsync(ttsMessage);

            _logger.LogInformation(
                "TTS playback completed for user {UserId} in guild {GuildId}, duration: {Duration:F2}s",
                userId,
                guildId,
                durationSeconds);

            // Success response
            var successEmbed = new EmbedBuilder()
                .WithTitle("TTS Playing")
                .WithDescription($"Now speaking: \"{TruncateMessage(message, 100)}\"")
                .WithColor(Color.Green)
                .AddField("Voice", effectiveVoice, inline: true)
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: successEmbed, ephemeral: true);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError(
                "Bot lacks permissions to join voice channel in guild {GuildId}",
                guildId);

            var permissionEmbed = new EmbedBuilder()
                .WithTitle("Permission Denied")
                .WithDescription("I don't have permission to join that voice channel.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

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

            var errorEmbed = new EmbedBuilder()
                .WithTitle("TTS Error")
                .WithDescription("An error occurred while processing your TTS request. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

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
