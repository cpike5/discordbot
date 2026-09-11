using System.Text.Json;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// Exposes a set of individually authored <see cref="IAgentTool"/>s to the registry as one
/// <see cref="IToolProvider"/>, and enforces the caller-access check on the way in.
/// </summary>
/// <remarks>
/// <para>
/// The registry's unit is a provider; the authoring unit is a tool. This is the one adapter between
/// them, which is why a new tool needs no provider of its own and no registration beyond being
/// scanned.
/// </para>
/// <para>
/// A host normally derives from this once per surface — the tools a guild assistant advertises are
/// not the tools a DM assistant advertises — and hands the base the subset that belongs on that
/// surface. The engine has no opinion about what a surface is.
/// </para>
/// </remarks>
public class AgentToolProvider : IToolProvider
{
    private readonly IReadOnlyList<IAgentTool> _tools;
    private readonly Dictionary<string, IAgentTool> _byName;
    private readonly Func<string, ToolExecutionResult> _mutationRefusal;

    /// <summary>
    /// Wraps <paramref name="tools"/> as a provider.
    /// </summary>
    /// <param name="name">Provider name, as it appears in the registry and in tool spans.</param>
    /// <param name="description">Human-readable description of the group.</param>
    /// <param name="tools">The tools on this surface.</param>
    /// <param name="mutationRefusal">
    /// Builds the refusal returned when a tool declares an <see cref="IAgentTool.Mutation"/> and the
    /// caller may not write. Defaults to <see cref="ToolResults.Forbidden"/>; a host with a voice of
    /// its own passes its own. Takes the tool's <see cref="IAgentTool.Mutation"/> phrase.
    /// </param>
    /// <exception cref="ArgumentException">Two tools advertise the same name.</exception>
    public AgentToolProvider(
        string name,
        string description,
        IEnumerable<IAgentTool> tools,
        Func<string, ToolExecutionResult>? mutationRefusal = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tools);

        Name = name;
        Description = description ?? string.Empty;
        _tools = tools.ToList();
        _mutationRefusal = mutationRefusal ?? ToolResults.Forbidden;

        _byName = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in _tools)
        {
            var toolName = tool.Definition.Name;

            // Loud, and at construction: two tools answering to one name means the registry's
            // first-match walk silently picks one of them, and which one depends on scan order.
            if (!_byName.TryAdd(toolName, tool))
            {
                throw new ArgumentException(
                    $"Tool name '{toolName}' is declared by both {_byName[toolName].GetType().Name} "
                    + $"and {tool.GetType().Name}. Tool names must be unique.",
                    nameof(tools));
            }
        }
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>The tools this provider exposes, in the order it was given them.</summary>
    public IReadOnlyList<IAgentTool> Tools => _tools;

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools() => _tools.Select(t => t.Definition);

    /// <inheritdoc />
    public Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(context);

        if (!_byName.TryGetValue(toolName, out var tool))
        {
            throw new NotSupportedException($"Tool '{toolName}' is not supported by provider '{Name}'");
        }

        // Before the tool is entered, so a write tool cannot forget the check and a refusal never
        // half-runs. This is the whole of the CanMutate convention: declaring Mutation is the guard.
        if (tool.Mutation is { } action && !context.CanMutate)
        {
            return Task.FromResult(_mutationRefusal(action));
        }

        return tool.InvokeAsync(input, context, cancellationToken);
    }
}
