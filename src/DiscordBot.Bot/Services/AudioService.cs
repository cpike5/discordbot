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
    private readonly ConcurrentDictionary<ulong, AudioOutStream> _pcmStreams = new();

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
                // Only trust the tracked connection if Discord agrees the bot is still in that channel.
                // A tracked connection whose gateway voice state has gone (kicked, channel deleted,
                // session reset) must be torn down and re-established rather than reused.
                var actualChannelId = GetActualBotVoiceChannelId(guildId, out var isActualKnown);
                var trackedChannelIsLive = !isActualKnown || actualChannelId == voiceChannelId;

                if (existingConnection.ChannelId == voiceChannelId && trackedChannelIsLive)
                {
                    _logger.LogInformation("Already connected to voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);

                    // Add voice channel attributes
                    activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, voiceChannelId.ToString());

                    BotActivitySource.SetSuccess(activity);
                    return existingConnection.AudioClient;
                }

                // Connected to a different channel (or the tracked connection is dead) - disconnect first
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

            // Add voice channel attributes
            activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, voiceChannelId.ToString());
            activity?.SetTag(TracingConstants.Attributes.VoiceChannelName, voiceChannel.Name);

            // Connect to voice channel
            var audioClient = await voiceChannel.ConnectAsync();

            // Store connection info
            var now = DateTime.UtcNow;
            var connectionInfo = new VoiceConnectionInfo(audioClient, voiceChannelId, now, now);
            _connections[guildId] = connectionInfo;

            // Add connection timestamp
            activity?.SetTag(TracingConstants.Attributes.VoiceConnectedAt, now.ToString("O"));

            _logger.LogInformation("Successfully joined voice channel {ChannelId} ({ChannelName}) in guild {GuildId}",
                voiceChannelId, voiceChannel.Name, guildId);

            // Get member count (excluding bots)
            var memberCount = voiceChannel.ConnectedUsers.Count(u => !u.IsBot);

            // Broadcast AudioConnected event to subscribed clients
            _ = _audioNotifier.NotifyAudioConnectedAsync(guildId, voiceChannelId, voiceChannel.Name, memberCount, cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return audioClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);
            BotActivitySource.RecordException(activity, ex);

            // Discord.NET clears the gateway voice state on its own failure paths, but a failure
            // after the join request was sent can still leave the bot visible in the channel.
            // Nothing is tracked at this point, so make sure Discord agrees.
            await ClearPhantomVoicePresenceAsync(guildId);
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
                // Nothing tracked, but Discord may still show the bot in a channel (state drift).
                // Honour the leave request by clearing that presence so the user is not stuck.
                var phantomChannelId = GetActualBotVoiceChannelId(guildId, out var isKnown);
                if (isKnown && phantomChannelId.HasValue)
                {
                    _logger.LogWarning(
                        "Leave requested for guild {GuildId}: no tracked connection but Discord shows the bot in voice channel {ChannelId}. Clearing presence",
                        guildId, phantomChannelId);

                    await ClearPhantomVoicePresenceAsync(guildId);
                    _ = _audioNotifier.NotifyAudioDisconnectedAsync(guildId, "User requested disconnect", cancellationToken);

                    BotActivitySource.SetSuccess(activity);
                    return true;
                }

                _logger.LogDebug("Not connected to any voice channel in guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            _logger.LogInformation("Leaving voice channel {ChannelId} in guild {GuildId}", connection.ChannelId, guildId);

            // Add voice channel attributes
            activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, connection.ChannelId.ToString());

            // Calculate connection duration
            var connectionDuration = DateTime.UtcNow - connection.ConnectedAt;
            activity?.SetTag(TracingConstants.Attributes.VoiceConnectionDurationSeconds, connectionDuration.TotalSeconds);

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

    /// <inheritdoc/>
    public AudioOutStream? GetOrCreatePcmStream(ulong guildId)
    {
        // Return existing stream if we have one
        if (_pcmStreams.TryGetValue(guildId, out var existingStream))
        {
            return existingStream;
        }

        // Get audio client
        if (!_connections.TryGetValue(guildId, out var connection))
        {
            _logger.LogWarning("Cannot create PCM stream - not connected to voice in guild {GuildId}", guildId);
            return null;
        }

        // Create new PCM stream and cache it
        var pcmStream = connection.AudioClient.CreatePCMStream(AudioApplication.Voice);
        _pcmStreams[guildId] = pcmStream;
        _logger.LogDebug("Created new PCM stream for guild {GuildId}", guildId);

        return pcmStream;
    }

    /// <inheritdoc/>
    public async Task ReconcileBotVoiceStateAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await guildLock.WaitAsync(cancellationToken);

        try
        {
            var actualChannelId = GetActualBotVoiceChannelId(guildId, out var isKnown);
            if (!isKnown)
            {
                _logger.LogDebug("Cannot reconcile voice state for guild {GuildId}: guild or bot member not cached", guildId);
                return;
            }

            _connections.TryGetValue(guildId, out var tracked);
            var trackedChannelId = tracked?.ChannelId;

            if (trackedChannelId == actualChannelId)
            {
                if (actualChannelId is null)
                {
                    // Discord and the tracked state agree the bot is not in voice. Broadcast anyway:
                    // this runs on the bot's own "left voice" event, and any client that missed the
                    // AudioDisconnected sent by the leave itself would otherwise keep showing the bot
                    // as connected until a page refresh. The event is idempotent for the UI.
                    _ = _audioNotifier.NotifyAudioDisconnectedAsync(guildId, "Bot is not in voice", cancellationToken);
                }

                return;
            }

            if (trackedChannelId.HasValue && actualChannelId is null)
            {
                // Discord says we left (kicked, moved out, channel deleted, session reset) but we still track a connection.
                _logger.LogWarning(
                    "Voice state drift in guild {GuildId}: tracked channel {ChannelId} but Discord shows the bot is not in voice. Clearing tracked connection",
                    guildId, trackedChannelId);

                await DisconnectInternalAsync(guildId);
                _ = _audioNotifier.NotifyAudioDisconnectedAsync(guildId, "Disconnected from voice by Discord", cancellationToken);
                return;
            }

            if (trackedChannelId.HasValue && actualChannelId.HasValue)
            {
                // Bot was moved to a different channel by a moderator. Keep the connection, retarget the tracking.
                _logger.LogInformation(
                    "Bot was moved from voice channel {OldChannelId} to {NewChannelId} in guild {GuildId}; updating tracked connection",
                    trackedChannelId, actualChannelId, guildId);

                // The cached PCM stream may be bound to the old voice server; drop it so it is recreated on next playback.
                await DisposePcmStreamAsync(guildId);

                _connections[guildId] = tracked! with { ChannelId = actualChannelId.Value, LastActivity = DateTime.UtcNow };

                var guild = _client.GetGuild(guildId);
                var channel = guild?.GetVoiceChannel(actualChannelId.Value);
                if (channel != null)
                {
                    var memberCount = channel.ConnectedUsers.Count(u => !u.IsBot);
                    _ = _audioNotifier.NotifyAudioConnectedAsync(guildId, channel.Id, channel.Name, memberCount, cancellationToken);
                }
                return;
            }

            // trackedChannelId is null, actualChannelId has a value: Discord shows the bot in voice but nothing is tracked.
            _logger.LogWarning(
                "Voice state drift in guild {GuildId}: Discord shows the bot in voice channel {ChannelId} but no connection is tracked. Leaving the channel",
                guildId, actualChannelId);

            await ClearPhantomVoicePresenceAsync(guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling voice state for guild {GuildId}", guildId);
        }
        finally
        {
            guildLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ReconcileAllBotVoiceStatesAsync(CancellationToken cancellationToken = default)
    {
        // Tracked connections that Discord no longer knows about (typically after a gateway session reset)
        var guildIds = _connections.Keys.ToHashSet();

        // Guilds where Discord shows the bot in voice but nothing is tracked
        foreach (var guild in _client.Guilds)
        {
            if (guild.CurrentUser?.VoiceChannel != null)
            {
                guildIds.Add(guild.Id);
            }
        }

        foreach (var guildId in guildIds)
        {
            await ReconcileBotVoiceStateAsync(guildId, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the voice channel Discord currently reports the bot as being in for the guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="isKnown">False when the guild or the bot's member object is not cached, in which case the result is meaningless.</param>
    /// <returns>The channel ID, or null when the bot is not in voice (or the state is unknown).</returns>
    private ulong? GetActualBotVoiceChannelId(ulong guildId, out bool isKnown)
    {
        var currentUser = _client.GetGuild(guildId)?.CurrentUser;
        isKnown = currentUser != null;
        return currentUser?.VoiceChannel?.Id;
    }

    /// <summary>
    /// Sends a voice state update that removes the bot from voice in the guild when Discord still
    /// shows it in a channel. Safe to call when the bot is not in voice. Assumes the guild lock is held.
    /// </summary>
    private async Task ClearPhantomVoicePresenceAsync(ulong guildId)
    {
        var guild = _client.GetGuild(guildId);
        var channel = guild?.CurrentUser?.VoiceChannel;
        if (channel == null)
        {
            return;
        }

        try
        {
            // IVoiceChannel.DisconnectAsync goes through the guild: it stops Discord.NET's audio client
            // and sends the gateway voice state update that actually removes the bot from the channel.
            await channel.DisconnectAsync();
            _logger.LogInformation("Cleared phantom voice presence in channel {ChannelId} for guild {GuildId}", channel.Id, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing phantom voice presence in channel {ChannelId} for guild {GuildId}", channel.Id, guildId);
        }
    }

    /// <summary>
    /// Flushes and disposes the cached PCM stream for the guild, if any.
    /// </summary>
    private async Task DisposePcmStreamAsync(ulong guildId)
    {
        if (_pcmStreams.TryRemove(guildId, out var pcmStream))
        {
            try
            {
                await pcmStream.FlushAsync();
                pcmStream.Dispose();
                _logger.LogDebug("Disposed PCM stream for guild {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing PCM stream for guild {GuildId}", guildId);
            }
        }
    }

    /// <summary>
    /// Internal disconnect method that assumes the guild lock is already held.
    /// Removes the tracked connection and leaves the voice channel through Discord.NET's guild-level
    /// disconnect, which is what sends the gateway voice state update. Calling
    /// <see cref="IAudioClient.StopAsync"/> directly only tears down the voice websocket and leaves
    /// the bot visibly sitting in the channel from Discord's point of view.
    /// </summary>
    /// <param name="guildId">The guild ID to disconnect from.</param>
    private async Task DisconnectInternalAsync(ulong guildId)
    {
        // Clean up PCM stream first
        await DisposePcmStreamAsync(guildId);

        if (!_connections.TryRemove(guildId, out var connection))
        {
            return;
        }

        var guild = _client.GetGuild(guildId);

        // Any voice channel in the guild works: SocketVoiceChannel.DisconnectAsync delegates to the guild,
        // which stops and disposes its audio client and sends the "left voice" state update.
        var channel = guild?.GetVoiceChannel(connection.ChannelId)
            ?? guild?.CurrentUser?.VoiceChannel
            ?? guild?.VoiceChannels.FirstOrDefault();

        if (channel != null)
        {
            try
            {
                await channel.DisconnectAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from voice in guild {GuildId}; falling back to stopping the audio client", guildId);
            }
        }
        else
        {
            _logger.LogWarning("Guild {GuildId} or its voice channels are not cached; stopping the audio client directly", guildId);
        }

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
