using System.Reflection;
using System.Runtime.CompilerServices;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Bot.Extensions;
using DiscordBot.Infrastructure.Services.LLM;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Every tool this application puts in front of a model, found the way the application finds them:
/// by scanning Infrastructure and Bot.
/// </summary>
/// <remarks>
/// <para>
/// Reflection rather than a list, so a tool added tomorrow is covered by the contract test without
/// anyone remembering to add it here — a hand-maintained list would fail exactly the case the test
/// exists for.
/// </para>
/// <para>
/// Two shapes declare a tool in this repository, and both are read without constructing anything.
/// A tool authored as an <see cref="IAgentTool"/> answers <c>Definition</c> from an uninitialized
/// instance, which is itself part of the contract: the definition is serialized at position 0 of
/// every request, so it has to be a static value rather than something composed out of injected
/// state — a definition that can differ between two runs is a cold prompt cache at roughly ten
/// times the input price. The hand-written providers declare theirs in static definition classes
/// (<c>Services/LLM/Implementations/*Tools.GetAllTools()</c>) or in a static field, and those are
/// read directly. Providers themselves are never constructed here: a provider may legitimately
/// decide what to advertise from options (<c>CodeExecutionToolProvider</c> does), and that decision
/// is about whether a tool ships, not about whether its schema is well formed.
/// </para>
/// </remarks>
public static class RegisteredAgentTools
{
    /// <summary>The assemblies the application scans, named the same way it names them.</summary>
    private static readonly Assembly[] Assemblies =
    {
        typeof(ToolPermissions).Assembly,
        typeof(AssistantServiceExtensions).Assembly
    };

    /// <summary>Every tool authored as an <see cref="IAgentTool"/>, as an instance.</summary>
    /// <remarks>Declared first: <see cref="All"/>'s initializer reads it.</remarks>
    public static IReadOnlyList<IAgentTool> AgentTools { get; } = DiscoverAgentTools();

    /// <summary>
    /// Every tool definition the application can advertise, with where it came from.
    /// </summary>
    public static IReadOnlyList<RegisteredTool> All { get; } = Discover();

    private static List<RegisteredTool> Discover()
    {
        var tools = AgentTools
            .Select(tool => new RegisteredTool(Read(tool), tool.GetType(), tool))
            .ToList();

        foreach (var type in Types().Where(t => !typeof(IAgentTool).IsAssignableFrom(t)))
        {
            foreach (var definition in StaticDefinitions(type))
            {
                tools.Add(new RegisteredTool(definition, type, null));
            }
        }

        return tools;
    }

    private static List<IAgentTool> DiscoverAgentTools() =>
        Types()
            .Where(t => t is { IsAbstract: false, ContainsGenericParameters: false }
                        && typeof(IAgentTool).IsAssignableFrom(t))
            .Select(t => (IAgentTool)RuntimeHelpers.GetUninitializedObject(t))
            .ToList();

    /// <summary>
    /// The tool definitions <paramref name="type"/> declares statically: a parameterless static
    /// method returning a sequence of them (the <c>GetAllTools()</c> convention the hand-written
    /// providers use), or a static field or property holding one.
    /// </summary>
    private static IEnumerable<LlmToolDefinition> StaticDefinitions(Type type)
    {
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var method in type.GetMethods(Statics))
        {
            if (method.GetParameters().Length != 0
                || !typeof(IEnumerable<LlmToolDefinition>).IsAssignableFrom(method.ReturnType))
            {
                continue;
            }

            var definitions = (IEnumerable<LlmToolDefinition>?)method.Invoke(null, null);

            foreach (var definition in definitions ?? Enumerable.Empty<LlmToolDefinition>())
            {
                yield return definition;
            }
        }

        foreach (var field in type.GetFields(Statics).Where(f => f.FieldType == typeof(LlmToolDefinition)))
        {
            if (field.GetValue(null) is LlmToolDefinition definition)
            {
                yield return definition;
            }
        }

        foreach (var property in type.GetProperties(Statics)
                     .Where(p => p.PropertyType == typeof(LlmToolDefinition) && p.CanRead))
        {
            if (property.GetValue(null) is LlmToolDefinition definition)
            {
                yield return definition;
            }
        }
    }

    private static IEnumerable<Type> Types() =>
        Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static LlmToolDefinition Read(IAgentTool tool)
    {
        try
        {
            return tool.Definition;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{tool.GetType().Name}.Definition could not be read without its constructor having "
                + "run. A tool definition is serialized at position 0 of every request and must be a "
                + "static value; building one from injected state risks a differing schema and a cold "
                + "prompt cache. Move it to a static readonly field.",
                ex);
        }
    }
}

/// <summary>One advertised tool, and where it came from.</summary>
/// <param name="Definition">What the model sees.</param>
/// <param name="DeclaringType">The tool or provider that declares it, for a failure message.</param>
/// <param name="Tool">
/// The tool instance when it is authored as an <see cref="IAgentTool"/>, else null. Uninitialized:
/// good for <c>Definition</c> and <c>Mutation</c>, which are static by contract, and for argument
/// validation, which happens before any dependency is touched.
/// </param>
public sealed record RegisteredTool(LlmToolDefinition Definition, Type DeclaringType, IAgentTool? Tool)
{
    /// <summary>The model-facing tool name.</summary>
    public string Name => Definition.Name;

    /// <summary>How the tool is named in a failure message.</summary>
    public override string ToString() => $"{Definition.Name} ({DeclaringType.Name})";
}
