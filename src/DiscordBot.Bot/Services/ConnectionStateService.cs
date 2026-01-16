using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for tracking Discord gateway connection state changes and calculating uptime metrics.
/// Thread-safe singleton service that persists connection events to the database.
/// </summary>
public class ConnectionStateService : IConnectionStateService
{
    private readonly ILogger<ConnectionStateService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    // In-memory state for fast access to current session info
    private DateTime? _lastConnectedTime;
    private DateTime? _lastDisconnectedTime;
    private GatewayConnectionState _currentState = GatewayConnectionState.Disconnected;
    private bool _initialized;

    public ConnectionStateService(
        ILogger<ConnectionStateService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    /// <summary>
    /// Initializes the service by checking for ungraceful shutdowns.
    /// Called on first connection event.
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

            var lastEvent = await repository.GetLastEventAsync();

            if (lastEvent != null)
            {
                _logger.LogInformation(
                    "Found last connection event: {EventType} at {Timestamp}",
                    lastEvent.EventType, lastEvent.Timestamp);

                // If last event was "Connected", the process crashed without recording a disconnect
                if (lastEvent.EventType == "Connected")
                {
                    _logger.LogWarning(
                        "Detected ungraceful shutdown - last event was Connected at {Timestamp}. Recording implicit disconnect.",
                        lastEvent.Timestamp);

                    // Record an implicit disconnect slightly after the last connected event
                    await repository.AddEventAsync(
                        "Disconnected",
                        lastEvent.Timestamp.AddSeconds(1),
                        reason: "Implicit - process terminated unexpectedly",
                        details: "UngracefulShutdown");
                }
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ConnectionStateService");
            // Still mark as initialized to prevent repeated failures
            _initialized = true;
        }
    }

    /// <inheritdoc/>
    public void RecordConnected()
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            _lastConnectedTime = now;
            _currentState = GatewayConnectionState.Connected;
        }

        // Fire-and-forget persistence to avoid blocking the bot
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeAsync();

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

                await repository.AddEventAsync("Connected", now);

                _logger.LogInformation("Gateway connected at {Timestamp}", now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist Connected event");
            }
        });
    }

    /// <inheritdoc/>
    public void RecordDisconnected(Exception? exception)
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            _lastDisconnectedTime = now;
            _currentState = GatewayConnectionState.Disconnected;
        }

        // Fire-and-forget persistence to avoid blocking the bot
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeAsync();

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

                await repository.AddEventAsync(
                    "Disconnected",
                    now,
                    reason: exception?.Message,
                    details: exception?.GetType().Name);

                if (exception != null)
                {
                    _logger.LogWarning(exception, "Gateway disconnected at {Timestamp}", now);
                }
                else
                {
                    _logger.LogInformation("Gateway disconnected at {Timestamp}", now);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist Disconnected event");
            }
        });
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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

            var now = DateTime.UtcNow;
            var periodStart = now - period;

            // Get events from database
            var events = repository.GetEventsSinceAsync(periodStart).GetAwaiter().GetResult();

            return CalculateUptimeFromEvents(events, periodStart, now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate uptime percentage");
            return 0.0;
        }
    }

    /// <summary>
    /// Calculates uptime percentage from a list of connection events.
    /// </summary>
    private double CalculateUptimeFromEvents(
        IReadOnlyList<Core.Entities.ConnectionEvent> events,
        DateTime periodStart,
        DateTime now)
    {
        if (events.Count == 0)
        {
            // No events in period - check current state
            lock (_lock)
            {
                if (_currentState == GatewayConnectionState.Connected && _lastConnectedTime.HasValue)
                {
                    // Currently connected, but no events in period means connected before period start
                    return 100.0;
                }
            }
            return 0.0;
        }

        long uptimeMs = 0;
        var eventsList = events.OrderBy(e => e.Timestamp).ToList();

        // Handle case where first event in period is a disconnect (was connected before period start)
        if (eventsList[0].EventType == "Disconnected")
        {
            // Connected from period start until first disconnect
            uptimeMs += (long)(eventsList[0].Timestamp - periodStart).TotalMilliseconds;
        }

        // Process event pairs
        for (int i = 0; i < eventsList.Count - 1; i++)
        {
            var current = eventsList[i];
            var next = eventsList[i + 1];

            if (current.EventType == "Connected" && next.EventType == "Disconnected")
            {
                var sessionStart = current.Timestamp > periodStart ? current.Timestamp : periodStart;
                var sessionEnd = next.Timestamp < now ? next.Timestamp : now;
                if (sessionEnd > sessionStart)
                {
                    uptimeMs += (long)(sessionEnd - sessionStart).TotalMilliseconds;
                }
            }
        }

        // Handle current session if connected
        var lastEvent = eventsList.LastOrDefault();
        if (lastEvent?.EventType == "Connected")
        {
            lock (_lock)
            {
                if (_currentState == GatewayConnectionState.Connected)
                {
                    var sessionStart = lastEvent.Timestamp > periodStart ? lastEvent.Timestamp : periodStart;
                    uptimeMs += (long)(now - sessionStart).TotalMilliseconds;
                }
            }
        }

        var periodMs = (now - periodStart).TotalMilliseconds;
        return periodMs > 0 ? (uptimeMs / periodMs) * 100.0 : 0.0;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConnectionEventDto> GetConnectionEvents(int days = 7)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

            var since = DateTime.UtcNow.AddDays(-days);
            var events = repository.GetEventsSinceAsync(since).GetAwaiter().GetResult();

            return events
                .Select(e => new ConnectionEventDto
                {
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    Reason = e.Reason,
                    Details = e.Details
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection events");
            return Array.Empty<ConnectionEventDto>();
        }
    }

    /// <inheritdoc/>
    public ConnectionStatsDto GetConnectionStats(int days = 7)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionEventRepository>();

            var since = DateTime.UtcNow.AddDays(-days);
            var events = repository.GetEventsSinceAsync(since).GetAwaiter().GetResult();
            var eventsList = events.OrderBy(e => e.Timestamp).ToList();

            var totalEvents = eventsList.Count;
            var reconnectionCount = eventsList.Count(e => e.EventType == "Connected") - 1;
            if (reconnectionCount < 0) reconnectionCount = 0;

            // Calculate average session duration
            var sessionDurations = new List<TimeSpan>();
            for (int i = 0; i < eventsList.Count - 1; i++)
            {
                if (eventsList[i].EventType == "Connected" && eventsList[i + 1].EventType == "Disconnected")
                {
                    sessionDurations.Add(eventsList[i + 1].Timestamp - eventsList[i].Timestamp);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection stats");
            return new ConnectionStatsDto();
        }
    }
}
