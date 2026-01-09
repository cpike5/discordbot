using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;

namespace DiscordBot.Bot.Services.Commands;

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
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

            // Ensure user exists before logging command (foreign key constraint)
            await userRepository.UpsertAsync(new Core.Entities.User
            {
                Id = context.User.Id,
                Username = context.User.Username,
                Discriminator = context.User.Discriminator,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            }, cancellationToken);

            // Ensure guild exists before logging command (foreign key constraint)
            if (context.Guild != null)
            {
                await guildRepository.UpsertAsync(new Core.Entities.Guild
                {
                    Id = context.Guild.Id,
                    Name = context.Guild.Name,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                }, cancellationToken);
            }

            _logger.LogDebug(
                "Logging command execution: {CommandName} by user {UserId} in guild {GuildId}, Success: {Success}, ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                commandName,
                context.User.Id,
                context.Guild?.Id,
                success,
                executionTimeMs,
                correlationId);

            // Sanitize parameters to prevent sensitive data from being stored in logs
            var sanitizedParameters = LogSanitizer.SanitizeString(parameters);
            var sanitizedErrorMessage = LogSanitizer.SanitizeString(errorMessage);

            await commandLogRepository.LogCommandAsync(
                context.Guild?.Id,
                context.User.Id,
                commandName,
                sanitizedParameters,
                executionTimeMs,
                success,
                sanitizedErrorMessage,
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
