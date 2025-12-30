using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Centralized service for managing the bot's Discord status with priority-based status sources.
/// Coordinates between multiple status sources (Rat Watch, custom status, maintenance mode, etc.)
/// and applies the highest priority active status to the Discord client.
/// </summary>
public class BotStatusService : IBotStatusService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotStatusService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Sorted dictionary maintains sources in priority order (lower number = higher priority)
    private readonly SortedDictionary<int, (string Name, Func<Task<string?>> Provider)> _sources = new();
    private string _currentSourceName = "None";
    private string? _currentMessage;

    public BotStatusService(
        DiscordSocketClient client,
        ILogger<BotStatusService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SetStatusAsync(string? message)
    {
        await _lock.WaitAsync();
        try
        {
            await SetDiscordStatusInternalAsync(message, ActivityType.Playing);
            _currentSourceName = "Direct";
            _currentMessage = message;
            _logger.LogInformation("Bot status set directly to: {Message}", message ?? "(null)");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RefreshStatusAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _logger.LogDebug("Refreshing bot status, evaluating {Count} registered sources", _sources.Count);

            // Evaluate all sources in priority order (lowest priority number first)
            foreach (var (priority, (name, provider)) in _sources)
            {
                try
                {
                    var message = await provider();
                    if (message != null)
                    {
                        // Found an active status source - use it
                        if (_currentSourceName != name || _currentMessage != message)
                        {
                            await SetDiscordStatusInternalAsync(message, ActivityType.Playing);
                            _logger.LogInformation(
                                "Bot status changed: Source={Source}, Priority={Priority}, Message={Message}",
                                name, priority, message);
                            _currentSourceName = name;
                            _currentMessage = message;
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Bot status unchanged: Source={Source}, Priority={Priority}, Message={Message}",
                                name, priority, message);
                        }
                        return;
                    }

                    _logger.LogTrace("Status source {Source} (Priority={Priority}) returned null, trying next source", name, priority);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to evaluate status source {Source} (Priority={Priority}), trying next source",
                        name, priority);
                }
            }

            // No active sources found - clear the status
            if (_currentSourceName != "None" || _currentMessage != null)
            {
                await SetDiscordStatusInternalAsync(null, ActivityType.Playing);
                _logger.LogInformation("Bot status cleared (no active status sources)");
                _currentSourceName = "None";
                _currentMessage = null;
            }
            else
            {
                _logger.LogDebug("Bot status unchanged (no active status sources)");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void RegisterStatusSource(string name, int priority, Func<Task<string?>> statusProvider)
    {
        _lock.Wait();
        try
        {
            if (_sources.ContainsKey(priority))
            {
                _logger.LogWarning(
                    "Overwriting existing status source at priority {Priority}: {OldName} -> {NewName}",
                    priority, _sources[priority].Name, name);
            }

            _sources[priority] = (name, statusProvider);
            _logger.LogInformation("Registered status source: {Name} (Priority={Priority})", name, priority);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void UnregisterStatusSource(string name)
    {
        _lock.Wait();
        try
        {
            var entry = _sources.FirstOrDefault(kvp => kvp.Value.Name == name);
            if (entry.Value.Name != null)
            {
                _sources.Remove(entry.Key);
                _logger.LogInformation("Unregistered status source: {Name} (Priority={Priority})", name, entry.Key);
            }
            else
            {
                _logger.LogWarning("Attempted to unregister non-existent status source: {Name}", name);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public (string SourceName, string? Message) GetCurrentStatus()
    {
        return (_currentSourceName, _currentMessage);
    }

    /// <summary>
    /// Sets the Discord client status with error handling.
    /// </summary>
    private async Task SetDiscordStatusInternalAsync(string? message, ActivityType type)
    {
        try
        {
            if (type == ActivityType.Playing)
            {
                await _client.SetGameAsync(message);
            }
            else
            {
                await _client.SetActivityAsync(message != null ? new Game(message, type) : null);
            }
            _logger.LogDebug("Discord status updated: Message={Message}, Type={Type}", message ?? "(null)", type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Discord client status");
        }
    }
}
