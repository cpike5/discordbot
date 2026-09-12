using DiscordBot.Agents;
using DiscordBot.Core.Enums;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Answers what an assistant surface actually advertises, and what it costs.
/// </summary>
/// <remarks>
/// <para>
/// "Actually advertises" is the whole point, and it is narrower than "everything registered": the
/// per-request tool array is the skill-aware set (<see cref="SkillToolSet.Compose"/>) over the
/// registry a run would be handed — which, for a guild, is that guild's allow-list. A tool held
/// behind a skill, or turned off for a guild, is not in the prefix and is not billed for.
/// </para>
/// <para>
/// Lives in Infrastructure rather than Core for the usual reason: its return type is made of engine
/// types.
/// </para>
/// </remarks>
public interface IPromptSurfaceReporter
{
    /// <summary>
    /// The report for one surface, or null when the assistant is not configured (no API key, so no
    /// registry and nothing to measure).
    /// </summary>
    /// <param name="scope">
    /// <see cref="ToolScopes.Guild"/> or <see cref="ToolScopes.Dm"/>. Anything else returns null —
    /// the feature-request assistant builds its own single-provider registry and has no surface to
    /// report on.
    /// </param>
    /// <param name="guildId">
    /// The guild whose allow-list to apply, for <see cref="ToolScopes.Guild"/>. Null reports the
    /// house set — every guild-scoped tool, before any guild has narrowed it.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PromptSurfaceReport?> ReportAsync(
        ToolScopes scope,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);
}
