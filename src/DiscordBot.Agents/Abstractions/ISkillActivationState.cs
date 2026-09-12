using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents.Abstractions;

/// <summary>
/// The skills one run may load, and the ones it has loaded so far.
/// </summary>
/// <remarks>
/// <para>
/// Run-scoped by construction: a session belongs to a single <see cref="AgentContext"/> and is
/// mutated by the loader tool as the run proceeds. The loop reads <see cref="Activated"/> after each
/// tool round and re-composes the advertised tool array from it, which is the whole of the
/// mechanism on the engine's side.
/// </para>
/// <para>
/// The host decides what is in <see cref="Available"/> — which skills exist, which surface they
/// belong to, and, crucially, which of each skill's named tools it is willing to advertise. The
/// engine narrows further but never widens: a tool reaches the model only if the registry already
/// holds it, so activating a skill cannot reach past whatever allow-list the registry applies.
/// </para>
/// </remarks>
public interface ISkillActivationState
{
    /// <summary>Every skill this run may load, ordered by key.</summary>
    IReadOnlyList<AgentSkill> Available { get; }

    /// <summary>The skills loaded so far, in activation order.</summary>
    /// <remarks>
    /// A host may pre-activate skills — carrying a multi-turn conversation's loaded skills into the
    /// next turn, say — in which case they are here before the first model call and their tools are
    /// advertised from the start.
    /// </remarks>
    IReadOnlyList<AgentSkill> Activated { get; }

    /// <summary>
    /// The name of the tool that loads a skill, as the host advertises it.
    /// </summary>
    /// <remarks>
    /// The engine needs it for one thing only: when <see cref="Available"/> is empty there is
    /// nothing to load, so the loader is dropped from the advertised array rather than costing its
    /// schema on every request of a surface that has no skills.
    /// </remarks>
    string LoaderToolName { get; }

    /// <summary>The available skill with this key, or null.</summary>
    /// <param name="key">The skill key, matched case-insensitively.</param>
    AgentSkill? Find(string key);

    /// <summary>Whether <paramref name="key"/> has already been activated.</summary>
    /// <param name="key">The skill key, matched case-insensitively.</param>
    bool IsActivated(string key);

    /// <summary>
    /// Activates the skill with this key, and returns it. Null when no available skill has the key.
    /// </summary>
    /// <remarks>
    /// Idempotent: re-activating an active skill returns it without adding it twice, because a model
    /// that loads the same skill again wants to re-read the instructions, not an error.
    /// </remarks>
    /// <param name="key">The skill key, matched case-insensitively.</param>
    AgentSkill? Activate(string key);
}
