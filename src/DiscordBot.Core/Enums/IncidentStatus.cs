namespace DiscordBot.Core.Enums;

/// <summary>
/// Status states for a performance incident.
/// </summary>
public enum IncidentStatus
{
    /// <summary>
    /// Incident is currently active and unresolved.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Incident has been resolved (auto-recovery or manual).
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// Incident has been acknowledged by an administrator but not yet resolved.
    /// </summary>
    Acknowledged = 2
}
