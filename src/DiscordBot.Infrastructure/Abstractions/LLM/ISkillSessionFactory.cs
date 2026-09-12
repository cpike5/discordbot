using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Builds the <see cref="SkillSession"/> for one assistant exchange: the skills of that surface's
/// directory, narrowed to the tools the run will actually advertise.
/// </summary>
/// <remarks>
/// The narrowing is the point. A skill file names the tools its instructions are about; whether the
/// run may use them is a different question, answered by the registry it is handed — the house set,
/// and on the guild surface the per-guild allow-list on top of it. Doing it here means the roster,
/// the loader's answer and the tool array all agree, and that loading a skill can never reach past
/// the allow-list.
/// </remarks>
public interface ISkillSessionFactory
{
    /// <summary>
    /// A session over the skills in <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Where the surface's skill files live. Blank means no skills.</param>
    /// <param name="registry">
    /// The run's registry, whose advertised tools the skills are narrowed to. Null (a run with no
    /// tools) narrows every skill to instructions alone.
    /// </param>
    /// <param name="preActivatedKeys">
    /// Skills to treat as already loaded — a multi-turn surface replays the previous turn's here.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SkillSession> CreateAsync(
        string directory,
        IToolRegistry? registry,
        IEnumerable<string>? preActivatedKeys = null,
        CancellationToken cancellationToken = default);
}
