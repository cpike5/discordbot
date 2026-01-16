using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Central registry for managing tool providers with enable/disable capability.
/// Routes tool execution to the appropriate provider.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly Dictionary<string, ProviderEntry> _providers = new(StringComparer.OrdinalIgnoreCase);
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
    public void RegisterProvider(IToolProvider provider, bool enabled = true)
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

            _providers[provider.Name] = new ProviderEntry(provider, enabled);

            _logger.LogInformation(
                "Registered tool provider {ProviderName} ({Description}) with {ToolCount} tools. Enabled: {Enabled}",
                provider.Name,
                provider.Description,
                provider.GetTools().Count(),
                enabled);
        }
    }

    /// <inheritdoc />
    public void EnableProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        lock (_lock)
        {
            if (!_providers.TryGetValue(providerName, out var entry))
            {
                throw new InvalidOperationException($"Provider '{providerName}' not found in registry");
            }

            if (entry.IsEnabled)
            {
                _logger.LogDebug("Provider {ProviderName} is already enabled", providerName);
                return;
            }

            entry.IsEnabled = true;
            _logger.LogInformation("Enabled tool provider {ProviderName}", providerName);
        }
    }

    /// <inheritdoc />
    public void DisableProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        lock (_lock)
        {
            if (!_providers.TryGetValue(providerName, out var entry))
            {
                throw new InvalidOperationException($"Provider '{providerName}' not found in registry");
            }

            if (!entry.IsEnabled)
            {
                _logger.LogDebug("Provider {ProviderName} is already disabled", providerName);
                return;
            }

            entry.IsEnabled = false;
            _logger.LogInformation("Disabled tool provider {ProviderName}", providerName);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetEnabledTools()
    {
        lock (_lock)
        {
            var tools = _providers.Values
                .Where(e => e.IsEnabled)
                .SelectMany(e => e.Provider.GetTools())
                .ToList();

            _logger.LogDebug(
                "Retrieved {ToolCount} tools from {ProviderCount} enabled providers",
                tools.Count,
                _providers.Values.Count(e => e.IsEnabled));

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

        // Find the first enabled provider that has this tool
        IToolProvider? targetProvider = null;

        lock (_lock)
        {
            foreach (var entry in _providers.Values.Where(e => e.IsEnabled))
            {
                if (entry.Provider.GetTools().Any(t =>
                    t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    targetProvider = entry.Provider;
                    break;
                }
            }
        }

        if (targetProvider == null)
        {
            _logger.LogWarning(
                "Tool {ToolName} not found in any enabled provider",
                toolName);
            throw new NotSupportedException($"Tool '{toolName}' not found in any enabled provider");
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

    /// <summary>
    /// Checks if a provider is enabled.
    /// </summary>
    /// <param name="providerName">Name of the provider to check.</param>
    /// <returns>True if the provider is enabled, false if disabled or not registered.</returns>
    public bool IsProviderEnabled(string providerName)
    {
        lock (_lock)
        {
            return _providers.TryGetValue(providerName, out var entry) && entry.IsEnabled;
        }
    }

    /// <summary>
    /// Internal class to track provider registration state.
    /// </summary>
    private class ProviderEntry
    {
        public IToolProvider Provider { get; }
        public bool IsEnabled { get; set; }

        public ProviderEntry(IToolProvider provider, bool isEnabled)
        {
            Provider = provider;
            IsEnabled = isEnabled;
        }
    }
}
