using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Typed access to the run's skill session in <see cref="ToolContext.Items"/>.
/// </summary>
/// <remarks>
/// <para>
/// The session is the one piece of state the loader tool and the loop both touch: the tool
/// activates, the loop reads the activation and re-composes the advertised tool array. The loop
/// gets it from <c>AgentContext.Skills</c>; the tool gets it from here, because a tool is handed a
/// <see cref="ToolContext"/> and nothing else, and because resolving it from the container instead
/// would make the two ends the same object only as long as nobody changed a service lifetime.
/// </para>
/// <para>
/// Same posture as <see cref="DmToolContextExtensions"/>: the key is spelled in one place, and the
/// engine never reads the bag.
/// </para>
/// </remarks>
public static class SkillToolContextExtensions
{
    /// <summary>The <see cref="ToolContext.Items"/> key holding the run's skill session.</summary>
    public const string SkillsKey = "agent.skills";

    /// <summary>The run's skill session, or null when this scope has no skills.</summary>
    public static ISkillActivationState? GetSkills(this ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(SkillsKey, out var value) ? value as ISkillActivationState : null;
    }

    /// <summary>Puts the run's skill session in the bag, or removes it with <c>null</c>.</summary>
    public static void SetSkills(this ToolContext context, ISkillActivationState? skills)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (skills is null)
        {
            context.Items.Remove(SkillsKey);
        }
        else
        {
            context.Items[SkillsKey] = skills;
        }
    }
}
