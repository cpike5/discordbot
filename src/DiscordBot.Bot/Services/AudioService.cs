using System.Collections.Concurrent;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for voice channel connection management and audio playback.
/// Maintains thread-safe connection state using per-guild locks.
/// </summary>
public class AudioService : IAudioService
{
    private readonly DiscordSocketClient _client;
    private readonly IAudioNotifier _audioNotifier;
    private readonly ILogger<AudioService> _logger;
    private readonly ConcurrentDictionary<ulong, VoiceConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="audioNotifier">The audio notifier for SignalR broadcasts.</param>
    /// <param name="logger">The logger.</param>
    public AudioService(
        DiscordSocketClient client,
        IAudioNotifier audioNotifier,
        ILogger<AudioService> logger)
    {
        _client = client;
        _audioNotifier = audioNotifier;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IAudioClient?> JoinChannelAsync(ulong guildId, ulong voiceChannelId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audio",
            "join_channel",
            guildId: guildId);

        var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await guildLock.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Attempting to join voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);

            // Check if already connected to the same channel
            if (_connections.TryGetValue(guildId, out var existingConnection))
            {
                if (existingConnection.ChannelId == voiceChannelId)
                {
                    _logger.LogInformation("Already connected to voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);
                    BotActivitySource.SetSuccess(activity);
                    return existingConnection.AudioClient;
                }

                // Connected to different channel - disconnect first
                _logger.LogInformation("Disconnecting from voice channel {OldChannelId} before joining {NewChannelId} in guild {GuildId}",
                    existingConnection.ChannelId, voiceChannelId, guildId);
                await DisconnectInternalAsync(guildId);
            }

            // Get guild and voice channel
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found, cannot join voice channel", guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var voiceChannel = guild.GetVoiceChannel(voiceChannelId);
            if (voiceChannel == null)
            {
                _logger.LogWarning("Voice channel {ChannelId} not found in guild {GuildId}", voiceChannelId, guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            // Connect to voice channel
            var audioClient = await voiceChannel.ConnectAsync();

            // Store connection info
            var now = DateTime.UtcNow;
            var connectionInfo = new VoiceConnectionInfo(audioClient, voiceChannelId, now, now);
            _connections[guildId] = connectionInfo;

            _logger.LogInformation("Successfully joined voice channel {ChannelId} ({ChannelName}) in guild {GuildId}",
                voiceChannelId, voiceChannel.Name, guildId);

            // Broadcast AudioConnected event to subscribed clients
            _ = _audioNotifier.NotifyAudioConnectedAsync(guildId, voiceChannelId, voiceChannel.Name, cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return audioClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
        finally
        {
            guildLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> LeaveChannelAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "audio",
            "leave_channel",
            guildId: guildId);

        var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await guildLock.WaitAsync(cancellationToken);

        try
        {
            if (!_connections.TryGetValue(guildId, out var connection))
            {
                _logger.LogDebug("Not connected to any voice channel in guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            _logger.LogInformation("Leaving voice channel {ChannelId} in guild {GuildId}", connection.ChannelId, guildId);

            await DisconnectInternalAsync(guildId);

            _logger.LogInformation("Successfully left voice channel in guild {GuildId}", guildId);

            // Broadcast AudioDisconnected event to subscribed clients
            _ = _audioNotifier.NotifyAudioDisconnectedAsync(guildId, "User requested disconnect", cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving voice channel in guild {GuildId}", guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
        finally
        {
            guildLock.Release();
        }
    }

    /// <inheritdoc/>
    public IAudioClient? GetAudioClient(ulong guildId)
    {
        return _connections.TryGetValue(guildId, out var connection) ? connection.AudioClient : null;
    }

    /// <inheritdoc/>
    public bool IsConnected(ulong guildId)
    {
        return _connections.ContainsKey(guildId);
    }

    /// <inheritdoc/>
    public ulong? GetConnectedChannelId(ulong guildId)
    {
        return _connections.TryGetValue(guildId, out var connection) ? connection.ChannelId : null;
    }

    /// <inheritdoc/>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from all voice channels ({Count} guilds)", _connections.Count);

        var disconnectTasks = _connections.Keys.Select(async guildId =>
        {
            try
            {
                await LeaveChannelAsync(guildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from voice channel in guild {GuildId} during shutdown", guildId);
            }
        });

        await Task.WhenAll(disconnectTasks);

        _logger.LogInformation("Disconnected from all voice channels");
    }

    /// <inheritdoc/>
    public void UpdateLastActivity(ulong guildId)
    {
        if (_connections.TryGetValue(guildId, out var connection))
        {
            var updatedConnection = connection with { LastActivity = DateTime.UtcNow };
            _connections[guildId] = updatedConnection;
            _logger.LogTrace("Updated last activity for voice connection in guild {GuildId}", guildId);
        }
    }

    /// <summary>
    /// Internal disconnect method that assumes the guild lock is already held.
    /// Stops and disposes the audio client and removes the connection from tracking.
    /// </summary>
    /// <param name="guildId">The guild ID to disconnect from.</param>
    private async Task DisconnectInternalAsync(ulong guildId)
    {
        if (_connections.TryRemove(guildId, out var connection))
        {
            try
            {
                await connection.AudioClient.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio client for guild {GuildId}", guildId);
            }

            try
            {
                connection.AudioClient.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing audio client for guild {GuildId}", guildId);
            }
        }
    }

    /// <summary>
    /// Internal method to get all active connections (for use by background service).
    /// Returns a snapshot of current connections.
    /// </summary>
    /// <returns>A collection of guild IDs and their connection info.</returns>
    internal IReadOnlyDictionary<ulong, VoiceConnectionInfo> GetActiveConnections()
    {
        return new Dictionary<ulong, VoiceConnectionInfo>(_connections);
    }

    /// <summary>
    /// Represents information about an active voice channel connection.
    /// </summary>
    /// <param name="AudioClient">The Discord audio client for the connection.</param>
    /// <param name="ChannelId">The voice channel ID the bot is connected to.</param>
    /// <param name="ConnectedAt">The UTC timestamp when the connection was established.</param>
    /// <param name="LastActivity">The UTC timestamp of the last audio activity (playback).</param>
    internal record VoiceConnectionInfo(
        IAudioClient AudioClient,
        ulong ChannelId,
        DateTime ConnectedAt,
        DateTime LastActivity);
}
