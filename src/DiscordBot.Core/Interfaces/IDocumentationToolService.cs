using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Legacy service interface for documentation tool operations.
/// OBSOLETE: Use DocumentationToolProvider : IToolProvider instead.
/// Provided for backward compatibility during transition to abstraction layer.
/// </summary>
[Obsolete("Use DocumentationToolProvider : IToolProvider instead. This interface will be removed in a future release.")]
public interface IDocumentationToolService
{
    /// <summary>
    /// Retrieves documentation for a specific feature.
    /// </summary>
    /// <param name="featureName">Name of the feature to get documentation for.</param>
    /// <param name="guildId">Discord guild ID for guild-specific feature status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result containing feature documentation or error message.</returns>
    Task<ToolExecutionResult> GetFeatureDocumentationAsync(
        string featureName,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for commands matching the specified query.
    /// </summary>
    /// <param name="query">Search query to match against command names and descriptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result containing matching commands or error message.</returns>
    Task<ToolExecutionResult> SearchCommandsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific command.
    /// </summary>
    /// <param name="commandName">Name of the command to retrieve details for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result containing command details or error message.</returns>
    Task<ToolExecutionResult> GetCommandDetailsAsync(
        string commandName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available features and their status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result containing feature list or error message.</returns>
    Task<ToolExecutionResult> ListFeaturesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cached documentation as a dictionary.
    /// Used for bulk documentation retrieval and caching strategies.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping feature names to documentation content.</returns>
    Task<Dictionary<string, string>> GetCachedDocumentationAsync(
        CancellationToken cancellationToken = default);
}
