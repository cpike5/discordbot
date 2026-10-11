using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Metrics;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Subscribes <see cref="DiscordGatewayMetrics"/> to the socket client's events: connection state,
/// heartbeat latency, Discord.NET log events, and a count of the gateway events the bot handles.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="BotHostedService"/> so the metrics wiring does not widen that class's
/// constructor. Handlers only touch counters, so they never block the gateway thread.
/// </remarks>
public sealed class DiscordGatewayMetricsService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordGatewayMetrics _metrics;

    public DiscordGatewayMetricsService(DiscordSocketClient client, DiscordGatewayMetrics metrics)
    {
        _client = client;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Connected += OnConnected;
        _client.Disconnected += OnDisconnected;
        _client.LatencyUpdated += OnLatencyUpdated;
        _client.Log += OnLog;
        _client.MessageReceived += OnMessageReceived;
        _client.InteractionCreated += OnInteractionCreated;
        _client.UserJoined += OnUserJoined;
        _client.UserLeft += OnUserLeft;
        _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
        _client.JoinedGuild += OnJoinedGuild;
        _client.LeftGuild += OnLeftGuild;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Connected -= OnConnected;
        _client.Disconnected -= OnDisconnected;
        _client.LatencyUpdated -= OnLatencyUpdated;
        _client.Log -= OnLog;
        _client.MessageReceived -= OnMessageReceived;
        _client.InteractionCreated -= OnInteractionCreated;
        _client.UserJoined -= OnUserJoined;
        _client.UserLeft -= OnUserLeft;
        _client.UserVoiceStateUpdated -= OnVoiceStateUpdated;
        _client.JoinedGuild -= OnJoinedGuild;
        _client.LeftGuild -= OnLeftGuild;
        return Task.CompletedTask;
    }

    private Task OnConnected()
    {
        _metrics.RecordConnected();
        _metrics.UpdateLatency(_client.Latency);
        return Task.CompletedTask;
    }

    private Task OnDisconnected(Exception? exception)
    {
        _metrics.RecordDisconnected(exception);
        return Task.CompletedTask;
    }

    private Task OnLatencyUpdated(int oldLatency, int newLatency)
    {
        _metrics.UpdateLatency(newLatency);
        return Task.CompletedTask;
    }

    private Task OnLog(LogMessage message)
    {
        _metrics.RecordLogEvent(message.Severity, message.Source);
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(SocketMessage message) => Count("message_received");

    private Task OnInteractionCreated(SocketInteraction interaction) => Count("interaction_created");

    private Task OnUserJoined(SocketGuildUser user) => Count("user_joined");

    private Task OnUserLeft(SocketGuild guild, SocketUser user) => Count("user_left");

    private Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        => Count("voice_state_updated");

    private Task OnJoinedGuild(SocketGuild guild) => Count("joined_guild");

    private Task OnLeftGuild(SocketGuild guild) => Count("left_guild");

    private Task Count(string eventType)
    {
        _metrics.RecordGatewayEvent(eventType);
        return Task.CompletedTask;
    }
}
