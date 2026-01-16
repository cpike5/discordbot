namespace DiscordBot.Core.Entities;

/// <summary>
/// Records Discord gateway connection state changes for uptime tracking.
/// Persisted to database to calculate uptime across restarts.
/// </summary>
public class ConnectionEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// Uses long for high volume scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Type of connection event: "Connected" or "Disconnected".
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional reason for the event (e.g., exception message for disconnections).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Optional additional details (e.g., exception type name).
    /// </summary>
    public string? Details { get; set; }
}
