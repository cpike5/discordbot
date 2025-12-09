namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for command log entries.
/// </summary>
public class CommandLogDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the guild ID where the command was executed.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name where the command was executed.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the user ID who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the user who executed the command.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command parameters as JSON.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
