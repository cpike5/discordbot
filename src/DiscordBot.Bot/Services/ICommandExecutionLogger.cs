using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service interface for logging command executions to the database.
/// </summary>
public interface ICommandExecutionLogger
{
    /// <summary>
    /// Logs a command execution to the database.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="commandName">The name of the executed command.</param>
    /// <param name="parameters">The serialized command parameters.</param>
    /// <param name="executionTimeMs">The command execution time in milliseconds.</param>
    /// <param name="success">Whether the command executed successfully.</param>
    /// <param name="errorMessage">The error message if the command failed.</param>
    /// <param name="correlationId">The correlation ID for tracking the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogCommandExecutionAsync(
        IInteractionContext context,
        string commandName,
        string? parameters,
        int executionTimeMs,
        bool success,
        string? errorMessage = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
