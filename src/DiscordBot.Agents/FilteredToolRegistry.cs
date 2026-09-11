using System.Text.Json;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// An <see cref="IToolRegistry"/> decorator that narrows an inner registry to a named allow-list.
/// </summary>
/// <remarks>
/// <para>
/// The refusal in <see cref="ExecuteToolAsync"/> is defence in depth rather than belt-and-braces: a
/// model that saw a tool in an earlier cached prefix will sometimes call it after the host has
/// stopped advertising it, and filtering only <see cref="GetEnabledTools"/> would let that through.
/// </para>
/// <para>
/// The engine holds no policy of its own - who decides the allow-list, and on what, is entirely the
/// host's business. This type only applies the set it is handed.
/// </para>
/// </remarks>
public class FilteredToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _inner;
    private readonly IReadOnlySet<string> _allowed;

    /// <summary>
    /// Wraps <paramref name="inner"/>, exposing only the tools named in <paramref name="allowed"/>.
    /// </summary>
    /// <param name="inner">The registry to filter.</param>
    /// <param name="allowed">
    /// Tool names to allow. Compared case-insensitively regardless of the set's own comparer, so a
    /// caller cannot accidentally make the filter case-sensitive.
    /// </param>
    public FilteredToolRegistry(IToolRegistry inner, IReadOnlySet<string> allowed)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(allowed);

        _allowed = allowed is HashSet<string> { Comparer: var comparer } set
                   && ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)
            ? set
            : new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The allow-list this registry applies.</summary>
    public IReadOnlySet<string> AllowedTools => _allowed;

    /// <inheritdoc />
    public void RegisterProvider(IToolProvider provider) => _inner.RegisterProvider(provider);

    /// <inheritdoc />
    /// <remarks>
    /// Re-sorted after filtering for the same reason the inner registry sorts: tool schemas
    /// serialize ahead of the system message, so their order is part of the cached prefix.
    /// </remarks>
    public IEnumerable<LlmToolDefinition> GetEnabledTools() =>
        _inner.GetEnabledTools()
            .Where(t => _allowed.Contains(t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public string? FindProviderName(string toolName) =>
        !string.IsNullOrWhiteSpace(toolName) && _allowed.Contains(toolName)
            ? _inner.FindProviderName(toolName)
            : null;

    /// <inheritdoc />
    public Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!_allowed.Contains(toolName))
        {
            throw new NotSupportedException($"Tool '{toolName}' is not enabled in this scope");
        }

        return _inner.ExecuteToolAsync(toolName, input, context, cancellationToken);
    }
}
