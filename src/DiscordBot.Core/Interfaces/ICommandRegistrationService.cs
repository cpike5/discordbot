namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing Discord slash command registration.
/// </summary>
public interface ICommandRegistrationService
{
    /// <summary>
    /// Clears all registered commands (global and guild-specific) and re-registers them globally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing success status and message.</returns>
    Task<CommandRegistrationResult> ClearAndRegisterGloballyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a command registration operation.
/// </summary>
public record CommandRegistrationResult
{
    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a human-readable message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of global commands registered.
    /// </summary>
    public int GlobalCommandsRegistered { get; init; }

    /// <summary>
    /// Gets the number of guilds cleared of commands.
    /// </summary>
    public int GuildsCleared { get; init; }
}
