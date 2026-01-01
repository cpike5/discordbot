using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for tracking Discord gateway connection state changes and calculating uptime metrics.
/// Thread-safe singleton service that maintains connection event history.
/// </summary>
public class ConnectionStateService : IConnectionStateService
{
    private readonly ILogger<ConnectionStateService> _logger;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    private readonly List<ConnectionEvent> _connectionEvents = new();
    private DateTime? _lastConnectedTime;
    private DateTime? _lastDisconnectedTime;
    private GatewayConnectionState _currentState = GatewayConnectionState.Disconnected;
    private long _totalUptimeMs;
    private readonly DateTime _serviceStartTime;

    public ConnectionStateService(
        ILogger<ConnectionStateService> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _serviceStartTime = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public void RecordConnected()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // If we were previously connected, calculate uptime of that session
            if (_lastConnectedTime.HasValue && _currentState == GatewayConnectionState.Connected)
            {
                var sessionDuration = (now - _lastConnectedTime.Value).TotalMilliseconds;
                _totalUptimeMs += (long)sessionDuration;
            }

            _lastConnectedTime = now;
            _currentState = GatewayConnectionState.Connected;

            var evt = new ConnectionEvent
            {
                EventType = "Connected",
                Timestamp = now,
                Reason = null,
                Details = null
            };

            _connectionEvents.Add(evt);
            CleanupOldEvents();

            _logger.LogInformation("Gateway connected at {Timestamp}", now);
        }
    }

    /// <inheritdoc/>
    public void RecordDisconnected(Exception? exception)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Calculate uptime for the session that just ended
            if (_lastConnectedTime.HasValue && _currentState == GatewayConnectionState.Connected)
            {
                var sessionDuration = (now - _lastConnectedTime.Value).TotalMilliseconds;
                _totalUptimeMs += (long)sessionDuration;
            }

            _lastDisconnectedTime = now;
            _currentState = GatewayConnectionState.Disconnected;

            var evt = new ConnectionEvent
            {
                EventType = "Disconnected",
                Timestamp = now,
                Reason = exception?.Message,
                Details = exception?.GetType().Name
            };

            _connectionEvents.Add(evt);
            CleanupOldEvents();

            if (exception != null)
            {
                _logger.LogWarning(exception, "Gateway disconnected at {Timestamp}", now);
            }
            else
            {
                _logger.LogInformation("Gateway disconnected at {Timestamp}", now);
            }
        }
    }

    /// <inheritdoc/>
    public GatewayConnectionState GetCurrentState()
    {
        lock (_lock)
        {
            return _currentState;
        }
    }

    /// <inheritdoc/>
    public DateTime? GetLastConnectedTime()
    {
        lock (_lock)
        {
            return _lastConnectedTime;
        }
    }

    /// <inheritdoc/>
    public DateTime? GetLastDisconnectedTime()
    {
        lock (_lock)
        {
            return _lastDisconnectedTime;
        }
    }

    /// <inheritdoc/>
    public TimeSpan GetCurrentSessionDuration()
    {
        lock (_lock)
        {
            if (_currentState == GatewayConnectionState.Connected && _lastConnectedTime.HasValue)
            {
                return DateTime.UtcNow - _lastConnectedTime.Value;
            }

            return TimeSpan.Zero;
        }
    }

    /// <inheritdoc/>
    public double GetUptimePercentage(TimeSpan period)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var periodStart = now - period;

            // Get total uptime in the period
            long uptimeInPeriod = 0;

            // Add current session uptime if connected
            if (_currentState == GatewayConnectionState.Connected && _lastConnectedTime.HasValue)
            {
                var sessionStart = _lastConnectedTime.Value > periodStart ? _lastConnectedTime.Value : periodStart;
                uptimeInPeriod += (long)(now - sessionStart).TotalMilliseconds;
            }

            // Add completed session uptimes within the period
            for (int i = 0; i < _connectionEvents.Count - 1; i++)
            {
                var current = _connectionEvents[i];
                var next = _connectionEvents[i + 1];

                // If this is a connected event followed by a disconnected event
                if (current.EventType == "Connected" && next.EventType == "Disconnected")
                {
                    if (next.Timestamp >= periodStart && current.Timestamp <= now)
                    {
                        var sessionStart = current.Timestamp > periodStart ? current.Timestamp : periodStart;
                        var sessionEnd = next.Timestamp < now ? next.Timestamp : now;
                        uptimeInPeriod += (long)(sessionEnd - sessionStart).TotalMilliseconds;
                    }
                }
            }

            var periodMs = period.TotalMilliseconds;
            return periodMs > 0 ? (uptimeInPeriod / periodMs) * 100.0 : 0.0;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConnectionEventDto> GetConnectionEvents(int days = 7)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            return _connectionEvents
                .Where(e => e.Timestamp >= cutoff)
                .Select(e => new ConnectionEventDto
                {
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    Reason = e.Reason,
                    Details = e.Details
                })
                .ToList();
        }
    }

    /// <inheritdoc/>
    public ConnectionStatsDto GetConnectionStats(int days = 7)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var eventsInPeriod = _connectionEvents.Where(e => e.Timestamp >= cutoff).ToList();

            var totalEvents = eventsInPeriod.Count;
            var reconnectionCount = eventsInPeriod.Count(e => e.EventType == "Connected") - 1; // First connect doesn't count as reconnect
            if (reconnectionCount < 0) reconnectionCount = 0;

            // Calculate average session duration
            var sessionDurations = new List<TimeSpan>();
            for (int i = 0; i < eventsInPeriod.Count - 1; i++)
            {
                if (eventsInPeriod[i].EventType == "Connected" && eventsInPeriod[i + 1].EventType == "Disconnected")
                {
                    sessionDurations.Add(eventsInPeriod[i + 1].Timestamp - eventsInPeriod[i].Timestamp);
                }
            }

            var avgSessionDuration = sessionDurations.Any()
                ? TimeSpan.FromMilliseconds(sessionDurations.Average(ts => ts.TotalMilliseconds))
                : TimeSpan.Zero;

            var uptimePercentage = GetUptimePercentage(TimeSpan.FromDays(days));

            return new ConnectionStatsDto
            {
                TotalEvents = totalEvents,
                ReconnectionCount = reconnectionCount,
                AverageSessionDuration = avgSessionDuration,
                UptimePercentage = uptimePercentage
            };
        }
    }

    /// <summary>
    /// Removes connection events older than the configured retention period.
    /// </summary>
    private void CleanupOldEvents()
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.ConnectionEventRetentionDays);
        _connectionEvents.RemoveAll(e => e.Timestamp < cutoff);
    }

    /// <summary>
    /// Internal class for storing connection events.
    /// </summary>
    private class ConnectionEvent
    {
        public required string EventType { get; init; }
        public required DateTime Timestamp { get; init; }
        public string? Reason { get; init; }
        public string? Details { get; init; }
    }
}
