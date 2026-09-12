using System.Text;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// Renders a run's skills into the system prompt: a one-line-per-skill roster of what can be
/// loaded, and the full instructions of anything already loaded.
/// </summary>
/// <remarks>
/// <para>
/// This is prompt surface, and the expensive kind — it sits in the cached system-prompt prefix, so
/// changing the wording invalidates it and changing the <em>order</em> of the roster invalidates it
/// for no benefit at all. <see cref="SkillSession"/> orders by key for exactly that reason.
/// </para>
/// <para>
/// The host decides where the block lands. Appending it to the end of the composed prompt is the
/// usual answer and is what keeps the rest of the prompt byte-identical to what it was before
/// skills existed.
/// </para>
/// </remarks>
public static class SkillRoster
{
    /// <summary>Heading for the skills a run may still load.</summary>
    private const string AvailableHeading = "## Skills";

    /// <summary>Heading for the skills a run has already loaded.</summary>
    private const string LoadedHeading = "## Loaded skills";

    /// <summary>
    /// The skills block for <paramref name="skills"/>, or an empty string when there is nothing to
    /// say.
    /// </summary>
    /// <remarks>
    /// Call it when composing the prompt, which is before the first model call: anything already
    /// activated at that point was replayed from a previous turn, and its instructions belong in the
    /// prompt because the tool result that carried them the first time is not in the history.
    /// A skill loaded <em>during</em> a run needs nothing here — the loader's own result is the
    /// instructions, and it is in the history for the rest of the run.
    /// </remarks>
    /// <param name="skills">The run's skills, or null.</param>
    public static string Render(ISkillActivationState? skills)
    {
        if (skills is null)
        {
            return string.Empty;
        }

        var loaded = skills.Activated;

        // By key rather than by skill: see SkillSession's note on record equality over a collection.
        var loadable = skills.Available.Where(s => !skills.IsActivated(s.Key)).ToList();

        if (loadable.Count == 0 && loaded.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        if (loadable.Count > 0)
        {
            builder.Append(AvailableHeading).Append("\n\n");
            builder.Append(
                "Extra instructions, and the tools that go with them, that you can load when a "
                + $"request needs them. Load one by calling `{skills.LoaderToolName}` with its key; "
                + "it costs a step, so load one only when the request is actually about it, and "
                + "don't load one you were not asked for.\n\n");

            foreach (var skill in loadable)
            {
                builder.Append("- `").Append(skill.Key).Append("` — ").Append(skill.Summary).Append('\n');
            }
        }

        if (loaded.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(LoadedHeading).Append("\n\n");
            builder.Append(
                "You already have these; their instructions follow. Don't load them again.\n");

            foreach (var skill in loaded)
            {
                builder.Append("\n### ").Append(skill.Key).Append("\n\n")
                    .Append(skill.Instructions.TrimEnd()).Append('\n');
            }
        }

        return builder.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// <paramref name="prompt"/> with the skills block appended, or unchanged when there is nothing
    /// to append.
    /// </summary>
    /// <param name="prompt">The composed system prompt.</param>
    /// <param name="skills">The run's skills, or null.</param>
    public static string Append(string prompt, ISkillActivationState? skills)
    {
        var block = Render(skills);

        if (block.Length == 0)
        {
            return prompt;
        }

        return string.IsNullOrEmpty(prompt) ? block : prompt.TrimEnd() + "\n\n" + block;
    }
}
