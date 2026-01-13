using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing command module configuration persistence.
/// </summary>
public interface ICommandModuleConfigurationRepository
{
    /// <summary>
    /// Gets a module configuration by its name.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration if found, otherwise null.</returns>
    Task<CommandModuleConfiguration?> GetByNameAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all module configurations for a specific category.
    /// </summary>
    /// <param name="category">The module category (e.g., "Admin", "Moderation").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of configurations in the category.</returns>
    Task<IReadOnlyList<CommandModuleConfiguration>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all module configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all module configurations.</returns>
    Task<IReadOnlyList<CommandModuleConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled module configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled module configurations.</returns>
    Task<IReadOnlyList<CommandModuleConfiguration>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a module configuration.
    /// </summary>
    /// <param name="configuration">The configuration to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(CommandModuleConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates multiple module configurations.
    /// </summary>
    /// <param name="configurations">The configurations to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRangeAsync(IEnumerable<CommandModuleConfiguration> configurations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a module configuration by its name.
    /// </summary>
    /// <param name="moduleName">The module name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string moduleName, CancellationToken cancellationToken = default);
}
