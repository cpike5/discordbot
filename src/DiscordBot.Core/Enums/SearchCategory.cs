namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the different searchable categories in the application.
/// </summary>
public enum SearchCategory
{
    /// <summary>
    /// Guild (server) search results.
    /// </summary>
    Guilds,

    /// <summary>
    /// Command execution log search results.
    /// </summary>
    CommandLogs,

    /// <summary>
    /// User account search results.
    /// </summary>
    Users,

    /// <summary>
    /// Registered slash command search results.
    /// </summary>
    Commands,

    /// <summary>
    /// System audit log search results.
    /// </summary>
    AuditLogs,

    /// <summary>
    /// Discord message log search results.
    /// </summary>
    MessageLogs,

    /// <summary>
    /// Admin UI page search results.
    /// </summary>
    Pages
}
