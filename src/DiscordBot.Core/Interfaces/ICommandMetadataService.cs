using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for retrieving command metadata from the Discord.NET InteractionService.
/// </summary>
public interface ICommandMetadataService
{
    /// <summary>
    /// Gets all command modules with their commands and metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of command module DTOs.</returns>
    Task<IReadOnlyList<CommandModuleDto>> GetAllModulesAsync(CancellationToken cancellationToken = default);
}
