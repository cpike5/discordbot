using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing command module configuration with business logic.
/// </summary>
public interface ICommandModuleConfigurationService
{
    /// <summary>
    /// Gets all module configurations with metadata.
    /// Merges database values with discovered modules from the bot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all module configurations.</returns>
    Task<IReadOnlyList<CommandModuleConfigurationDto>> GetAllModulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all module configurations for a specific category.
    /// </summary>
    /// <param name="category">The module category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of module configurations in the category.</returns>
    Task<IReadOnlyList<CommandModuleConfigurationDto>> GetModulesByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single module configuration by name.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The module configuration if found, otherwise null.</returns>
    Task<CommandModuleConfigurationDto?> GetModuleAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a module is enabled.
    /// </summary>
    /// <param name="moduleName">The module name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the module is enabled, false otherwise.</returns>
    Task<bool> IsModuleEnabledAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the enabled state of a module.
    /// </summary>
    /// <param name="moduleName">The module name to update.</param>
    /// <param name="isEnabled">The new enabled state.</param>
    /// <param name="userId">The ID of the user making the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and whether restart is required.</returns>
    Task<CommandModuleUpdateResultDto> SetModuleEnabledAsync(
        string moduleName,
        bool isEnabled,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple module configurations.
    /// </summary>
    /// <param name="updates">The updates to apply.</param>
    /// <param name="userId">The ID of the user making the changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success, errors, and restart requirement.</returns>
    Task<CommandModuleUpdateResultDto> UpdateModulesAsync(
        CommandModuleConfigurationUpdateDto updates,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes module configurations with discovered modules from the bot.
    /// Creates configurations for new modules and optionally removes orphaned ones.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of modules added or updated.</returns>
    Task<int> SyncModulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a restart is pending due to module configuration changes.
    /// </summary>
    bool IsRestartPending { get; }

    /// <summary>
    /// Clears the restart pending flag.
    /// Should be called by the bot hosted service after successful restart.
    /// </summary>
    void ClearRestartPending();

    /// <summary>
    /// Event that is raised when module configurations are updated.
    /// </summary>
    event EventHandler<CommandModuleConfigurationChangedEventArgs>? ConfigurationChanged;
}

/// <summary>
/// Event arguments for module configuration changed events.
/// </summary>
public class CommandModuleConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The module names that were updated.
    /// </summary>
    public IReadOnlyList<string> UpdatedModules { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The user who made the changes.
    /// </summary>
    public string UserId { get; init; } = string.Empty;
}
