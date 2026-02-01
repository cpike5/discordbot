using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Constants;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for soundboard playback commands.
/// Allows users to play sounds from the guild's soundboard, list available sounds, and control playback.
/// </summary>
[RequireGuildActive]
[RequireAudioEnabled]
[RateLimit(5, 10)]
public class SoundboardModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly ISoundService _soundService;
    private readonly ISoundboardOrchestrationService _orchestrationService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ILogger<SoundboardModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundboardModule"/> class.
    /// </summary>
    public SoundboardModule(
        IAudioService audioService,
        IPlaybackService playbackService,
        ISoundService soundService,
        ISoundboardOrchestrationService orchestrationService,
        IGuildAudioSettingsService audioSettingsService,
        ILogger<SoundboardModule> logger)
    {
        _audioService = audioService;
        _playbackService = playbackService;
        _soundService = soundService;
        _orchestrationService = orchestrationService;
        _audioSettingsService = audioSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Plays a sound from the guild's soundboard.
    /// </summary>
    /// <param name="soundName">The name of the sound to play.</param>
    /// <param name="filter">The audio filter to apply (optional).</param>
    [SlashCommand("play", "Play a sound from the soundboard")]
    [RequireVoiceChannel]
    public async Task PlayAsync(
        [Summary("sound", "Name of the sound to play")]
        [Autocomplete(typeof(SoundAutocompleteHandler))]
        string soundName,
        [Summary("filter", "Audio filter to apply")]
        [Autocomplete(typeof(FilterAutocompleteHandler))]
        AudioFilter filter = AudioFilter.None)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        _logger.LogInformation(
            "Play command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Sound: {SoundName}, Filter: {Filter}",
            Context.User.Username,
            userId,
            Context.Guild.Name,
            guildId,
            soundName,
            filter);

        try
        {
            // Get the sound by name
            var sound = await _soundService.GetByNameAsync(soundName, guildId);

            if (sound == null)
            {
                _logger.LogDebug(
                    "Sound '{SoundName}' not found in guild {GuildId}",
                    soundName,
                    guildId);

                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("Sound Not Found")
                    .WithDescription($"Sound '{soundName}' not found.")
                    .WithColor(Color.Red)
                    .AddField("Suggestion", "Use `/sounds` to see available sounds.")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                return;
            }

            // Get user's voice channel
            var guildUser = Context.User as SocketGuildUser;
            var voiceChannel = guildUser?.VoiceChannel;

            if (voiceChannel == null)
            {
                _logger.LogDebug(
                    "User {UserId} not in voice channel for play command in guild {GuildId}",
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

            // Check if bot needs to connect or switch channels
            await EnsureBotInVoiceChannelAsync(guildId, voiceChannel.Id);

            // Get audio settings to check if queueing is enabled and silent playback
            var settings = await _audioSettingsService.GetSettingsAsync(guildId);
            var queueEnabled = settings?.QueueEnabled ?? false;
            var silentPlayback = settings?.SilentPlayback ?? false;

            // Delegate to orchestration service
            var result = await _orchestrationService.PlaySoundAsync(
                guildId,
                sound.Id,
                userId,
                queueEnabled,
                filter);

            if (!result.Success)
            {
                _logger.LogError("Failed to play sound '{SoundName}': {ErrorMessage}", soundName, result.ErrorMessage);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Playback Failed")
                    .WithDescription(result.ErrorMessage ?? "An unknown error occurred.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                return;
            }

            // Get filter display text for embed
            var filterText = filter != AudioFilter.None && AudioFilters.Definitions.TryGetValue(filter, out var filterDef)
                ? $" (with {filterDef.Name} effect)"
                : string.Empty;

            // Determine response based on whether sound was queued or playing immediately
            if (result.WasQueued)
            {
                _logger.LogInformation(
                    "Sound '{SoundName}' queued for guild {GuildId} by user {UserId} at position {QueuePosition}, Filter: {Filter}",
                    soundName,
                    guildId,
                    userId,
                    result.QueuePosition,
                    filter);

                // Silent playback: acknowledge without visible response
                if (silentPlayback)
                {
                    await DeferAsync(ephemeral: true);
                    await DeleteOriginalResponseAsync();
                }
                else
                {
                    var queuedEmbed = new EmbedBuilder()
                        .WithTitle("Sound Queued")
                        .WithDescription($"Queued: **{sound.Name}**{filterText} (position: {result.QueuePosition})")
                        .WithColor(Color.Blue)
                        .WithCurrentTimestamp()
                        .Build();

                    await RespondAsync(embed: queuedEmbed, ephemeral: true);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Sound '{SoundName}' now playing for guild {GuildId} by user {UserId}, Filter: {Filter}",
                    soundName,
                    guildId,
                    userId,
                    filter);

                // Silent playback: acknowledge without visible response
                if (silentPlayback)
                {
                    await DeferAsync(ephemeral: true);
                    await DeleteOriginalResponseAsync();
                }
                else
                {
                    var playingEmbed = new EmbedBuilder()
                        .WithTitle("Now Playing")
                        .WithDescription($"Now playing: **{sound.Name}**{filterText}")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();

                    await RespondAsync(embed: playingEmbed, ephemeral: true);
                }
            }
        }
        catch (FileNotFoundException)
        {
            _logger.LogError(
                "Sound file not found for sound '{SoundName}' in guild {GuildId}",
                soundName,
                guildId);

            var fileErrorEmbed = new EmbedBuilder()
                .WithTitle("File Not Found")
                .WithDescription("Sound file not found. It may have been deleted.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: fileErrorEmbed, ephemeral: true);
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

            await RespondAsync(embed: permissionEmbed, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to play sound '{SoundName}' in guild {GuildId}",
                soundName,
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Playback Error")
                .WithDescription("An error occurred while trying to play the sound. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Lists all available sounds for the guild.
    /// </summary>
    [SlashCommand("sounds", "List all available sounds")]
    public async Task SoundsAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogInformation(
            "Sounds command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var sounds = await _soundService.GetAllByGuildAsync(guildId);

            if (sounds == null || !sounds.Any())
            {
                _logger.LogDebug(
                    "No sounds found for guild {GuildId}",
                    guildId);

                var emptyEmbed = new EmbedBuilder()
                    .WithTitle("Available Sounds")
                    .WithDescription("No sounds available. Sounds can be added via the admin panel.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: emptyEmbed, ephemeral: true);
                return;
            }

            _logger.LogDebug(
                "Found {Count} sounds for guild {GuildId}",
                sounds.Count(),
                guildId);

            var embed = new EmbedBuilder()
                .WithTitle("Available Sounds")
                .WithColor(Color.Blue)
                .WithFooter($"Found {sounds.Count()} sounds")
                .WithCurrentTimestamp();

            // Group sounds into columns for better display
            var soundList = sounds.Select(s => s.Name).OrderBy(n => n).ToList();

            // Split into multiple fields if there are many sounds
            const int soundsPerField = 15;
            var fieldNumber = 1;

            for (int i = 0; i < soundList.Count; i += soundsPerField)
            {
                var fieldSounds = soundList.Skip(i).Take(soundsPerField);
                var fieldValue = string.Join("\n", fieldSounds.Select(s => $"• {s}"));

                var fieldName = soundList.Count <= soundsPerField
                    ? "Sounds"
                    : $"Sounds (Part {fieldNumber})";

                embed.AddField(fieldName, fieldValue, inline: false);
                fieldNumber++;
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug(
                "Sounds list response sent for guild {GuildId}: {Count} sounds",
                guildId,
                sounds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve sounds for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while retrieving the sounds list. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Stops current playback and clears the queue.
    /// </summary>
    [SlashCommand("stop", "Stop playback and clear the queue")]
    [RequireAdmin]
    public async Task StopAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogInformation(
            "Stop command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            await _playbackService.StopAsync(guildId);

            _logger.LogInformation(
                "Playback stopped and queue cleared for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            var successEmbed = new EmbedBuilder()
                .WithTitle("Playback Stopped")
                .WithDescription("Playback stopped and queue cleared.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: successEmbed, ephemeral: true);

            _logger.LogDebug("Stop command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to stop playback for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while stopping playback. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Ensures the bot is connected to the specified voice channel.
    /// If already connected to a different channel, switches to the target channel.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="targetChannelId">Target voice channel ID.</param>
    private async Task EnsureBotInVoiceChannelAsync(ulong guildId, ulong targetChannelId)
    {
        var isConnected = _audioService.IsConnected(guildId);
        if (!isConnected)
        {
            _logger.LogDebug(
                "Bot not connected to voice in guild {GuildId}, connecting to channel {ChannelId}",
                guildId,
                targetChannelId);

            await _audioService.JoinChannelAsync(guildId, targetChannelId);
        }
        else
        {
            // Check if connected to a different channel
            var currentChannelId = _audioService.GetConnectedChannelId(guildId);
            if (currentChannelId.HasValue && currentChannelId.Value != targetChannelId)
            {
                _logger.LogDebug(
                    "Bot connected to different channel in guild {GuildId}, switching from {CurrentChannelId} to {NewChannelId}",
                    guildId,
                    currentChannelId.Value,
                    targetChannelId);

                await _audioService.LeaveChannelAsync(guildId);
                await _audioService.JoinChannelAsync(guildId, targetChannelId);
            }
        }
    }
}
