using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for logging command executions to the database.
/// </summary>
public class CommandExecutionLogger : ICommandExecutionLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandExecutionLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutionLogger"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped repositories.</param>
    /// <param name="logger">The logger instance.</param>
    public CommandExecutionLogger(
        IServiceScopeFactory scopeFactory,
        ILogger<CommandExecutionLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogCommandExecutionAsync(
        IInteractionContext context,
        string commandName,
        string? parameters,
        int executionTimeMs,
        bool success,
        string? errorMessage = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a new scope to access scoped services (repositories are scoped)
            using var scope = _scopeFactory.CreateScope();
            var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

            _logger.LogDebug(
                "Logging command execution: {CommandName} by user {UserId} in guild {GuildId}, Success: {Success}, ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                commandName,
                context.User.Id,
                context.Guild?.Id,
                success,
                executionTimeMs,
                correlationId);

            await commandLogRepository.LogCommandAsync(
                context.Guild?.Id,
                context.User.Id,
                commandName,
                parameters,
                executionTimeMs,
                success,
                errorMessage,
                correlationId,
                cancellationToken);

            _logger.LogTrace(
                "Command execution logged successfully for command {CommandName}, CorrelationId: {CorrelationId}",
                commandName,
                correlationId);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - logging failures shouldn't break command execution
            _logger.LogError(
                ex,
                "Failed to log command execution for {CommandName} by user {UserId}, CorrelationId: {CorrelationId}",
                commandName,
                context.User.Id,
                correlationId);
        }
    }
}
