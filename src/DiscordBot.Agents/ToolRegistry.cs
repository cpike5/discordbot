using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Agents;

/// <summary>
/// Central registry of tool providers. Routes a tool call to the provider that owns the tool.
/// </summary>
/// <remarks>
/// Every registered provider's tools are advertised. Narrowing that for a particular run is
/// <see cref="FilteredToolRegistry"/>'s job, not this type's: the registry answers "who owns this
/// tool", and the decorator answers "may this run use it".
/// </remarks>
public class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly Dictionary<string, IToolProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the ToolRegistry.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="toolProviders">Tool providers to register automatically.</param>
    public ToolRegistry(ILogger<ToolRegistry> logger, IEnumerable<IToolProvider> toolProviders)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Auto-register all injected providers
        foreach (var provider in toolProviders)
        {
            RegisterProvider(provider);
        }
    }

    /// <inheritdoc />
    public void RegisterProvider(IToolProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_lock)
        {
            if (_providers.ContainsKey(provider.Name))
            {
                _logger.LogWarning(
                    "Provider {ProviderName} is already registered. Skipping duplicate registration",
                    provider.Name);
                return;
            }

            _providers[provider.Name] = provider;

            _logger.LogInformation(
                "Registered tool provider {ProviderName} ({Description}) with {ToolCount} tools",
                provider.Name,
                provider.Description,
                provider.GetTools().Count());
        }
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetEnabledTools()
    {
        lock (_lock)
        {
            // Sorted by name, not left in provider-registration order. Tool schemas serialize at
            // position 0 of the request, ahead of the system message, so any change in their order
            // invalidates every prompt-cache breakpoint behind them - and a DI reshuffle would
            // change that order silently, with correct answers and a tenfold price rise as the
            // only symptom.
            var tools = _providers.Values
                .SelectMany(p => p.GetTools())
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(
                "Retrieved {ToolCount} tools from {ProviderCount} registered providers",
                tools.Count,
                _providers.Count);

            return tools;
        }
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug(
            "Executing tool {ToolName} for user {UserId} in guild {GuildId}",
            toolName,
            context.UserId,
            context.GuildId);

        // Find the first registered provider that owns this tool
        IToolProvider? targetProvider = null;

        lock (_lock)
        {
            foreach (var provider in _providers.Values)
            {
                if (provider.GetTools().Any(t =>
                    t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    targetProvider = provider;
                    break;
                }
            }
        }

        if (targetProvider == null)
        {
            _logger.LogWarning(
                "Tool {ToolName} not found in any registered provider",
                toolName);
            throw new NotSupportedException($"Tool '{toolName}' not found in any registered provider");
        }

        _logger.LogDebug(
            "Routing tool {ToolName} to provider {ProviderName}",
            toolName,
            targetProvider.Name);

        try
        {
            var result = await targetProvider.ExecuteToolAsync(toolName, input, context, cancellationToken);

            if (result.Success)
            {
                _logger.LogDebug(
                    "Tool {ToolName} executed successfully via provider {ProviderName}",
                    toolName,
                    targetProvider.Name);
            }
            else
            {
                _logger.LogWarning(
                    "Tool {ToolName} execution failed via provider {ProviderName}: {Error}",
                    toolName,
                    targetProvider.Name,
                    result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            _logger.LogError(ex,
                "Tool {ToolName} execution threw exception via provider {ProviderName}",
                toolName,
                targetProvider.Name);

            return ToolExecutionResult.CreateError($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the names of all registered providers.
    /// </summary>
    /// <returns>Collection of provider names.</returns>
    public IEnumerable<string> GetProviderNames()
    {
        lock (_lock)
        {
            return _providers.Keys.ToList();
        }
    }

    /// <summary>
    /// Checks if a provider is registered.
    /// </summary>
    /// <param name="providerName">Name of the provider to check.</param>
    /// <returns>True if the provider is registered, false otherwise.</returns>
    public bool IsProviderRegistered(string providerName)
    {
        lock (_lock)
        {
            return _providers.ContainsKey(providerName);
        }
    }
}
