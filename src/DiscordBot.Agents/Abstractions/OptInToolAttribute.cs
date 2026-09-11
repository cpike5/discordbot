namespace DiscordBot.Agents.Abstractions;

/// <summary>
/// Marks an <see cref="IAgentTool"/> that is registered only when a configuration flag is true, so a
/// capability can ship dark.
/// </summary>
/// <remarks>
/// The alternative — shipping the tool and having it decline at execution time, or hiding it from
/// <c>GetTools()</c> with a runtime branch — still pays for the class, the branch, and (in the second
/// case) a tool list whose shape depends on configuration read at an awkward moment. Gating the
/// <em>registration</em> means a disabled tool costs nothing and the flag is read exactly once, at
/// startup.
/// </remarks>
/// <param name="configurationKey">
/// Full configuration path of the flag, e.g. <c>"DmAssistant:CodeExecution:Enabled"</c>. The tool is
/// registered only when that key binds to <c>true</c>; a missing key, a false one, or no configuration
/// at all all mean "not registered".
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OptInToolAttribute(string configurationKey) : Attribute
{
    /// <summary>Full configuration path of the flag that enables the tool.</summary>
    public string ConfigurationKey { get; } = configurationKey;
}
