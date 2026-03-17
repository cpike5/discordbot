using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Preconditions;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Voice channel management commands for joining and leaving voice channels.
/// Allows users to connect the bot to their current voice channel or a specific voice channel,
/// and disconnect the bot from voice channels.
/// </summary>
[RequireGuildActive]
[RequireAudioEnabled]
[RateLimit(3, 10)]
public class VoiceModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly ILogger<VoiceModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModule"/> class.
    /// </summary>
    public VoiceModule(
        IAudioService audioService,
        ILogger<VoiceModule> logger)
    {
        _audioService = audioService;
        _logger = logger;
    }

    /// <summary>
    /// Joins the user's current voice channel.
    /// </summary>
    [SlashCommand("join", "Join your current voice channel")]
    [RequireVoiceChannel]
    public async Task JoinAsync()
    {
        var guildId = Context.Guild.Id;
        var guildUser = (SocketGuildUser)Context.User;
        var voiceChannel = guildUser.VoiceChannel;

        _logger.LogDebug(
            "Join command (no parameter) executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}) for channel {ChannelName} (ID: {ChannelId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId,
            voiceChannel?.Name,
            voiceChannel?.Id);

        try
        {
            // Check if already in this channel
            var currentChannelId = _audioService.GetConnectedChannelId(guildId);
            if (currentChannelId.HasValue && currentChannelId.Value == voiceChannel!.Id)
            {
                _logger.LogDebug(
                    "Bot already in channel {ChannelId} for guild {GuildId}",
                    voiceChannel.Id,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Confirmation("Already Connected", $"I'm already in {voiceChannel.Name}!"), ephemeral: true);
                return;
            }

            // Join the channel
            var audioClient = await _audioService.JoinChannelAsync(guildId, voiceChannel!.Id);

            if (audioClient == null)
            {
                _logger.LogWarning(
                    "Failed to join voice channel {ChannelId} in guild {GuildId} - channel or guild not found",
                    voiceChannel.Id,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Error("Error", "Failed to join the voice channel. The channel may no longer exist."), ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Joined voice channel {ChannelId} in guild {GuildId} via command from user {UserId}",
                voiceChannel.Id,
                guildId,
                Context.User.Id);

            await RespondAsync(embed: EmbedHelper.Success("Joined Voice Channel", $"Joined {voiceChannel.Name}"), ephemeral: true);

            _logger.LogDebug("Join command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to join voice channel {ChannelId} in guild {GuildId}",
                voiceChannel?.Id,
                guildId);

            await RespondAsync(embed: EmbedHelper.Error("Error", "I don't have permission to join that voice channel."), ephemeral: true);
        }
    }

    /// <summary>
    /// Joins a specified voice channel.
    /// </summary>
    /// <param name="channel">The voice channel to join.</param>
    [SlashCommand("join-channel", "Join a specific voice channel")]
    public async Task JoinChannelAsync(
        [Summary("channel", "The voice channel to join")]
        IVoiceChannel channel)
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Join-channel command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}) for channel {ChannelName} (ID: {ChannelId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId,
            channel.Name,
            channel.Id);

        try
        {
            // Check if already in this channel
            var currentChannelId = _audioService.GetConnectedChannelId(guildId);
            if (currentChannelId.HasValue && currentChannelId.Value == channel.Id)
            {
                _logger.LogDebug(
                    "Bot already in channel {ChannelId} for guild {GuildId}",
                    channel.Id,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Confirmation("Already Connected", $"I'm already in {channel.Name}!"), ephemeral: true);
                return;
            }

            // Join the channel
            var audioClient = await _audioService.JoinChannelAsync(guildId, channel.Id);

            if (audioClient == null)
            {
                _logger.LogWarning(
                    "Failed to join voice channel {ChannelId} in guild {GuildId} - channel or guild not found",
                    channel.Id,
                    guildId);

                await RespondAsync(embed: EmbedHelper.Error("Error", "Failed to join the voice channel. The channel may no longer exist."), ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Joined voice channel {ChannelId} in guild {GuildId} via command from user {UserId}",
                channel.Id,
                guildId,
                Context.User.Id);

            await RespondAsync(embed: EmbedHelper.Success("Joined Voice Channel", $"Joined {channel.Name}"), ephemeral: true);

            _logger.LogDebug("Join-channel command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to join voice channel {ChannelId} in guild {GuildId}",
                channel.Id,
                guildId);

            await RespondAsync(embed: EmbedHelper.Error("Error", "I don't have permission to join that voice channel."), ephemeral: true);
        }
    }

    /// <summary>
    /// Leaves the current voice channel.
    /// </summary>
    [SlashCommand("leave", "Leave the current voice channel")]
    public async Task LeaveAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Leave command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var disconnected = await _audioService.LeaveChannelAsync(guildId);

            if (!disconnected)
            {
                _logger.LogDebug(
                    "Leave command executed but bot not connected to voice in guild {GuildId}",
                    guildId);

                await RespondAsync(embed: EmbedHelper.Confirmation("Not Connected", "I'm not in a voice channel."), ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Left voice channel in guild {GuildId} via command from user {UserId}",
                guildId,
                Context.User.Id);

            await RespondAsync(embed: EmbedHelper.Success("Left Voice Channel", "Left the voice channel."), ephemeral: true);

            _logger.LogDebug("Leave command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to leave voice channel in guild {GuildId}",
                guildId);

            await RespondAsync(embed: EmbedHelper.Error("Error", "An error occurred while leaving the voice channel. Please try again later."), ephemeral: true);
        }
    }
}
