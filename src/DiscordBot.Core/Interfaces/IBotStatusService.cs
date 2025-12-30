namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for centralized bot status management with priority-based status sources.
/// Replaces the scattered status logic previously in RatWatchStatusService and BotHostedService.
/// </summary>
public interface IBotStatusService
{
    /// <summary>
    /// Sets the bot's Discord status directly, bypassing the priority system.
    /// Use with caution - prefer RegisterStatusSource for managed status.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    Task SetStatusAsync(string? message);

    /// <summary>
    /// Re-evaluates all registered status sources by priority and applies the highest priority active status.
    /// Call this when any status source's state may have changed.
    /// </summary>
    Task RefreshStatusAsync();

    /// <summary>
    /// Registers a status source with a given priority (lower number = higher priority).
    /// </summary>
    /// <param name="name">The unique name for this status source.</param>
    /// <param name="priority">The priority level (lower number = higher priority).</param>
    /// <param name="statusProvider">A function that returns the status message, or null if inactive.</param>
    void RegisterStatusSource(string name, int priority, Func<Task<string?>> statusProvider);

    /// <summary>
    /// Unregisters a previously registered status source.
    /// </summary>
    /// <param name="name">The unique name of the status source to unregister.</param>
    void UnregisterStatusSource(string name);

    /// <summary>
    /// Gets the current active status source name and message.
    /// </summary>
    /// <returns>A tuple containing the source name and message.</returns>
    (string SourceName, string? Message) GetCurrentStatus();
}

/// <summary>
/// Well-known priority levels for status sources.
/// Lower numbers indicate higher priority.
/// </summary>
public static class StatusSourcePriority
{
    /// <summary>
    /// Maintenance mode status (highest priority).
    /// </summary>
    public const int Maintenance = 10;

    /// <summary>
    /// Rat Watch active status.
    /// </summary>
    public const int RatWatch = 20;

    /// <summary>
    /// Custom user-configured status message.
    /// </summary>
    public const int CustomStatus = 100;

    /// <summary>
    /// Default fallback status (lowest priority).
    /// </summary>
    public const int Default = 1000;
}
