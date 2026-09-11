using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents.Abstractions;

/// <summary>
/// Reads the skill files of one directory.
/// </summary>
/// <remarks>
/// A directory is the unit because it is the only grouping the engine can understand without
/// learning the host's taxonomy: <em>which</em> skills a particular assistant should offer is the
/// host's question, and it answers it by pointing this at one directory or another.
/// </remarks>
public interface ISkillLibrary
{
    /// <summary>
    /// The skills in <paramref name="directory"/>, ordered by key.
    /// </summary>
    /// <remarks>
    /// Cached and hot-reloaded on the same terms as a prompt file, so editing a skill takes effect
    /// without a restart. A missing or empty directory is not an error — it is a surface with no
    /// skills, which is the normal state of a surface nobody has written any for.
    /// </remarks>
    /// <param name="directory">
    /// Absolute, or relative to the application or working directory. Blank returns no skills.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<AgentSkill>> LoadAsync(string directory, CancellationToken cancellationToken = default);
}
