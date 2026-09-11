using System.Reflection;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordBot.Agents;

/// <summary>
/// Finds the <see cref="IAgentTool"/>s in a set of assemblies and registers them, so adding a tool
/// is adding a file rather than a file and a DI line someone has to remember.
/// </summary>
public static class AgentToolRegistration
{
    /// <summary>
    /// Registers every concrete <see cref="IAgentTool"/> in <paramref name="assemblies"/> as a
    /// scoped service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assemblies are a parameter rather than something this method works out for itself: the
    /// engine is a leaf library and the tools live in the host's assemblies, which it cannot see and
    /// should not guess at.
    /// </para>
    /// <para>
    /// Scoped, because a tool's dependencies usually are (repositories, a <c>DbContext</c>).
    /// Registered through <c>TryAddEnumerable</c>, so calling this twice — once per assistant
    /// surface, say — registers each tool once.
    /// </para>
    /// <para>
    /// Order is by full type name, so the registered sequence is the same on every process and does
    /// not depend on reflection order. The tool <em>array</em> is sorted by name downstream anyway
    /// (<c>ToolRegistry.GetEnabledTools</c>), but a stable registration order keeps everything else
    /// — logs, the provider's <c>Tools</c> list, failures — reproducible.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan. Duplicates are ignored.</param>
    /// <param name="configuration">
    /// Used to evaluate <see cref="OptInToolAttribute"/> flags. When null, every opt-in tool is left
    /// unregistered — "ships dark" is the safe reading of "nobody said".
    /// </param>
    /// <returns>The number of tool types registered by this call, for a startup log line.</returns>
    public static int AddAgentTools(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var toolTypes = assemblies
            .Where(a => a is not null)
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(IsAgentTool)
            .Where(type => IsEnabled(type, configuration))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var type in toolTypes)
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IAgentTool), type));
        }

        return toolTypes.Count;
    }

    /// <summary>
    /// A concrete, instantiable <see cref="IAgentTool"/>. Abstract bases and open generics are
    /// skipped rather than failing the scan.
    /// </summary>
    private static bool IsAgentTool(Type type) =>
        type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(IAgentTool).IsAssignableFrom(type);

    /// <summary>
    /// Whether a tool's <see cref="OptInToolAttribute"/> flag, if it has one, is set.
    /// </summary>
    private static bool IsEnabled(Type type, IConfiguration? configuration)
    {
        var optIn = type.GetCustomAttribute<OptInToolAttribute>();

        return optIn is null
            || (configuration?.GetValue<bool>(optIn.ConfigurationKey) ?? false);
    }

    /// <summary>
    /// The types in <paramref name="assembly"/>, tolerating one whose dependencies will not load:
    /// a tool in an assembly that also holds something unloadable should not take the whole scan
    /// down with it.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
