namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for real-time command execution updates.
/// </summary>
public class CommandExecutedUpdateDto
{
    /// <summary>
    /// Gets or sets the name of the executed command.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild ID where the command was executed.
    /// Null if the command was executed in a DM.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the guild where the command was executed.
    /// Null if the command was executed in a DM.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the user ID of the person who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the person who executed the command.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the command was executed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
