using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Hosting;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Base class for background services that support health monitoring.
/// Automatically registers with IBackgroundServiceHealthRegistry and provides
/// heartbeat tracking, status management, and error recording.
/// </summary>
public abstract class MonitoredBackgroundService : BackgroundService, IBackgroundServiceHealth
{
    private readonly IServiceProvider _serviceProvider;
    protected readonly ILogger _logger;
    private IBackgroundServiceHealthRegistry? _healthRegistry;

    private DateTime? _lastHeartbeat;
    private string? _lastError;
    private string _status = "Initializing";

    // IBackgroundServiceHealth implementation
    public abstract string ServiceName { get; }
    public string Status => _status;
    public DateTime? LastHeartbeat => _lastHeartbeat;
    public string? LastError => _lastError;

    protected MonitoredBackgroundService(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Called to perform the service's work. Override this instead of ExecuteAsync.
    /// </summary>
    protected abstract Task ExecuteMonitoredAsync(CancellationToken stoppingToken);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately to prevent blocking startup
        await Task.Yield();

        // Lazy resolve to avoid circular DI
        _healthRegistry = _serviceProvider.GetService<IBackgroundServiceHealthRegistry>();
        _healthRegistry?.Register(ServiceName, this);

        try
        {
            _status = "Running";
            _logger.LogInformation("{ServiceName} started and registered with health monitoring", ServiceName);

            await ExecuteMonitoredAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{ServiceName} stopping due to cancellation", ServiceName);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _status = "Error";
            _logger.LogError(ex, "{ServiceName} encountered a fatal error", ServiceName);
            throw;
        }
        finally
        {
            _status = "Stopped";
            _healthRegistry?.Unregister(ServiceName);
            _logger.LogInformation("{ServiceName} stopped and unregistered from health monitoring", ServiceName);
        }
    }

    /// <summary>
    /// Updates the heartbeat timestamp. Call this periodically in your service loop.
    /// </summary>
    protected void UpdateHeartbeat()
    {
        _lastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Records an error. The service remains running.
    /// </summary>
    protected void RecordError(Exception ex)
    {
        _lastError = ex.Message;
        _status = "Error";
    }

    /// <summary>
    /// Records an error message. The service remains running.
    /// </summary>
    protected void RecordError(string errorMessage)
    {
        _lastError = errorMessage;
        _status = "Error";
    }

    /// <summary>
    /// Clears the error state and returns to Running status.
    /// </summary>
    protected void ClearError()
    {
        _lastError = null;
        _status = "Running";
    }

    /// <summary>
    /// Sets a custom status (e.g., "Syncing", "Processing").
    /// </summary>
    protected void SetStatus(string status)
    {
        _status = status;
    }
}
