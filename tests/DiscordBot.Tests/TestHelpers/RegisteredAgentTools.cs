using System.Reflection;
using System.Runtime.CompilerServices;
using cpike.Agent;
using DiscordBot.Bot.Extensions;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Every tool this application puts in front of a model, found the way the application finds them:
/// by scanning Infrastructure and Bot for <see cref="IAgentTool"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Reflection rather than a list, so a tool added tomorrow is covered by the contract test without
/// anyone remembering to add it here — a hand-maintained list would fail exactly the case the test
/// exists for. The framework's own built-ins (<c>load_skill</c>, <c>get_current_time</c>) live in the
/// cpike.Agent assembly and are not scanned: they are the framework's contract, not the house's.
/// </para>
/// <para>
/// Tools are read without constructing anything. A tool answers <c>Definition</c> from an
/// uninitialized instance, which is itself part of the contract: the definition is serialized at
/// position 0 of every request, so it has to be a static value rather than something composed out of
/// injected state — a definition that can differ between two runs is a cold prompt cache at roughly
/// ten times the input price.
/// </para>
/// </remarks>
public static class RegisteredAgentTools
{
    /// <summary>The assemblies the application scans, named the same way it names them.</summary>
    private static readonly Assembly[] Assemblies = AgentToolAssemblies.All.ToArray();

    /// <summary>Every house tool, as an uninitialized instance.</summary>
    /// <remarks>Declared first: <see cref="All"/>'s initializer reads it.</remarks>
    public static IReadOnlyList<IAgentTool> AgentTools { get; } = DiscoverAgentTools();

    /// <summary>Every tool definition the application can advertise, with where it came from.</summary>
    public static IReadOnlyList<RegisteredTool> All { get; } =
        AgentTools.Select(tool => new RegisteredTool(Read(tool), tool.GetType(), tool)).ToList();

    /// <summary>The concrete tool types, for tests that resolve them through DI.</summary>
    public static IReadOnlyList<Type> Types { get; } = AgentTools.Select(t => t.GetType()).ToList();

    private static List<IAgentTool> DiscoverAgentTools() =>
        Assemblies
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                        && typeof(IAgentTool).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (IAgentTool)RuntimeHelpers.GetUninitializedObject(t))
            .ToList();

    /// <summary>
    /// A working instance of <paramref name="toolType"/>, built through its constructor with stand-ins
    /// for every dependency: Moq mocks for interfaces, <see cref="NullLogger{T}"/> for loggers, default
    /// options, a <see cref="TestRunContext"/> for the run, and the same treatment recursively for
    /// concrete helpers. For tests that call a tool rather than only read its definition.
    /// </summary>
    public static IAgentTool Construct(Type toolType, DiscordRunContext? run = null) =>
        (IAgentTool)Build(toolType, run ?? TestRunContext.Create());

    private static object Build(Type type, DiscordRunContext run)
    {
        if (type == typeof(DiscordRunContext))
        {
            return run;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            return Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(type.GetGenericArguments()))!;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
        {
            var value = Activator.CreateInstance(type.GetGenericArguments()[0])!;
            return typeof(Options).GetMethod(nameof(Options.Create))!
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, [value])!;
        }

        if (type.IsInterface)
        {
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type))!;
            return mock.Object;
        }

        var constructor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var arguments = constructor.GetParameters().Select(p => Build(p.ParameterType, run)).ToArray();
        return constructor.Invoke(arguments);
    }

    private static AgentToolDefinition Read(IAgentTool tool)
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
/// <param name="DeclaringType">The tool type, for a failure message.</param>
/// <param name="Tool">
/// The tool instance. Uninitialized: good for <c>Definition</c>, which is static by contract, and for
/// argument validation, which happens before any dependency is touched.
/// </param>
public sealed record RegisteredTool(AgentToolDefinition Definition, Type DeclaringType, IAgentTool Tool)
{
    /// <summary>The model-facing tool name.</summary>
    public string Name => Definition.Name!;

    /// <summary>How the tool is named in a failure message.</summary>
    public override string ToString() => $"{Definition.Name} ({DeclaringType.Name})";
}
