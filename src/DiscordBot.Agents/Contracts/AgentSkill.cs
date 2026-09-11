namespace DiscordBot.Agents.Contracts;

/// <summary>
/// A named bundle of instructions, and optionally the tools those instructions are about, that a
/// run can load on demand instead of carrying from the first byte.
/// </summary>
/// <remarks>
/// <para>
/// The problem a skill solves is that every tool costs its schema on every request, whether or not
/// the question needs it, and a tool's instructions cost their prose in the system prompt on the
/// same terms. A skill moves both behind a one-line summary: the model reads the summary, decides
/// the request needs the skill, and pays for the rest only then.
/// </para>
/// <para>
/// A skill with no <see cref="Tools"/> is legitimate and is something a tools-only mechanism cannot
/// express — a body of standing orders for a kind of request, loaded when that kind of request
/// arrives.
/// </para>
/// </remarks>
public sealed record AgentSkill
{
    /// <summary>
    /// The identifier the model passes to the loader tool. Lower-case and stable — it is written
    /// into the roster, so changing one costs a prompt-cache prefix.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// One line saying when to load this skill.
    /// </summary>
    /// <remarks>
    /// This is the entire basis for the model's decision, and the only part of the skill that is
    /// paid for on every request. Write it as the condition that should trigger it ("Look up
    /// moderation cases…"), not as a description of the file.
    /// </remarks>
    public required string Summary { get; init; }

    /// <summary>
    /// The tools this skill unlocks, by name. Empty for a skill of pure standing orders.
    /// </summary>
    /// <remarks>
    /// A name here is a request, never a grant: the host narrows the list to what it actually
    /// advertises before the run sees it, and the loop only ever un-hides tools the registry
    /// already holds. Loading a skill can therefore never widen a caller's reach.
    /// </remarks>
    public IReadOnlyList<string> Tools { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The body: what the model should read once the skill is loaded.
    /// </summary>
    public required string Instructions { get; init; }

    /// <summary>Where the skill was loaded from, for logging. Not shown to the model.</summary>
    public string? Source { get; init; }
}
