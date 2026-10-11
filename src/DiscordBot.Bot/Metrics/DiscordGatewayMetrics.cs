using System.Diagnostics;
using System.Diagnostics.Metrics;
using Discord;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Metrics for the bot's connection to Discord: gateway state and latency, gateway event volume,
/// Discord.NET log events, and REST rate limits reported by Discord.NET.
/// </summary>
/// <remarks>
/// Fed by <see cref="Services.DiscordGatewayMetricsService"/>, which subscribes to the socket
/// client's events, and by the client's <c>DefaultRatelimitCallback</c> (see
/// <c>DiscordServiceExtensions.AddDiscordBot</c>).
/// </remarks>
public sealed class DiscordGatewayMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Discord";

    private readonly Meter _meter;
    private readonly Counter<long> _connectionEvents;
    private readonly Counter<long> _gatewayEvents;
    private readonly Counter<long> _logEvents;
    private readonly Counter<long> _restResponses;
    private readonly Counter<long> _restRateLimits;

    private long _latencyMs;
    private long _connected;

    public DiscordGatewayMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _connectionEvents = _meter.CreateCounter<long>(
            name: "discordbot.gateway.connection.events",
            unit: "{events}",
            description: "Gateway connect and disconnect events");

        _gatewayEvents = _meter.CreateCounter<long>(
            name: "discordbot.gateway.events",
            unit: "{events}",
            description: "Gateway events received, by event type");

        _logEvents = _meter.CreateCounter<long>(
            name: "discordbot.discord.log.events",
            unit: "{events}",
            description: "Discord.NET log events, by severity and source");

        _restResponses = _meter.CreateCounter<long>(
            name: "discordbot.discord.rest.responses",
            unit: "{responses}",
            description: "Discord REST responses reported by Discord.NET's rate-limit callback");

        _restRateLimits = _meter.CreateCounter<long>(
            name: "discordbot.discord.rest.ratelimits",
            unit: "{events}",
            description: "Discord REST rate limits: 429s (kind=hit) and exhausted buckets (kind=exhausted)");

        _meter.CreateObservableGauge(
            name: "discordbot.gateway.latency",
            observeValue: () => Interlocked.Read(ref _latencyMs),
            unit: "ms",
            description: "Last gateway heartbeat round-trip latency");

        _meter.CreateObservableGauge(
            name: "discordbot.gateway.connected",
            observeValue: () => Interlocked.Read(ref _connected),
            unit: "{state}",
            description: "1 while the gateway is connected, 0 otherwise");
    }

    /// <summary>Records a gateway connection and marks the gateway connected.</summary>
    public void RecordConnected()
    {
        Interlocked.Exchange(ref _connected, 1);
        _connectionEvents.Add(1, new KeyValuePair<string, object?>("event", "connected"));
    }

    /// <summary>Records a gateway disconnection and marks the gateway disconnected.</summary>
    /// <param name="exception">The exception Discord.NET reported, if any.</param>
    public void RecordDisconnected(Exception? exception)
    {
        Interlocked.Exchange(ref _connected, 0);
        _connectionEvents.Add(1, new TagList
        {
            { "event", "disconnected" },
            { "reason", exception?.GetType().Name ?? "none" }
        });
    }

    /// <summary>Updates the gateway latency gauge.</summary>
    public void UpdateLatency(int latencyMs) => Interlocked.Exchange(ref _latencyMs, latencyMs);

    /// <summary>Records one gateway event of the given type.</summary>
    /// <param name="eventType">A fixed event name such as "message_received".</param>
    public void RecordGatewayEvent(string eventType)
        => _gatewayEvents.Add(1, new KeyValuePair<string, object?>("event", eventType));

    /// <summary>Records one Discord.NET log event.</summary>
    public void RecordLogEvent(LogSeverity severity, string? source)
    {
        _logEvents.Add(1, new TagList
        {
            { "severity", severity.ToString().ToLowerInvariant() },
            { "source", string.IsNullOrWhiteSpace(source) ? "unknown" : source }
        });
    }

    /// <summary>
    /// Records the rate-limit information Discord.NET reports for a REST response.
    /// </summary>
    public void RecordRateLimitInfo(IRateLimitInfo info)
    {
        var route = NormalizeRoute(info.Endpoint);
        _restResponses.Add(1, new KeyValuePair<string, object?>("route", route));

        if (info.RetryAfter.HasValue)
        {
            _restRateLimits.Add(1, new TagList
            {
                { "route", route },
                { "kind", "hit" },
                { "scope", info.IsGlobal ? "global" : "bucket" }
            });
        }
        else if (info.Remaining == 0)
        {
            _restRateLimits.Add(1, new TagList
            {
                { "route", route },
                { "kind", "exhausted" },
                { "scope", info.IsGlobal ? "global" : "bucket" }
            });
        }
    }

    /// <summary>
    /// Collapses the variable parts of a Discord REST endpoint (snowflakes, interaction and
    /// webhook tokens, query strings) so the route is safe to use as a metric label.
    /// </summary>
    /// <param name="endpoint">An endpoint such as <c>channels/123456789012345678/messages</c>.</param>
    /// <returns>The normalized route, e.g. <c>channels/{id}/messages</c>.</returns>
    public static string NormalizeRoute(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "unknown";
        }

        var path = endpoint;
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        var segments = path.Trim('/').Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && segment.All(char.IsAsciiDigit))
            {
                segments[i] = "{id}";
            }
            else if (segment.Length >= 32)
            {
                segments[i] = "{token}";
            }
            else if (segment.Contains('%') || segment.Contains(':'))
            {
                // URL-encoded emoji (reactions) and name:id pairs.
                segments[i] = "{value}";
            }
        }

        return string.Join('/', segments);
    }

    public void Dispose() => _meter.Dispose();
}
