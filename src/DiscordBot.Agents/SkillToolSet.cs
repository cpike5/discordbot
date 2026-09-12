using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// Works out which of a registry's tools a run should advertise, given the skills it has loaded.
/// </summary>
/// <remarks>
/// <para>
/// The rule is one sentence: <em>a tool named by any available skill is hidden until one of the
/// skills naming it is loaded</em>. Everything else the registry holds is advertised as it always
/// was, which is what keeps a surface's common tools always-on and puts only what a skill claims
/// behind a round trip.
/// </para>
/// <para>
/// The set is always computed from <see cref="IToolRegistry.GetEnabledTools"/>, never from the
/// skill's own list, so loading a skill can only ever un-hide something the registry already holds.
/// That is what makes skills safe to enable on a surface with a per-guild allow-list: the allow-list
/// is applied by the registry (a <c>FilteredToolRegistry</c>), and this only subtracts from it.
/// </para>
/// </remarks>
public static class SkillToolSet
{
    /// <summary>
    /// The tools to advertise right now.
    /// </summary>
    /// <param name="registry">The run's registry, or null for a run with no tools.</param>
    /// <param name="skills">The run's skills, or null when the host is not using skills.</param>
    /// <returns>
    /// The tool definitions for <see cref="LlmRequest.Tools"/>, ordered by name, or null when there
    /// is no registry.
    /// </returns>
    public static List<LlmToolDefinition>? Compose(IToolRegistry? registry, ISkillActivationState? skills)
    {
        var all = registry?.GetEnabledTools().ToList();

        if (all is null || skills is null)
        {
            return all;
        }

        if (skills.Available.Count == 0)
        {
            // Nothing to load, so the loader is dead weight in a prefix that is paid for on every
            // request. A surface with no skill files should cost exactly what it did before skills.
            return Sort(all.Where(t => !Matches(t.Name, skills.LoaderToolName)));
        }

        var gated = new HashSet<string>(
            skills.Available.SelectMany(s => s.Tools), StringComparer.OrdinalIgnoreCase);

        var unlocked = new HashSet<string>(
            skills.Activated.SelectMany(s => s.Tools), StringComparer.OrdinalIgnoreCase);

        return Sort(all.Where(t => !gated.Contains(t.Name) || unlocked.Contains(t.Name)));
    }

    /// <summary>
    /// Ordered by name, ordinal — the same order the registry itself uses, restated because the
    /// tool array serializes at position 0 of the request and its order is part of every prompt-cache
    /// breakpoint behind it.
    /// </summary>
    private static List<LlmToolDefinition> Sort(IEnumerable<LlmToolDefinition> tools) =>
        tools.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();

    private static bool Matches(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
